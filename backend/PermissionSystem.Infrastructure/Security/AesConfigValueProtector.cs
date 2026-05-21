using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Infrastructure.Security;

public sealed class AesConfigValueProtector : IConfigValueProtector
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int VersionSize = 1;
    private readonly byte[] _key;

    public AesConfigValueProtector(IConfiguration configuration)
    {
        var configuredKey = configuration["Security:SystemConfigEncryptionKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException("Security:SystemConfigEncryptionKey must be configured before encrypted system configs can be used.");
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    }

    public string Protect(string value)
    {
        var plainBytes = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var payload = new byte[VersionSize + NonceSize + TagSize + cipherBytes.Length];
        payload[0] = 1;
        Buffer.BlockCopy(nonce, 0, payload, VersionSize, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, VersionSize + NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, payload, VersionSize + NonceSize + TagSize, cipherBytes.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedValue)
    {
        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return protectedValue;
        }

        var payload = Convert.FromBase64String(protectedValue[Prefix.Length..]);
        if (payload.Length < VersionSize + NonceSize + TagSize || payload[0] != 1)
        {
            throw new CryptographicException("Invalid protected config value.");
        }

        var cipherLength = payload.Length - VersionSize - NonceSize - TagSize;
        var nonce = payload.AsSpan(VersionSize, NonceSize);
        var tag = payload.AsSpan(VersionSize + NonceSize, TagSize);
        var cipherBytes = payload.AsSpan(VersionSize + NonceSize + TagSize, cipherLength);
        var plainBytes = new byte[cipherLength];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
