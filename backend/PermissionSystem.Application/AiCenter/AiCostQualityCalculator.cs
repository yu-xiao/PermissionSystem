using PermissionSystem.Domain.Enums;

namespace PermissionSystem.Application.AiCenter;

internal sealed record AiCostQualityObservation(Guid Id, Guid RunId, DateTimeOffset CreatedAt, AiInvocationStatus Status,
    int? Input, int? Output, int? Total, int? EstimatedInput, int? EstimatedOutput,
    decimal? InputPrice, decimal? OutputPrice, string? Currency, decimal? StoredCost);

internal sealed record AiCostQualityCalculation(AiCostQualityPopulation Population, AiCostQualityBasis Basis,
    IReadOnlyList<AiCostQualityIssue> Issues, AiCostQualityTokenComparison Input, AiCostQualityTokenComparison Output,
    AiCostQualityTotalTokens Total, IReadOnlyList<AiCostQualityCurrencySummary> Currencies,
    IReadOnlyList<AiCostQualityCurrencyDistribution> Distributions);

internal static class AiCostQualityCalculator
{
    private static readonly string[] IssueCodes = ["MissingInputTokens", "NegativeInputTokens", "MissingOutputTokens",
        "NegativeOutputTokens", "MissingTotalTokens", "NegativeTotalTokens", "MissingEstimatedInputTokens",
        "NegativeEstimatedInputTokens", "MissingEstimatedOutputTokens", "NegativeEstimatedOutputTokens",
        "MissingPrice", "NegativePrice", "MissingCurrency", "InvalidCurrency", "MissingStoredCost", "NegativeStoredCost", "ArithmeticOverflow"];

    public static AiCostQualityCalculation Calculate(IReadOnlyList<AiCostQualityObservation> rows, Action checkDeadline)
    {
        var issues = IssueCodes.ToDictionary(code => code, _ => 0L, StringComparer.Ordinal);
        var basis = new BasisCounter();
        var input = new TokenCounter(); var output = new TokenCounter();
        var currencies = new Dictionary<string, CostCounter>(StringComparer.Ordinal);
        long terminal = 0, unsettled = 0, unknown = 0, comparable = 0, consistent = 0, totalComparable = 0, totalDifferent = 0;
        foreach (var row in rows)
        {
            checkDeadline();
            if (row.Status is AiInvocationStatus.Pending or AiInvocationStatus.Running) { unsettled++; continue; }
            if (row.Status is not (AiInvocationStatus.Completed or AiInvocationStatus.Failed or AiInvocationStatus.Cancelled)) { unknown++; continue; }
            terminal++;
            DiagnoseTokens(row.Input, "InputTokens"); DiagnoseTokens(row.Output, "OutputTokens");
            DiagnoseTokens(row.Total, "TotalTokens"); DiagnoseTokens(row.EstimatedInput, "EstimatedInputTokens");
            DiagnoseTokens(row.EstimatedOutput, "EstimatedOutputTokens");
            if (row.InputPrice is null || row.OutputPrice is null) issues["MissingPrice"]++;
            if (row.InputPrice < 0 || row.OutputPrice < 0) issues["NegativePrice"]++;
            if (string.IsNullOrWhiteSpace(row.Currency)) issues["MissingCurrency"]++;
            else if (!ValidCurrency(row.Currency)) issues["InvalidCurrency"]++;
            if (row.StoredCost is null) issues["MissingStoredCost"]++;
            else if (row.StoredCost < 0) issues["NegativeStoredCost"]++;
            input.Add(row.Input, row.EstimatedInput); output.Add(row.Output, row.EstimatedOutput);
            if (row.Input is >= 0 && row.Output is >= 0 && row.Total is >= 0)
            {
                totalComparable++;
                if ((long)row.Input.Value + row.Output.Value != row.Total.Value) totalDifferent++;
            }
            var selectedInput = Select(row.Input, row.EstimatedInput); var selectedOutput = Select(row.Output, row.EstimatedOutput);
            var kind = selectedInput is null || selectedOutput is null ? TokenBasis.Unusable :
                row.Input is >= 0 && row.Output is >= 0 ? TokenBasis.Recorded :
                row.Input is >= 0 || row.Output is >= 0 ? TokenBasis.Mixed : TokenBasis.Estimated;
            basis.Add(kind);
            decimal? recomputed = null;
            if (selectedInput.HasValue && selectedOutput.HasValue && row.InputPrice is >= 0 && row.OutputPrice is >= 0 && ValidCurrency(row.Currency))
            {
                try
                {
                    recomputed = decimal.Round(checked(selectedInput.Value * row.InputPrice.Value / 1_000_000m +
                        selectedOutput.Value * row.OutputPrice.Value / 1_000_000m), 6, MidpointRounding.AwayFromZero);
                }
                catch (OverflowException) { issues["ArithmeticOverflow"]++; }
            }
            var canCompare = recomputed.HasValue && row.StoredCost is >= 0;
            if (canCompare) { comparable++; if (row.StoredCost == recomputed) consistent++; }
            if (ValidCurrency(row.Currency))
            {
                if (!currencies.TryGetValue(row.Currency!, out var cost)) currencies.Add(row.Currency!, cost = new());
                cost.Add(kind, canCompare ? row.StoredCost : null, canCompare ? recomputed : null);
            }
        }
        var distributions = currencies.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value.Distribution(p.Key)).ToArray();
        return new(new(rows.Count, terminal, unsettled, unknown, comparable, consistent, comparable - consistent, terminal - comparable),
            basis.Result(), issues.Select(p => new AiCostQualityIssue(p.Key, p.Value)).ToArray(), input.Result(), output.Result(),
            new(totalComparable, totalDifferent), distributions.Select(d => d.Summary).ToArray(), distributions);

        void DiagnoseTokens(int? value, string suffix)
        {
            if (value is null) issues["Missing" + suffix]++;
            else if (value < 0) issues["Negative" + suffix]++;
        }
    }

    private static int? Select(int? recorded, int? estimated) => recorded is >= 0 ? recorded : estimated is >= 0 ? estimated : null;
    private static bool ValidCurrency(string? currency) => currency is { Length: 3 } && currency.All(c => c is >= 'A' and <= 'Z');
    private enum TokenBasis { Recorded, Mixed, Estimated, Unusable }
    private sealed class BasisCounter
    {
        private readonly long[] _counts = new long[4];
        public void Add(TokenBasis basis) => _counts[(int)basis]++;
        public AiCostQualityBasis Result() => new(_counts[0], _counts[1], _counts[2], _counts[3]);
    }
    private sealed class TokenCounter
    {
        private long _samples, _recorded, _estimated, _difference, _absolute, _zero, _above;
        public void Add(int? recorded, int? estimated)
        {
            if (recorded is not >= 0 || estimated is not >= 0) return;
            if (estimated == 0) { _zero++; return; }
            checked
            {
                _samples++; _recorded += recorded.Value; _estimated += estimated.Value;
                var difference = (long)recorded.Value - estimated.Value;
                _difference += difference; _absolute += Math.Abs(difference);
                if (difference > 0) _above++;
            }
        }
        public AiCostQualityTokenComparison Result() => new(_samples, _recorded, _estimated, _difference, _absolute,
            _samples == 0 ? null : decimal.Round(checked(_recorded * 100m / _estimated), 2, MidpointRounding.AwayFromZero), _zero, _above);
    }
    private sealed class CostCounter
    {
        private readonly BasisCounter _basis = new();
        private long _terminal, _samples, _consistent, _above, _below;
        private decimal _stored, _recomputed, _difference, _absolute;
        public void Add(TokenBasis basis, decimal? stored, decimal? recomputed)
        {
            _terminal++; _basis.Add(basis);
            if (!stored.HasValue || !recomputed.HasValue) return;
            _samples++; if (stored == recomputed) _consistent++;
            else if (stored > recomputed) _above++;
            else _below++;
            checked
            {
                _stored += stored.Value; _recomputed += recomputed.Value;
                var difference = stored.Value - recomputed.Value;
                _difference += difference; _absolute += Math.Abs(difference);
            }
        }
        public AiCostQualityCurrencySummary Result(string currency) => new(currency, _terminal, _basis.Result(),
            _samples, _consistent, _samples - _consistent, _terminal - _samples,
            _samples == 0 ? null : _stored, _samples == 0 ? null : _recomputed,
            _samples == 0 ? null : _difference, _samples == 0 ? null : _absolute);
        public AiCostQualityCurrencyDistribution Distribution(string currency) => new(Result(currency), _above, _below);
    }
}
