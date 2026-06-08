namespace PermissionSystem.Application.Integration;

public sealed class ApiClientContext : IApiClientContext
{
    public Guid? ClientId { get; private set; }

    public string? ClientCode { get; private set; }

    public string? AllowedScopes { get; private set; }

    public bool IsAuthenticated => ClientId.HasValue;

    public void SetClient(Guid clientId, string clientCode, string? allowedScopes)
    {
        ClientId = clientId;
        ClientCode = clientCode;
        AllowedScopes = allowedScopes;
    }
}
