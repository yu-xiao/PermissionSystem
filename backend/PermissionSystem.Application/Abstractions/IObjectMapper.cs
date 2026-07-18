namespace PermissionSystem.Application.Abstractions;

public interface IObjectMapper<in TSource, out TDestination> : IScopedDependency
{
    TDestination Map(TSource source);
}
