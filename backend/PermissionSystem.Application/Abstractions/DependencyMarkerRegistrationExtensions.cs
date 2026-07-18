using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace PermissionSystem.Application.Abstractions;

public static class DependencyMarkerRegistrationExtensions
{
    private static readonly Type[] DependencyMarkerTypes =
    [
        typeof(ITransientDependency),
        typeof(IScopedDependency),
        typeof(ISingletonDependency)
    ];

    public static IServiceCollection AddMarkedDependencies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var implementationType in assemblies
            .Where(assembly => assembly is not null)
            .Distinct()
            .SelectMany(GetLoadableTypes)
            .Where(IsDependencyImplementation))
        {
            var lifetime = GetDependencyLifetime(implementationType);
            if (lifetime is null)
            {
                continue;
            }

            var serviceTypes = GetDefaultServiceTypes(implementationType).ToArray();
            if (serviceTypes.Length == 0)
            {
                AddIfNotRegistered(services, implementationType, implementationType, lifetime.Value);
                continue;
            }

            foreach (var serviceType in serviceTypes)
            {
                AddIfNotRegistered(services, serviceType, implementationType, lifetime.Value);
            }
        }

        return services;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }

    private static bool IsDependencyImplementation(Type type)
    {
        return type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false };
    }

    private static ServiceLifetime? GetDependencyLifetime(Type implementationType)
    {
        var matchedMarkers = DependencyMarkerTypes
            .Where(markerType => markerType.IsAssignableFrom(implementationType))
            .ToArray();

        return matchedMarkers.Length switch
        {
            0 => null,
            1 when matchedMarkers[0] == typeof(ITransientDependency) => ServiceLifetime.Transient,
            1 when matchedMarkers[0] == typeof(IScopedDependency) => ServiceLifetime.Scoped,
            1 when matchedMarkers[0] == typeof(ISingletonDependency) => ServiceLifetime.Singleton,
            1 => null,
            _ => throw new InvalidOperationException(
                $"{implementationType.FullName} can only implement one dependency lifetime marker.")
        };
    }

    private static IEnumerable<Type> GetDefaultServiceTypes(Type implementationType)
    {
        return implementationType
            .GetInterfaces()
            .Where(IsDefaultServiceType);
    }

    private static bool IsDefaultServiceType(Type serviceType)
    {
        if (DependencyMarkerTypes.Contains(serviceType))
        {
            return false;
        }

        if (serviceType.Namespace is null)
        {
            return false;
        }

        return serviceType.Namespace.StartsWith("PermissionSystem.", StringComparison.Ordinal);
    }

    private static void AddIfNotRegistered(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        ServiceLifetime lifetime)
    {
        var allowMultipleImplementations = serviceType.Name.EndsWith("Handler", StringComparison.Ordinal);
        if (allowMultipleImplementations)
        {
            if (services.Any(descriptor =>
                descriptor.ServiceType == serviceType &&
                descriptor.ImplementationType == implementationType))
            {
                return;
            }
        }
        else if (services.Any(descriptor => descriptor.ServiceType == serviceType))
        {
            return;
        }

        services.Add(new ServiceDescriptor(serviceType, implementationType, lifetime));
    }
}
