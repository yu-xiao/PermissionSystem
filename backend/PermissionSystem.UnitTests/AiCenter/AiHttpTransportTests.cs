using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Infrastructure.Ai;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class AiHttpTransportTests
{
    [Fact]
    public async Task SendAsync_RejectsUntrustedHttpsCertificate()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(5));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pkcs12), null);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new HttpClient(AiHttpTransport.CreateHandler());
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://localhost:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        AiHttpTransport.PinEndpoint(request, [IPAddress.Loopback], true);
        var responseTask = client.SendAsync(request, timeout.Token);
        using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
        await using var tls = new SslStream(connection.GetStream());
        try
        {
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, timeout.Token);
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            // A client rejecting the certificate can terminate the server handshake.
        }
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => responseTask);
        Assert.IsType<AuthenticationException>(error.InnerException);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("192.168.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("64:ff9b::a00:1")]
    public void PinEndpoint_RejectsPrivateAddressesWithoutExplicitPermission(string address)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://provider.example.test/");
        Assert.Throws<AiModelGatewayException>(() => AiHttpTransport.PinEndpoint(request,
            [IPAddress.Parse("8.8.8.8"), IPAddress.Parse(address)], false));
    }

    [Fact]
    public void CreateHandler_DisablesRedirectProxyCookiesAndConnectionReuse()
    {
        using var handler = AiHttpTransport.CreateHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.False(handler.UseCookies);
        Assert.Equal(TimeSpan.Zero, handler.PooledConnectionLifetime);
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
    }

    [Theory]
    [InlineData(302)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task SendAsync_UsesPinnedAddressAndDoesNotFollowRedirect(int status)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = RespondAsync(listener, status, timeout.Token);
        using var client = new HttpClient(AiHttpTransport.CreateHandler());
        // This hostname cannot resolve: a successful response requires connecting to the pinned IP.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://pinned.invalid:{port}/");
        AiHttpTransport.PinEndpoint(request, [IPAddress.Loopback], true);
        using var response = await client.SendAsync(request, timeout.Token);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Contains($"Host: pinned.invalid:{port}", await server);
        Assert.False(listener.Pending());
    }

    [Fact]
    public async Task SendAsync_CannotReusePermissiveConnectionForUnvalidatedRequest()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var uri = new Uri($"http://localhost:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        using var client = new HttpClient(AiHttpTransport.CreateHandler());
        using var first = new HttpRequestMessage(HttpMethod.Get, uri);
        await AiHttpTransport.PrepareAsync(first, true, timeout.Token);
        var responseTask = client.SendAsync(first, timeout.Token);
        using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
        await ReadHeadersAsync(connection.GetStream(), timeout.Token);
        await connection.GetStream().WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"), timeout.Token);
        using var response = await responseTask;

        using var second = new HttpRequestMessage(HttpMethod.Get, uri);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(second, timeout.Token));
        await Assert.ThrowsAsync<AiModelGatewayException>(() => AiHttpTransport.PrepareAsync(second, false, timeout.Token));
    }

    [Fact]
    public async Task SendAsync_RejectsChangedDestinationAfterValidation()
    {
        using var client = new HttpClient(AiHttpTransport.CreateHandler());
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:1234/");
        AiHttpTransport.PinEndpoint(request, [IPAddress.Loopback], true);
        request.RequestUri = new Uri("http://localhost:5678/");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request));
    }

    private static async Task<string> RespondAsync(TcpListener listener, int status, CancellationToken token)
    {
        using var connection = await listener.AcceptTcpClientAsync(token);
        var stream = connection.GetStream();
        var headers = await ReadHeadersAsync(stream, token);
        var location = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/redirected";
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Redirect\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), token);
        return headers;
    }

    private static async Task<string> ReadHeadersAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[1];
        var headers = new StringBuilder();
        while (headers.Length < 16384 && await stream.ReadAsync(buffer, token) > 0)
        {
            headers.Append((char)buffer[0]);
            if (headers.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) return headers.ToString();
        }
        throw new InvalidOperationException("The test request headers are incomplete.");
    }
}
