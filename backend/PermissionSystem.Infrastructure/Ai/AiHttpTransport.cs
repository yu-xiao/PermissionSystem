using System.Net;
using System.Net.Sockets;

namespace PermissionSystem.Infrastructure.Ai;

internal static class AiHttpTransport
{
    private static readonly HttpRequestOptionsKey<ValidatedEndpoint> EndpointKey = new("Ai.ValidatedEndpoint");

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        // Each request must use its own validated DNS result and private-network policy.
        PooledConnectionLifetime = TimeSpan.Zero,
        ConnectCallback = ConnectAsync
    };

    public static async Task PrepareAsync(HttpRequestMessage request, bool allowPrivateNetwork, CancellationToken cancellationToken)
    {
        var addresses = await OpenAiCompatibleEndpointValidator.ValidateResolvedAddressesAsync(
            request.RequestUri!, allowPrivateNetwork, cancellationToken);
        PinEndpoint(request, addresses, allowPrivateNetwork);
    }

    internal static void PinEndpoint(HttpRequestMessage request, IPAddress[] addresses, bool allowPrivateNetwork)
    {
        OpenAiCompatibleEndpointValidator.ValidateAddresses(addresses, allowPrivateNetwork);
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        request.Options.Set(EndpointKey, new ValidatedEndpoint(request.RequestUri!, addresses.ToArray()));
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        if (!context.InitialRequestMessage.Options.TryGetValue(EndpointKey, out var endpoint) ||
            endpoint.Uri != context.InitialRequestMessage.RequestUri ||
            !string.Equals(endpoint.Uri.IdnHost, context.DnsEndPoint.Host, StringComparison.OrdinalIgnoreCase) ||
            endpoint.Uri.Port != context.DnsEndPoint.Port)
        {
            throw new HttpRequestException("The AI endpoint has not been validated for this connection.");
        }

        SocketException? lastException = null;
        foreach (var address in endpoint.Addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Uri.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                socket.Dispose();
                lastException = exception;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("The validated AI endpoint could not be reached.", lastException);
    }

    private sealed record ValidatedEndpoint(Uri Uri, IPAddress[] Addresses);
}
