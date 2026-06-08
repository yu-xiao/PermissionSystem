using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using PermissionSystem.Application.Integration;

namespace PermissionSystem.Infrastructure.Integration;

public sealed class WebhookHttpSender : IWebhookHttpSender
{
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookHttpSender(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<WebhookSendResult> SendAsync(
        string targetUrl,
        string eventType,
        string payload,
        string secret,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = CreateSignature(secret, timestamp, payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Webhook-Event", eventType);
        request.Headers.Add("X-Webhook-Timestamp", timestamp);
        request.Headers.Add("X-Webhook-Signature", signature);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PermissionSystem-Webhook", "1.0"));

        try
        {
            var client = _httpClientFactory.CreateClient("Webhook");
            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return new WebhookSendResult
            {
                Succeeded = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseBody = responseBody
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new WebhookSendResult
            {
                Succeeded = false,
                ResponseBody = ex.Message
            };
        }
    }

    private static string CreateSignature(string secret, string timestamp, string payload)
    {
        var message = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
    }
}
