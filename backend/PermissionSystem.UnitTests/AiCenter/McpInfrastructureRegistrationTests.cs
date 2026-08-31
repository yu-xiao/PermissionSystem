using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Infrastructure;
using PermissionSystem.Infrastructure.Locks;

namespace PermissionSystem.UnitTests.AiCenter;

public sealed class McpInfrastructureRegistrationTests
{
    [Fact]
    public void AddMcpInfrastructure_RegistersMemoryDistributedLockOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=PermissionSystem;Integrated Security=True;TrustServerCertificate=True",
                ["Cache:Provider"] = "Memory",
                ["RateLimit:Provider"] = "Memory"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMcpInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        Assert.IsType<MemoryDistributedLock>(
            serviceProvider.GetRequiredService<IDistributedLock>());
    }
}
