using System.Globalization;
using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.NumberRules;

public sealed class NumberGenerator : INumberGenerator
{
    private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockWaitTime = TimeSpan.FromSeconds(10);

    private readonly IRepository<NumberRule> _ruleRepository;
    private readonly IRepository<NumberRuleSegment> _segmentRepository;
    private readonly IRepository<NumberSequence> _sequenceRepository;
    private readonly IDistributedLock _distributedLock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;

    public NumberGenerator(
        IRepository<NumberRule> ruleRepository,
        IRepository<NumberRuleSegment> segmentRepository,
        IRepository<NumberSequence> sequenceRepository,
        IDistributedLock distributedLock,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext)
    {
        _ruleRepository = ruleRepository;
        _segmentRepository = segmentRepository;
        _sequenceRepository = sequenceRepository;
        _distributedLock = distributedLock;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
    }

    public Task<string> GenerateAsync(string ruleCode, CancellationToken cancellationToken = default)
    {
        return GenerateAsync(ruleCode, [], cancellationToken);
    }

    public async Task<string> GenerateAsync(
        string ruleCode,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Tenant context is required.");
        }

        var normalizedRuleCode = NormalizeRequired(ruleCode, "Rule code is required.");
        var rule = _ruleRepository.Query()
            .FirstOrDefault(entity => entity.RuleCode == normalizedRuleCode)
            ?? throw new BusinessException(ErrorCode.NotFound, "Number rule was not found.");

        if (!rule.IsEnabled)
        {
            throw new BusinessException(ErrorCode.Conflict, "Number rule is disabled.");
        }

        var now = DateTimeOffset.UtcNow;
        var sequenceKey = BuildSequenceKey(rule, now);
        var lockKey = $"number-rule:{_tenantContext.TenantId.Value:N}:{rule.RuleCode}:{sequenceKey}";

        return await _distributedLock.ExecuteWithLockAsync(
            lockKey,
            async token =>
            {
                string number = string.Empty;

                await _unitOfWork.ExecuteInTransactionAsync(
                    async transactionToken =>
                    {
                        var sequence = _sequenceRepository.Query()
                            .FirstOrDefault(entity =>
                                entity.RuleCode == rule.RuleCode &&
                                entity.SequenceKey == sequenceKey);

                        if (sequence is null)
                        {
                            sequence = new NumberSequence
                            {
                                TenantId = rule.TenantId,
                                RuleCode = rule.RuleCode,
                                SequenceKey = sequenceKey,
                                CurrentValue = 0
                            };
                            await _sequenceRepository.AddAsync(sequence, transactionToken);
                        }

                        sequence.CurrentValue += 1;
                        sequence.LastGeneratedAt = now;

                        number = BuildNumber(rule, sequence.CurrentValue, now, variables);
                        await _unitOfWork.SaveChangesAsync(transactionToken);
                    },
                    token);

                return number;
            },
            LockExpiry,
            LockWaitTime,
            cancellationToken);
    }

    public static string BuildSequenceKey(NumberRule rule, DateTimeOffset now)
    {
        var period = rule.ResetCycle switch
        {
            NumberRuleResetCycle.Daily => now.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            NumberRuleResetCycle.Monthly => now.ToString("yyyyMM", CultureInfo.InvariantCulture),
            NumberRuleResetCycle.Yearly => now.ToString("yyyy", CultureInfo.InvariantCulture),
            _ => "ALL"
        };

        return $"{rule.RuleCode}:{period}";
    }

    private string BuildNumber(
        NumberRule rule,
        long sequenceValue,
        DateTimeOffset now,
        IReadOnlyDictionary<string, object> variables)
    {
        var segments = _segmentRepository.Query()
            .Where(entity => entity.RuleId == rule.Id)
            .OrderBy(entity => entity.Sort)
            .ToList();

        if (segments.Count == 0)
        {
            return NumberRuleFormatter.BuildNumber(
                rule.Prefix,
                rule.DateFormat,
                rule.SequenceLength,
                rule.Separator,
                sequenceValue,
                now,
                variables);
        }

        var values = segments
            .Select(segment => ResolveSegmentValue(segment, rule, sequenceValue, now, variables))
            .Where(value => !string.IsNullOrEmpty(value))
            .ToList();

        return string.Join(rule.Separator, values);
    }

    private static string ResolveSegmentValue(
        NumberRuleSegment segment,
        NumberRule rule,
        long sequenceValue,
        DateTimeOffset now,
        IReadOnlyDictionary<string, object> variables)
    {
        return segment.SegmentType switch
        {
            NumberRuleSegmentType.FixedText => segment.SegmentValue,
            NumberRuleSegmentType.Date => now.ToString(
                string.IsNullOrWhiteSpace(segment.SegmentValue) ? rule.DateFormat : segment.SegmentValue,
                CultureInfo.InvariantCulture),
            NumberRuleSegmentType.Sequence => sequenceValue.ToString(
                new string('0', ResolveSequenceLength(segment.SegmentValue, rule.SequenceLength)),
                CultureInfo.InvariantCulture),
            NumberRuleSegmentType.TenantCode => ResolveVariable(variables, "TenantCode"),
            NumberRuleSegmentType.DepartmentCode => ResolveVariable(variables, "DepartmentCode"),
            NumberRuleSegmentType.Custom => ResolveVariable(variables, segment.SegmentValue),
            _ => string.Empty
        };
    }

    private static int ResolveSequenceLength(string value, int defaultLength)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
            ? Math.Clamp(length, 1, 18)
            : defaultLength;
    }

    private static string ResolveVariable(IReadOnlyDictionary<string, object> variables, string key)
    {
        return variables.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
    }

    private static string NormalizeRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessException(ErrorCode.ValidationFailed, message);
        }

        return value.Trim();
    }
}

public static class NumberRuleFormatter
{
    public static string BuildNumber(
        string prefix,
        string dateFormat,
        int sequenceLength,
        string separator,
        long sequenceValue,
        DateTimeOffset now,
        IReadOnlyDictionary<string, object> variables)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            parts.Add(prefix.Trim());
        }

        if (!string.IsNullOrWhiteSpace(dateFormat))
        {
            parts.Add(now.ToString(dateFormat.Trim(), CultureInfo.InvariantCulture));
        }

        parts.Add(sequenceValue.ToString(new string('0', Math.Clamp(sequenceLength, 1, 18)), CultureInfo.InvariantCulture));

        return string.Join(separator ?? string.Empty, parts);
    }

    public static string BuildPattern(CreateOrUpdateNumberRuleRequest request)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Prefix))
        {
            parts.Add(request.Prefix.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.DateFormat))
        {
            parts.Add($"{{{request.DateFormat.Trim()}}}");
        }

        parts.Add($"{{{new string('0', Math.Clamp(request.SequenceLength, 1, 18))}}}");

        return string.Join(request.Separator ?? string.Empty, parts);
    }
}
