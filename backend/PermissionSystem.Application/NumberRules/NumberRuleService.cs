using PermissionSystem.Application.Abstractions;
using PermissionSystem.Domain.Entities;
using PermissionSystem.Domain.Enums;
using PermissionSystem.Domain.Repositories;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;
using PermissionSystem.Shared.Results;

namespace PermissionSystem.Application.NumberRules;

public sealed class NumberRuleService : INumberRuleService
{
    private readonly IRepository<NumberRule> _ruleRepository;
    private readonly IRepository<NumberRuleSegment> _segmentRepository;
    private readonly IRepository<NumberSequence> _sequenceRepository;
    private readonly ITenantWriteResolver _tenantWriteResolver;
    private readonly IUnitOfWork _unitOfWork;

    public NumberRuleService(
        IRepository<NumberRule> ruleRepository,
        IRepository<NumberRuleSegment> segmentRepository,
        IRepository<NumberSequence> sequenceRepository,
        ITenantWriteResolver tenantWriteResolver,
        IUnitOfWork unitOfWork)
    {
        _ruleRepository = ruleRepository;
        _segmentRepository = segmentRepository;
        _sequenceRepository = sequenceRepository;
        _tenantWriteResolver = tenantWriteResolver;
        _unitOfWork = unitOfWork;
    }

    public Task<PagedResult<NumberRuleResponse>> GetPagedAsync(
        NumberRuleQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _ruleRepository.Query();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(entity =>
                entity.RuleCode.Contains(keyword) ||
                entity.RuleName.Contains(keyword) ||
                entity.BusinessType.Contains(keyword) ||
                entity.Prefix.Contains(keyword) ||
                (entity.Remark != null && entity.Remark.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessType))
        {
            var businessType = request.BusinessType.Trim();
            query = query.Where(entity => entity.BusinessType == businessType);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(entity => entity.IsEnabled == request.IsEnabled.Value);
        }

        var totalCount = query.LongCount();
        var items = query
            .OrderBy(entity => entity.BusinessType)
            .ThenBy(entity => entity.RuleCode)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToList()
            .Select(ToResponse)
            .ToList();

        return Task.FromResult(PagedResult<NumberRuleResponse>.Create(
            items,
            request.PageIndex,
            request.PageSize,
            totalCount));
    }

    public async Task<NumberRuleResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ToResponse(await GetRuleOrThrowAsync(id, cancellationToken));
    }

    public async Task<NumberRuleResponse> CreateAsync(
        CreateOrUpdateNumberRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantWriteResolver.ResolveTenantId();
        var ruleCode = NormalizeRequired(request.RuleCode, "Rule code is required.");
        ValidateRequest(request);

        if (_ruleRepository.Query().Any(entity => entity.RuleCode == ruleCode))
        {
            throw new BusinessException(ErrorCode.Conflict, "Rule code already exists.");
        }

        var rule = new NumberRule
        {
            TenantId = tenantId,
            RuleCode = ruleCode,
            RuleName = NormalizeRequired(request.RuleName, "Rule name is required."),
            BusinessType = NormalizeRequired(request.BusinessType, "Business type is required."),
            Prefix = NormalizeOptional(request.Prefix) ?? string.Empty,
            DateFormat = NormalizeRequired(request.DateFormat, "Date format is required."),
            SequenceLength = request.SequenceLength,
            ResetCycle = ParseResetCycle(request.ResetCycle),
            Separator = NormalizeOptional(request.Separator) ?? string.Empty,
            IsEnabled = request.IsEnabled,
            Remark = NormalizeOptional(request.Remark)
        };

        await _ruleRepository.AddAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await SyncDefaultSegmentsAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(rule);
    }

    public async Task<NumberRuleResponse> UpdateAsync(
        Guid id,
        CreateOrUpdateNumberRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var ruleCode = NormalizeRequired(request.RuleCode, "Rule code is required.");
        ValidateRequest(request);

        var rule = await GetRuleOrThrowAsync(id, cancellationToken);
        if (_ruleRepository.Query().Any(entity => entity.Id != id && entity.RuleCode == ruleCode))
        {
            throw new BusinessException(ErrorCode.Conflict, "Rule code already exists.");
        }

        rule.RuleCode = ruleCode;
        rule.RuleName = NormalizeRequired(request.RuleName, "Rule name is required.");
        rule.BusinessType = NormalizeRequired(request.BusinessType, "Business type is required.");
        rule.Prefix = NormalizeOptional(request.Prefix) ?? string.Empty;
        rule.DateFormat = NormalizeRequired(request.DateFormat, "Date format is required.");
        rule.SequenceLength = request.SequenceLength;
        rule.ResetCycle = ParseResetCycle(request.ResetCycle);
        rule.Separator = NormalizeOptional(request.Separator) ?? string.Empty;
        rule.IsEnabled = request.IsEnabled;
        rule.Remark = NormalizeOptional(request.Remark);

        _ruleRepository.Update(rule);
        await SyncDefaultSegmentsAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(rule);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await GetRuleOrThrowAsync(id, cancellationToken);
        var segments = _segmentRepository.Query()
            .Where(entity => entity.RuleId == id)
            .ToList();

        foreach (var segment in segments)
        {
            _segmentRepository.Remove(segment);
        }

        _ruleRepository.Remove(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task EnableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await GetRuleOrThrowAsync(id, cancellationToken);
        rule.IsEnabled = true;
        _ruleRepository.Update(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await GetRuleOrThrowAsync(id, cancellationToken);
        rule.IsEnabled = false;
        _ruleRepository.Update(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task<NumberRulePreviewResponse> PreviewAsync(
        CreateOrUpdateNumberRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var number = NumberRuleFormatter.BuildNumber(
            NormalizeOptional(request.Prefix) ?? string.Empty,
            NormalizeRequired(request.DateFormat, "Date format is required."),
            request.SequenceLength,
            NormalizeOptional(request.Separator) ?? string.Empty,
            1,
            DateTimeOffset.UtcNow,
            new Dictionary<string, object>());

        return Task.FromResult(new NumberRulePreviewResponse
        {
            Number = number,
            Pattern = NumberRuleFormatter.BuildPattern(request)
        });
    }

    public async Task ResetSequenceAsync(string ruleCode, CancellationToken cancellationToken = default)
    {
        var normalizedRuleCode = NormalizeRequired(ruleCode, "Rule code is required.");
        if (!_ruleRepository.Query().Any(entity => entity.RuleCode == normalizedRuleCode))
        {
            throw new BusinessException(ErrorCode.NotFound, "Number rule was not found.");
        }

        var sequences = _sequenceRepository.Query()
            .Where(entity => entity.RuleCode == normalizedRuleCode)
            .ToList();

        foreach (var sequence in sequences)
        {
            sequence.CurrentValue = 0;
            sequence.LastGeneratedAt = null;
            _sequenceRepository.Update(sequence);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncDefaultSegmentsAsync(NumberRule rule, CancellationToken cancellationToken)
    {
        var existingSegments = _segmentRepository.Query()
            .Where(entity => entity.RuleId == rule.Id)
            .ToList();

        foreach (var segment in existingSegments)
        {
            _segmentRepository.Remove(segment);
        }

        var sort = 1;
        if (!string.IsNullOrWhiteSpace(rule.Prefix))
        {
            await _segmentRepository.AddAsync(
                new NumberRuleSegment
                {
                    TenantId = rule.TenantId,
                    RuleId = rule.Id,
                    SegmentType = NumberRuleSegmentType.FixedText,
                    SegmentValue = rule.Prefix,
                    Sort = sort++
                },
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(rule.DateFormat))
        {
            await _segmentRepository.AddAsync(
                new NumberRuleSegment
                {
                    TenantId = rule.TenantId,
                    RuleId = rule.Id,
                    SegmentType = NumberRuleSegmentType.Date,
                    SegmentValue = rule.DateFormat,
                    Sort = sort++
                },
                cancellationToken);
        }

        await _segmentRepository.AddAsync(
            new NumberRuleSegment
            {
                TenantId = rule.TenantId,
                RuleId = rule.Id,
                SegmentType = NumberRuleSegmentType.Sequence,
                SegmentValue = rule.SequenceLength.ToString(),
                Sort = sort
            },
            cancellationToken);
    }

    private async Task<NumberRule> GetRuleOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _ruleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "Number rule was not found.");
    }

    private static NumberRuleResponse ToResponse(NumberRule rule)
    {
        return new NumberRuleResponse
        {
            Id = rule.Id,
            TenantId = rule.TenantId,
            RuleCode = rule.RuleCode,
            RuleName = rule.RuleName,
            BusinessType = rule.BusinessType,
            Prefix = rule.Prefix,
            DateFormat = rule.DateFormat,
            SequenceLength = rule.SequenceLength,
            ResetCycle = rule.ResetCycle.ToString(),
            Separator = rule.Separator,
            IsEnabled = rule.IsEnabled,
            Remark = rule.Remark,
            CreatedAt = rule.CreatedAt
        };
    }

    private static void ValidateRequest(CreateOrUpdateNumberRuleRequest request)
    {
        NormalizeRequired(request.RuleCode, "Rule code is required.");
        NormalizeRequired(request.RuleName, "Rule name is required.");
        NormalizeRequired(request.BusinessType, "Business type is required.");
        NormalizeRequired(request.DateFormat, "Date format is required.");
        _ = ParseResetCycle(request.ResetCycle);

        if (request.SequenceLength is < 1 or > 18)
        {
            throw new BusinessException(ErrorCode.ValidationFailed, "Sequence length must be between 1 and 18.");
        }
    }

    private static NumberRuleResetCycle ParseResetCycle(string? resetCycle)
    {
        if (Enum.TryParse<NumberRuleResetCycle>(resetCycle, ignoreCase: true, out var value))
        {
            return value;
        }

        throw new BusinessException(ErrorCode.ValidationFailed, "Reset cycle is invalid.");
    }

    private static string NormalizeRequired(string? value, string message)
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
}
