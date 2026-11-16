using System.Globalization;

// Outcome of one regression-gate check.
// Passed is true only when FailureReasons is empty, so a pass cannot carry a blocking reason.
public sealed class EvaluationRegressionResult
{
    private EvaluationRegressionResult(
        EvaluationRegressionPolicy policy,
        EvaluationRunComparison? comparison,
        IReadOnlyList<string> failureReasons)
    {
        Policy = policy;
        Comparison = comparison;
        FailureReasons = failureReasons;
    }

    public bool Passed => FailureReasons.Count == 0;

    public EvaluationRegressionPolicy Policy { get; }

    public EvaluationRunComparison? Comparison { get; }

    public IReadOnlyList<string> FailureReasons { get; }

    public string Report
    {
        get
        {
            var lines = new List<string>
            {
                $"Regression gate: {(Passed ? "PASSED" : "FAILED")}"
            };

            if (Comparison is null)
            {
                AppendReasons(lines);
                return string.Join('\n', lines);
            }

            lines.Add(DatasetLine());
            if (DatasetMatches())
            {
                lines.Add(
                    $"Failure rate: {FormatRate(Comparison.Previous.FailureRate)} -> {FormatRate(Comparison.Latest.FailureRate)} " +
                    $"(change {FormatPoints(Comparison.FailureRateChange)} percentage points, " +
                    $"limit {FormatPoints(Policy.MaximumFailureRateRegression)})");
                lines.Add(CategoryLine(
                    EvaluationFailureCategory.UngroundedClaim,
                    Policy.MaximumUngroundedClaimIncrease));
                lines.Add(CategoryLine(
                    EvaluationFailureCategory.MissingExpectedFact,
                    Policy.MaximumMissingExpectedFactIncrease));
            }

            AppendReasons(lines);
            return string.Join('\n', lines);
        }
    }

    public static EvaluationRegressionResult From(
        EvaluationRegressionPolicy policy,
        EvaluationRunComparison? comparison,
        IReadOnlyList<string> failureReasons)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(failureReasons);
        var copy = new string[failureReasons.Count];
        for (var index = 0; index < failureReasons.Count; index++)
        {
            copy[index] = failureReasons[index] ?? throw new ArgumentException(
                "Failure reasons cannot contain null values.",
                nameof(failureReasons));
        }

        return new EvaluationRegressionResult(policy, comparison, copy);
    }

    public override string ToString() => Report;

    private void AppendReasons(List<string> lines)
    {
        foreach (var reason in FailureReasons)
        {
            lines.Add($"- {reason}");
        }
    }

    private string DatasetLine()
    {
        var baseline = Comparison!.Previous;
        var candidate = Comparison.Latest;
        var line =
            $"Dataset: {baseline.TotalCases.ToString(CultureInfo.InvariantCulture)} -> " +
            $"{candidate.TotalCases.ToString(CultureInfo.InvariantCulture)} cases";
        if (baseline.CaseIds is null && candidate.CaseIds is null)
        {
            return line;
        }

        if (baseline.CaseIds is null || candidate.CaseIds is null)
        {
            return line + ", evaluation cases were not recorded on both runs";
        }

        return line + (SameCases(baseline.CaseIds, candidate.CaseIds)
            ? ", same evaluation cases"
            : ", different evaluation cases");
    }

    private bool DatasetMatches()
    {
        var baseline = Comparison!.Previous;
        var candidate = Comparison.Latest;
        if (baseline.TotalCases != candidate.TotalCases)
        {
            return false;
        }

        if (baseline.CaseIds is null && candidate.CaseIds is null)
        {
            return true;
        }

        if (baseline.CaseIds is null || candidate.CaseIds is null)
        {
            return false;
        }

        return SameCases(baseline.CaseIds, candidate.CaseIds);
    }

    private string CategoryLine(EvaluationFailureCategory category, int limit)
    {
        var comparison = Comparison!;
        var previous = comparison.PreviousCategoryCounts[category];
        var latest = comparison.LatestCategoryCounts[category];
        var change = latest - previous;
        return
            $"{category}: {previous.ToString(CultureInfo.InvariantCulture)} -> " +
            $"{latest.ToString(CultureInfo.InvariantCulture)} " +
            $"(change {change.ToString(CultureInfo.InvariantCulture)}, " +
            $"limit {limit.ToString(CultureInfo.InvariantCulture)})";
    }

    private static bool SameCases(IReadOnlyList<string> baseline, IReadOnlyList<string> candidate)
    {
        if (baseline.Count != candidate.Count)
        {
            return false;
        }

        var remaining = new HashSet<string>(baseline, StringComparer.Ordinal);
        foreach (var caseId in candidate)
        {
            if (!remaining.Remove(caseId))
            {
                return false;
            }
        }

        return remaining.Count == 0;
    }

    private static string FormatRate(double value) =>
        $"{FormatPoints(value)}%";

    private static string FormatPoints(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
