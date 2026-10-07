namespace PermissionSystem.Application.AiCenter;

internal sealed record AiCostQualityTrendCalculation(AiCostQualityCalculation Window, IReadOnlyList<AiCostQualityDay> Daily);

internal static class AiCostQualityTrendCalculator
{
    public static AiCostQualityTrendCalculation Calculate(IReadOnlyList<AiCostQualityObservation> rows,
        DateTimeOffset from, DateTimeOffset to, Action checkDeadline)
    {
        var window = AiCostQualityCalculator.Calculate(rows, checkDeadline);
        var byDay = new Dictionary<DateOnly, List<AiCostQualityObservation>>();
        foreach (var row in rows)
        {
            checkDeadline();
            var date = DateOnly.FromDateTime(row.CreatedAt.UtcDateTime);
            if (!byDay.TryGetValue(date, out var group)) byDay.Add(date, group = []);
            group.Add(row);
        }
        var daily = new List<AiCostQualityDay>();
        var utcFrom = from.ToUniversalTime(); var utcTo = to.ToUniversalTime();
        var day = DateOnly.FromDateTime(utcFrom.UtcDateTime);
        while (true)
        {
            checkDeadline();
            if (daily.Count >= AiCostQualityTrendContract.MaxDays) throw new OverflowException();
            var dayFrom = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            var dayTo = day == DateOnly.MaxValue ? DateTimeOffset.MaxValue : dayFrom.AddDays(1);
            var bucketFrom = utcFrom > dayFrom ? utcFrom : dayFrom;
            var bucketTo = utcTo < dayTo ? utcTo : dayTo;
            var result = AiCostQualityCalculator.Calculate(byDay.TryGetValue(day, out var group) ? group : [], checkDeadline);
            daily.Add(new(day, bucketFrom, bucketTo, bucketFrom != dayFrom || bucketTo != dayTo,
                result.Population, result.Basis, result.Issues, result.Input, result.Output, result.Total));
            if (bucketTo == utcTo) break;
            day = day.AddDays(1);
        }
        checkDeadline();
        return new(window, daily);
    }
}
