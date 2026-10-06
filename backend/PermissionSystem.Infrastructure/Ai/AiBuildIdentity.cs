using System.Security.Cryptography;
using PermissionSystem.Application.AiCenter;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Shared.Constants;

namespace PermissionSystem.Infrastructure.Ai;

public sealed class AiBuildIdentity : IAiBuildIdentity
{
    private static readonly Lazy<string> Build = new(() =>
    {
        var assemblies = new[] { typeof(AiBuildIdentity).Assembly, typeof(AiConversationService).Assembly,
            typeof(AiRun).Assembly, typeof(AiCenterConstants).Assembly }.OrderBy(a => a.GetName().Name, StringComparer.Ordinal);
        var manifest = string.Join('\n', assemblies.Select(a => a.GetName().Name + ":" +
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location)))));
        return AiScenarioSnapshots.Digest(manifest);
    });
    public string Identity => Build.Value;
}
