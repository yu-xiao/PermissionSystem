using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Tenants;

public sealed class SystemTenantScope : ISystemTenantScope
{
    private readonly TenantContext _tenantContext;
    private readonly ILogger<SystemTenantScope> _logger;

    public SystemTenantScope(
        TenantContext tenantContext,
        ILogger<SystemTenantScope> logger)
    {
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public IDisposable Begin(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        if (_tenantContext.IsHttpRequest)
        {
            throw new BusinessException(
                ErrorCode.Forbidden,
                "System tenant scope cannot be opened during an HTTP request.");
        }

        var normalizedOperation = operation.Trim();
        _tenantContext.EnterSystemScope();
        _logger.LogInformation(
            "System tenant scope entered. Operation: {SystemTenantOperation}",
            normalizedOperation);

        return new ScopeLease(this, normalizedOperation, Stopwatch.StartNew());
    }

    private void End(string operation, Stopwatch stopwatch)
    {
        _tenantContext.ExitSystemScope();
        stopwatch.Stop();
        _logger.LogInformation(
            "System tenant scope exited. Operation: {SystemTenantOperation}, ElapsedMilliseconds: {ElapsedMilliseconds}",
            operation,
            stopwatch.ElapsedMilliseconds);
    }

    private sealed class ScopeLease : IDisposable
    {
        private readonly SystemTenantScope _owner;
        private readonly string _operation;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public ScopeLease(SystemTenantScope owner, string operation, Stopwatch stopwatch)
        {
            _owner = owner;
            _operation = operation;
            _stopwatch = stopwatch;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner.End(_operation, _stopwatch);
        }
    }
}
