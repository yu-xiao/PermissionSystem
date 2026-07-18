using Microsoft.Extensions.DependencyInjection;
using PermissionSystem.Application.Abstractions;

namespace PermissionSystem.Tests.DependencyInjection;

public sealed class DependencyMarkerRegistrationTests
{
    [Fact]
    public void AddMarkedDependencies_RegistersInterfacesWithExpectedLifetimes()
    {
        var services = new ServiceCollection();

        services.AddMarkedDependencies(typeof(DependencyMarkerRegistrationTests).Assembly);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMarkerScopedService) &&
            descriptor.ImplementationType == typeof(MarkerScopedService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMarkerTransientService) &&
            descriptor.ImplementationType == typeof(MarkerTransientService) &&
            descriptor.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IMarkerSingletonService) &&
            descriptor.ImplementationType == typeof(MarkerSingletonService) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddMarkedDependencies_RegistersSelfWhenNoApplicationInterfaceExists()
    {
        var services = new ServiceCollection();

        services.AddMarkedDependencies(typeof(DependencyMarkerRegistrationTests).Assembly);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(SelfRegisteredJob) &&
            descriptor.ImplementationType == typeof(SelfRegisteredJob) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddMarkedDependencies_DoesNotOverrideExistingRegistrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMarkerScopedService, ExistingMarkerScopedService>();

        services.AddMarkedDependencies(typeof(DependencyMarkerRegistrationTests).Assembly);

        var descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(IMarkerScopedService));
        Assert.Equal(typeof(ExistingMarkerScopedService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddMarkedDependencies_AllowsMultipleHandlerImplementations()
    {
        var services = new ServiceCollection();

        services.AddMarkedDependencies(typeof(DependencyMarkerRegistrationTests).Assembly);

        var descriptors = services
            .Where(service => service.ServiceType == typeof(IMarkerHandler))
            .ToArray();
        Assert.Equal(2, descriptors.Length);
        Assert.Contains(descriptors, descriptor => descriptor.ImplementationType == typeof(FirstMarkerHandler));
        Assert.Contains(descriptors, descriptor => descriptor.ImplementationType == typeof(SecondMarkerHandler));
    }

    private interface IMarkerScopedService : IScopedDependency;

    private interface IMarkerTransientService : ITransientDependency;

    private interface IMarkerSingletonService : ISingletonDependency;

    private interface IMarkerHandler : IScopedDependency;

    private sealed class MarkerScopedService : IMarkerScopedService;

    private sealed class MarkerTransientService : IMarkerTransientService;

    private sealed class MarkerSingletonService : IMarkerSingletonService;

    private sealed class ExistingMarkerScopedService : IMarkerScopedService;

    private sealed class SelfRegisteredJob : IScopedDependency;

    private sealed class FirstMarkerHandler : IMarkerHandler;

    private sealed class SecondMarkerHandler : IMarkerHandler;
}
