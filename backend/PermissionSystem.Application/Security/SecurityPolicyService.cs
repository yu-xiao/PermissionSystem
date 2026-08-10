using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Application.Common;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.Security;

public sealed class SecurityPolicyService : ISecurityPolicyService
{
    private readonly IRepository<SecurityPolicy> _policyRepository;
    private readonly IRepository<LoginFailureRecord> _loginFailureRepository;
    private readonly IRepository<SensitiveOperationVerification> _verificationRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<IpAccessRule> _ipRuleRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISensitiveOperationCodeProvider _codeProvider;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IStepUpVerificationStore _stepUpVerificationStore;
    private readonly ILogger<SecurityPolicyService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public SecurityPolicyService(
        IRepository<SecurityPolicy> policyRepository,
        IRepository<LoginFailureRecord> loginFailureRepository,
        IRepository<SensitiveOperationVerification> verificationRepository,
        IRepository<User> userRepository,
        IRepository<IpAccessRule> ipRuleRepository,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        ISensitiveOperationCodeProvider codeProvider,
        IPasswordHashService passwordHashService,
        IStepUpVerificationStore stepUpVerificationStore,
        ILogger<SecurityPolicyService> logger,
        IUnitOfWork unitOfWork)
    {
        _policyRepository = policyRepository;
        _loginFailureRepository = loginFailureRepository;
        _verificationRepository = verificationRepository;
        _userRepository = userRepository;
        _ipRuleRepository = ipRuleRepository;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _codeProvider = codeProvider;
        _passwordHashService = passwordHashService;
        _stepUpVerificationStore = stepUpVerificationStore;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<SecurityPolicyResponse> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        return ToResponse(await GetOrCreatePolicyAsync(cancellationToken));
    }

    public async Task<SecurityPolicyResponse> UpdatePolicyAsync(
        UpdateSecurityPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureSensitiveOperationVerifiedAsync("security:policy:update", force: true, cancellationToken);
        var policy = await GetOrCreatePolicyAsync(cancellationToken);
        ConcurrencyTokenGuard.EnsureMatches(policy, request.ConcurrencyToken);
        policy.PasswordMinLength = Math.Clamp(request.PasswordMinLength, 6, 128);
        policy.RequireDigit = request.RequireDigit;
        policy.RequireUppercase = request.RequireUppercase;
        policy.RequireLowercase = request.RequireLowercase;
        policy.RequireSpecialChar = request.RequireSpecialChar;
        policy.PasswordExpireDays = Math.Max(0, request.PasswordExpireDays);
        policy.LoginFailureLockThreshold = Math.Clamp(request.LoginFailureLockThreshold, 1, 50);
        policy.LoginFailureLockMinutes = Math.Clamp(request.LoginFailureLockMinutes, 1, 1440);
        policy.EnableMfa = request.EnableMfa;
        policy.EnableSensitiveOperationVerify = request.EnableSensitiveOperationVerify;
        policy.EnableIpWhitelist = request.EnableIpWhitelist;
        policy.EnableIpBlacklist = request.EnableIpBlacklist;
        _policyRepository.Update(policy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(policy);
    }

    public async Task ValidatePasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        var policy = await GetOrCreatePolicyAsync(cancellationToken);
        if (password.Length < policy.PasswordMinLength ||
            (policy.RequireDigit && !password.Any(char.IsDigit)) ||
            (policy.RequireUppercase && !password.Any(char.IsUpper)) ||
            (policy.RequireLowercase && !password.Any(char.IsLower)) ||
            (policy.RequireSpecialChar && !password.Any(ch => !char.IsLetterOrDigit(ch))))
        {
            throw new BusinessException(
                ErrorCode.ValidationFailed,
                $"Password must be at least {policy.PasswordMinLength} characters and meet the enabled complexity rules.");
        }
    }

    public async Task EnsureLoginAllowedAsync(
        string userName,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        if (!await IsIpAllowedAsync(ipAddress, cancellationToken))
        {
            throw new BusinessException(ErrorCode.Forbidden, "Current IP is not allowed to access the system.");
        }

        var now = DateTimeOffset.UtcNow;
        var record = FindFailureRecord(tenantId, userName, ipAddress);
        if (record?.LockedUntil > now)
        {
            throw new BusinessException(ErrorCode.Forbidden, $"Account or IP is locked until {record.LockedUntil:yyyy-MM-dd HH:mm:ss} UTC.");
        }
    }

    public async Task RecordLoginFailureAsync(
        Guid tenantId,
        string userName,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var policy = await GetOrCreatePolicyAsync(tenantId, cancellationToken);
        var normalizedUserName = NormalizeUserName(userName);
        var normalizedIp = NormalizeOptional(ipAddress);
        var record = FindFailureRecord(tenantId, normalizedUserName, normalizedIp);
        var now = DateTimeOffset.UtcNow;

        if (record is null)
        {
            record = new LoginFailureRecord
            {
                TenantId = tenantId,
                UserName = normalizedUserName,
                IpAddress = normalizedIp,
                FailureCount = 0
            };
            await _loginFailureRepository.AddAsync(record, cancellationToken);
        }

        record.FailureCount++;
        record.LastFailureAt = now;
        record.LockedUntil = record.FailureCount >= policy.LoginFailureLockThreshold
            ? now.AddMinutes(policy.LoginFailureLockMinutes)
            : null;
        _loginFailureRepository.Update(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearLoginFailureAsync(
        Guid tenantId,
        string userName,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserName = NormalizeUserName(userName);
        var normalizedIp = NormalizeOptional(ipAddress);
        foreach (var record in _loginFailureRepository.Query()
            .Where(entity => entity.TenantId == tenantId &&
                entity.UserName == normalizedUserName &&
                (entity.IpAddress == normalizedIp || entity.IpAddress == null))
            .ToList())
        {
            _loginFailureRepository.Remove(record);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SendSensitiveVerificationResponse> SendVerificationAsync(
        SendSensitiveVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "Current user is not authenticated.");
        var sessionId = TrimRequired(_currentUserService.SessionId, "Current session is required for step-up authentication.");

        var operationCode = TrimRequired(request.OperationCode, "Operation code is required.");
        var tenantId = ResolveTenantId();
        var now = DateTimeOffset.UtcNow;
        var recentChallenges = _verificationRepository.Query()
            .Count(entity => entity.UserId == userId &&
                entity.SessionId == sessionId &&
                entity.OperationCode == operationCode &&
                entity.CreatedAt >= now.AddMinutes(-10));
        if (recentChallenges >= 5)
        {
            throw new BusinessException(ErrorCode.TooManyRequests, "Too many step-up challenges. Please try again later.");
        }

        var expiresAt = now.AddMinutes(5);
        var challenge = new SensitiveOperationVerification
        {
            TenantId = tenantId,
            UserId = userId,
            SessionId = sessionId,
            OperationCode = operationCode,
            VerificationMethod = "Password",
            ExpiresAt = expiresAt
        };
        await _verificationRepository.AddAsync(challenge, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Step-up challenge created. UserId: {UserId}, OperationCode: {OperationCode}, ExpiresAt: {ExpiresAt}",
            userId,
            operationCode,
            expiresAt);

        return new SendSensitiveVerificationResponse
        {
            ChallengeId = challenge.Id,
            OperationCode = operationCode,
            VerificationMethod = challenge.VerificationMethod,
            ExpiresAt = expiresAt
        };
    }

    public async Task<VerifySensitiveOperationResponse> VerifyAsync(
        VerifySensitiveOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "Current user is not authenticated.");
        var sessionId = TrimRequired(_currentUserService.SessionId, "Current session is required for step-up authentication.");
        var tenantId = ResolveTenantId();
        var password = TrimRequired(request.Password, "Password is required.");
        var challenge = _verificationRepository.Query()
            .FirstOrDefault(entity => entity.Id == request.ChallengeId &&
                entity.UserId == userId &&
                entity.TenantId == tenantId &&
                entity.SessionId == sessionId);
        var now = DateTimeOffset.UtcNow;
        if (challenge is null ||
            challenge.ExpiresAt <= now ||
            challenge.LockedAt is not null ||
            challenge.VerifiedAt is not null ||
            challenge.UsedAt is not null)
        {
            throw new BusinessException(ErrorCode.Forbidden, "Step-up challenge is invalid or expired.");
        }

        var user = _userRepository.Query()
            .FirstOrDefault(entity => entity.Id == userId && entity.TenantId == tenantId && entity.IsEnabled);
        if (user is null || !_passwordHashService.VerifyPassword(user.PasswordHash, password))
        {
            await _stepUpVerificationStore.RegisterFailedAttemptAsync(
                challenge.Id,
                maxAttempts: 5,
                now,
                cancellationToken);
            _logger.LogWarning(
                "Step-up password verification failed. UserId: {UserId}, OperationCode: {OperationCode}, ChallengeId: {ChallengeId}",
                userId,
                challenge.OperationCode,
                challenge.Id);
            throw new BusinessException(ErrorCode.Forbidden, "Step-up password verification failed.");
        }

        var ticket = CreateTicket();
        var ticketExpiresAt = now.AddMinutes(2);
        if (!await _stepUpVerificationStore.MarkVerifiedAsync(
                challenge.Id,
                HashTicket(ticket),
                now,
                ticketExpiresAt,
                cancellationToken))
        {
            throw new BusinessException(ErrorCode.Forbidden, "Step-up challenge is invalid or expired.");
        }

        _logger.LogInformation(
            "Step-up challenge verified. UserId: {UserId}, OperationCode: {OperationCode}, ChallengeId: {ChallengeId}",
            userId,
            challenge.OperationCode,
            challenge.Id);
        return new VerifySensitiveOperationResponse
        {
            StepUpTicket = ticket,
            ExpiresAt = ticketExpiresAt
        };
    }

    public async Task EnsureSensitiveOperationVerifiedAsync(
        string operationCode,
        CancellationToken cancellationToken = default)
    {
        await EnsureSensitiveOperationVerifiedAsync(operationCode, force: false, cancellationToken);
    }

    public async Task EnsureSensitiveOperationVerifiedAsync(
        string operationCode,
        bool force,
        CancellationToken cancellationToken = default)
    {
        var policy = await GetOrCreatePolicyAsync(cancellationToken);
        if (!force && !policy.EnableSensitiveOperationVerify)
        {
            return;
        }

        var ticket = NormalizeOptional(_codeProvider.StepUpTicket)
            ?? throw new BusinessException(ErrorCode.Forbidden, "Sensitive operation verification is required.");
        var userId = _currentUserService.UserId
            ?? throw new BusinessException(ErrorCode.Unauthorized, "Current user is not authenticated.");
        var sessionId = TrimRequired(_currentUserService.SessionId, "Current session is required for step-up authentication.");
        var consumed = await _stepUpVerificationStore.TryConsumeTicketAsync(
            ResolveTenantId(),
            userId,
            sessionId,
            TrimRequired(operationCode, "Operation code is required."),
            HashTicket(ticket),
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!consumed)
        {
            _logger.LogWarning(
                "Step-up ticket rejected. UserId: {UserId}, OperationCode: {OperationCode}",
                userId,
                operationCode);
            throw new BusinessException(ErrorCode.Forbidden, "Step-up ticket is invalid, expired, or already used.");
        }
    }

    public async Task<bool> IsIpAllowedAsync(string? ipAddress, CancellationToken cancellationToken = default)
    {
        var policy = await GetOrCreatePolicyAsync(cancellationToken);
        var ip = NormalizeOptional(ipAddress);
        if (ip is null)
        {
            return true;
        }

        var tenantId = ResolveTenantId();
        var rules = _ipRuleRepository.Query()
            .Where(entity => entity.TenantId == tenantId && entity.IsEnabled)
            .ToList();
        if (policy.EnableIpBlacklist && rules.Any(rule => rule.RuleType == "Blacklist" && MatchIp(rule.IpPattern, ip)))
        {
            return false;
        }

        return !policy.EnableIpWhitelist || rules.Any(rule => rule.RuleType == "Whitelist" && MatchIp(rule.IpPattern, ip));
    }

    public Task<PagedResult<IpAccessRuleResponse>> GetIpRulesAsync(
        IpAccessRuleQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _ipRuleRepository.Query();
        if (!string.IsNullOrWhiteSpace(request.RuleType))
        {
            var ruleType = NormalizeRuleType(request.RuleType);
            query = query.Where(entity => entity.RuleType == ruleType);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity => entity.IpPattern.Contains(keyword) || (entity.Description != null && entity.Description.Contains(keyword)));
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var items = query.OrderBy(entity => entity.RuleType)
            .ThenBy(entity => entity.IpPattern)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<IpAccessRuleResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    public async Task<IpAccessRuleResponse> CreateIpRuleAsync(CreateIpAccessRuleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSensitiveOperationVerifiedAsync("security:ip-rule:create", force: true, cancellationToken);
        var rule = new IpAccessRule
        {
            RuleType = NormalizeRuleType(request.RuleType),
            IpPattern = TrimRequired(request.IpPattern, "IP pattern is required."),
            Description = NormalizeOptional(request.Description),
            IsEnabled = request.IsEnabled
        };
        await _ipRuleRepository.AddAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(rule);
    }

    public async Task<IpAccessRuleResponse> UpdateIpRuleAsync(Guid id, UpdateIpAccessRuleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSensitiveOperationVerifiedAsync("security:ip-rule:update", force: true, cancellationToken);
        var rule = await _ipRuleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "IP access rule was not found.");
        ConcurrencyTokenGuard.EnsureMatches(rule, request.ConcurrencyToken);
        rule.RuleType = NormalizeRuleType(request.RuleType);
        rule.IpPattern = TrimRequired(request.IpPattern, "IP pattern is required.");
        rule.Description = NormalizeOptional(request.Description);
        rule.IsEnabled = request.IsEnabled;
        _ipRuleRepository.Update(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(rule);
    }

    public async Task DeleteIpRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSensitiveOperationVerifiedAsync("security:ip-rule:delete", force: true, cancellationToken);
        var rule = await _ipRuleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "IP access rule was not found.");
        _ipRuleRepository.Remove(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<PagedResult<LoginFailureRecordResponse>> GetLoginFailuresAsync(
        LoginFailureQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _loginFailureRepository.Query();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity => entity.UserName.Contains(keyword) || (entity.IpAddress != null && entity.IpAddress.Contains(keyword)));
        }

        var totalCount = query.LongCount();
        var items = query.OrderByDescending(entity => entity.LastFailureAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<LoginFailureRecordResponse>.Create(items, request.PageIndex, request.PageSize, totalCount));
    }

    private static string CreateTicket()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashTicket(string ticket)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ticket)));
    }

    private async Task<SecurityPolicy> GetOrCreatePolicyAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        return await GetOrCreatePolicyAsync(tenantId, cancellationToken);
    }

    private async Task<SecurityPolicy> GetOrCreatePolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var policy = _policyRepository.Query().FirstOrDefault(entity => entity.TenantId == tenantId);
        if (policy is not null)
        {
            return policy;
        }

        policy = new SecurityPolicy { TenantId = tenantId };
        await _policyRepository.AddAsync(policy, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return policy;
    }

    private LoginFailureRecord? FindFailureRecord(Guid tenantId, string userName, string? ipAddress)
    {
        var normalizedUserName = NormalizeUserName(userName);
        var normalizedIp = NormalizeOptional(ipAddress);
        return _loginFailureRepository.Query()
            .FirstOrDefault(entity => entity.TenantId == tenantId &&
                entity.UserName == normalizedUserName &&
                entity.IpAddress == normalizedIp);
    }

    private Guid ResolveTenantId()
    {
        return _tenantContext.TenantId ??
            _currentUserService.TenantId ??
            Guid.Parse("10000000-0000-0000-0000-000000000001");
    }

    private static bool MatchIp(string pattern, string ip)
    {
        return IpAccessMatcher.Matches(pattern, ip);
    }

    private static string NormalizeRuleType(string value)
    {
        var ruleType = TrimRequired(value, "Rule type is required.");
        return ruleType.Equals("Whitelist", StringComparison.OrdinalIgnoreCase)
            ? "Whitelist"
            : ruleType.Equals("Blacklist", StringComparison.OrdinalIgnoreCase)
                ? "Blacklist"
                : throw new BusinessException(ErrorCode.ValidationFailed, "Rule type must be Whitelist or Blacklist.");
    }

    private static string NormalizeUserName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToUpperInvariant();
    }

    private static string TrimRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static SecurityPolicyResponse ToResponse(SecurityPolicy entity)
    {
        return new SecurityPolicyResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PasswordMinLength = entity.PasswordMinLength,
            RequireDigit = entity.RequireDigit,
            RequireUppercase = entity.RequireUppercase,
            RequireLowercase = entity.RequireLowercase,
            RequireSpecialChar = entity.RequireSpecialChar,
            PasswordExpireDays = entity.PasswordExpireDays,
            LoginFailureLockThreshold = entity.LoginFailureLockThreshold,
            LoginFailureLockMinutes = entity.LoginFailureLockMinutes,
            EnableMfa = entity.EnableMfa,
            EnableSensitiveOperationVerify = entity.EnableSensitiveOperationVerify,
            EnableIpWhitelist = entity.EnableIpWhitelist,
            EnableIpBlacklist = entity.EnableIpBlacklist,
            ConcurrencyToken = entity.RowVersion
        };
    }

    private static IpAccessRuleResponse ToResponse(IpAccessRule entity)
    {
        return new IpAccessRuleResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            RuleType = entity.RuleType,
            IpPattern = entity.IpPattern,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            CreatedAt = entity.CreatedAt,
            ConcurrencyToken = entity.RowVersion
        };
    }

    private static LoginFailureRecordResponse ToResponse(LoginFailureRecord entity)
    {
        return new LoginFailureRecordResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserName = entity.UserName,
            IpAddress = entity.IpAddress,
            FailureCount = entity.FailureCount,
            LockedUntil = entity.LockedUntil,
            LastFailureAt = entity.LastFailureAt
        };
    }
}
