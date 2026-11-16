using System.Globalization;

// Compares a candidate evaluation run with a baseline and applies EvaluationRegressionPolicy.
// Regression is the candidate failure rate minus the baseline failure rate, in percentage points.
// The result passes only when the dataset matches and every policy rule holds.
// A different case count, or a different case-id set, is not the same fixed dataset.
// The gate stops on that mismatch. A failure-rate or category change across different cases
// is not a regression result, because the runs did not score the same cases.
public static class EvaluationRegressionGate
{
    public static EvaluationRegressionResult Evaluate(
        EvaluationSnapshot? baseline,
        EvaluationSnapshot? candidate,
        EvaluationRegressionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var reasons = new List<string>();
        if (!policy.AllowsRuns(baseline, candidate))
        {
            if (baseline is null)
            {
                reasons.Add("The baseline snapshot is missing.");
            }

            if (candidate is null)
            {
                reasons.Add("The candidate snapshot is missing.");
            }

            return EvaluationRegressionResult.From(policy, null, reasons);
        }

        if (baseline is null || candidate is null)
        {
            return EvaluationRegressionResult.From(policy, null, reasons);
        }

        var comparison = EvaluationRunComparer.Compare(baseline, candidate);
        AddDatasetReasons(reasons, comparison);
        if (reasons.Count > 0)
        {
            return EvaluationRegressionResult.From(policy, comparison, reasons);
        }

        if (!policy.AllowsFailureRate(comparison))
        {
            reasons.Add(RateReason(comparison, policy));
        }

        if (!policy.AllowsUngroundedClaims(comparison))
        {
            reasons.Add(IncreaseReason(
                comparison,
                EvaluationFailureCategory.UngroundedClaim,
                policy.MaximumUngroundedClaimIncrease,
                "Ungrounded-claim failures"));
        }

        if (!policy.AllowsMissingExpectedFacts(comparison))
        {
            reasons.Add(IncreaseReason(
                comparison,
                EvaluationFailureCategory.MissingExpectedFact,
                policy.MaximumMissingExpectedFactIncrease,
                "Missing expected facts"));
        }

        return EvaluationRegressionResult.From(policy, comparison, reasons);
    }

    private static void AddDatasetReasons(List<string> reasons, EvaluationRunComparison comparison)
    {
        var baseline = comparison.Previous;
        var candidate = comparison.Latest;
        if (baseline.TotalCases != candidate.TotalCases)
        {
            reasons.Add(
                "The dataset size changed from " +
                $"{baseline.TotalCases.ToString(CultureInfo.InvariantCulture)} to " +
                $"{candidate.TotalCases.ToString(CultureInfo.InvariantCulture)} cases. " +
                "These runs are not the same fixed evaluation dataset.");
        }

        if (baseline.CaseIds is null && candidate.CaseIds is null)
        {
            return;
        }

        if (baseline.CaseIds is null || candidate.CaseIds is null)
        {
            var side = baseline.CaseIds is null ? "baseline" : "candidate";
            reasons.Add(
                $"The {side} snapshot does not record its evaluation cases. " +
                "These runs cannot be treated as the same fixed evaluation dataset.");
            return;
        }

        if (!SameCases(baseline.CaseIds, candidate.CaseIds))
        {
            reasons.Add(
                $"The evaluation cases changed from {FormatCases(baseline.CaseIds)} to {FormatCases(candidate.CaseIds)}. " +
                "These runs are not the same fixed evaluation dataset.");
        }
    }

    private static string RateReason(EvaluationRunComparison comparison, EvaluationRegressionPolicy policy)
    {
        return
            $"The failure rate regressed by {FormatPoints(comparison.FailureRateChange)} percentage points, " +
            $"from {FormatRate(comparison.Previous.FailureRate)} to {FormatRate(comparison.Latest.FailureRate)}. " +
            $"The limit is {FormatPoints(policy.MaximumFailureRateRegression)} percentage points.";
    }

    private static string IncreaseReason(
        EvaluationRunComparison comparison,
        EvaluationFailureCategory category,
        int limit,
        string description)
    {
        var previous = comparison.PreviousCategoryCounts[category];
        var latest = comparison.LatestCategoryCounts[category];
        var change = latest - previous;
        return
            $"{description} increased by {change.ToString(CultureInfo.InvariantCulture)}, " +
            $"from {previous.ToString(CultureInfo.InvariantCulture)} to {latest.ToString(CultureInfo.InvariantCulture)}. " +
            $"The limit is {limit.ToString(CultureInfo.InvariantCulture)}.";
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

    private static string FormatCases(IReadOnlyList<string> caseIds)
    {
        if (caseIds.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", caseIds.Select(caseId => $"'{caseId}'"));
    }

    private static string FormatRate(double value) =>
        $"{FormatPoints(value)}%";

    private static string FormatPoints(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
