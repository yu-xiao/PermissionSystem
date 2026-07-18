# DI and Mapping Guide

## DI registration standard

PermissionSystem uses Microsoft.Extensions.DependencyInjection as the standard service registration mechanism. New application and infrastructure components should prefer marker-based automatic registration instead of editing `DependencyInjection` files for every service.

Available lifetime markers:

- `ITransientDependency`: register the implementation as transient.
- `IScopedDependency`: register the implementation as scoped.
- `ISingletonDependency`: register the implementation as singleton.

Recommended usage:

```csharp
public interface IUserDetailMapper : IScopedDependency
{
    UserDetailResponse Map(User user);
}

public sealed class UserDetailMapper : IUserDetailMapper
{
    public UserDetailResponse Map(User user)
    {
        // explicit complex mapping
    }
}
```

Automatic scanning rules:

- `PermissionSystem.Application` scans the Application assembly.
- `PermissionSystem.Infrastructure` scans the Infrastructure assembly.
- Module assemblies can be passed to `AddApplication(...)` or `AddInfrastructure(...)` when a future module is split into its own assembly.
- Implementations of `IScopedDependency` are registered as scoped.
- Implementations of `ITransientDependency` are registered as transient.
- Implementations of `ISingletonDependency` are registered as singleton.
- The default service type is the PermissionSystem interface implemented by the class.
- If no PermissionSystem service interface exists, the class is registered as itself.
- Existing registrations are not overwritten.
- Handler interfaces ending with `Handler` allow multiple implementations and are registered for `IEnumerable<THandler>` resolution.

The following components should use DI registration:

- Application services
- Repositories
- Policies and strategies
- Handlers
- Resolvers
- Business callbacks
- Workflow and approval handlers
- Dedicated mapper classes for complex mapping

Manual registrations are still required when the registration depends on configuration, factories, framework builders, hosted infrastructure, or third-party integration setup. Examples include DbContext, OpenIddict, authentication, authorization, Hangfire, RabbitMQ, Redis, HttpClient, `ICacheService`, `IMessageBus`, and provider-selected services such as `IFileStorageService`.

## AutoMapper usage standard

AutoMapper is optional in this project. Do not introduce AutoMapper only to satisfy simple assignment convenience if the project does not already use it.

When AutoMapper is present, register profiles by assembly scanning:

```csharp
services.AddAutoMapper(applicationAssembly);
```

If profiles are distributed across multiple module assemblies, pass those assemblies to `AddAutoMapper(...)`. Avoid repeated registrations like `AddAutoMapper(typeof(SomeProfile))` for individual profiles. New `Profile` classes should become effective through assembly scanning.

AutoMapper is only allowed for simple DTO mapping:

- Entity to list/detail DTO with direct property assignment.
- Request DTO to command-like object with direct property assignment.
- Flat object copy without business decisions.

The following logic is forbidden in AutoMapper profiles, converters, resolvers, or mapping actions:

- Database queries
- Permission checks
- Tenant filtering
- Workflow approval decisions
- State machine transitions
- SSO binding decisions
- Field masking or desensitization
- Security policy decisions

Complex mapping must be explicit in an Application Service or in a dedicated mapper class.

## Dedicated mapper class standard

For complex mapping, use explicit mapper classes instead of AutoMapper. A shared contract is available:

```csharp
public interface IObjectMapper<TSource, TDestination>
{
    TDestination Map(TSource source);
}
```

`IObjectMapper<TSource, TDestination>` is scoped by default through `IScopedDependency`, so implementations are automatically registered by DI scanning.

Recommended examples:

- `UserDetailMapper`
- `WorkflowInstanceDetailMapper`
- `RolePermissionMatrixMapper`

Use dedicated mapper classes when mapping needs branching, aggregation, masking after permission decisions, tenant-aware rules, workflow state interpretation, or composition of multiple domain objects.
