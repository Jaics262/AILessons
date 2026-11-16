// Learning defaults for the regression gate, not a universal AI quality standard.
// The gate reads these limits. A different limit is a different policy, not a different gate.
// Baseline is the previous snapshot. Candidate is the latest snapshot.
// The numeric rules read EvaluationRunComparison from EvaluationRunComparer.Compare(baseline, candidate).
// Failure-rate regression is that comparison's FailureRateChange: latest rate minus previous rate,
// in percentage points. A positive change is worse. The default allows a worsening of 5 points.
// A change equal to the limit stays inside the policy.
// Ungrounded-claim and missing-fact limits are the allowed increase in those category counts.
// Zero means that count must not rise. One means one additional occurrence is still allowed.
// When FailWhenBaselineOrCandidateIsMissing is true, a missing baseline or candidate fails the gate.
// An empty run is still a run. Its failure rate stays 0, and the comparison can use it.
// A different dataset is not one of these limits. The gate rejects that pair on its own.
public sealed record EvaluationRegressionPolicy(
    double MaximumFailureRateRegression,
    int MaximumUngroundedClaimIncrease,
    int MaximumMissingExpectedFactIncrease,
    bool FailWhenBaselineOrCandidateIsMissing = true)
{
    public const double DefaultMaximumFailureRateRegression = 5d;

    public const int DefaultMaximumUngroundedClaimIncrease = 0;

    public const int DefaultMaximumMissingExpectedFactIncrease = 1;

    public const bool DefaultFailWhenBaselineOrCandidateIsMissing = true;

    public static EvaluationRegressionPolicy Default { get; } = new(
        DefaultMaximumFailureRateRegression,
        DefaultMaximumUngroundedClaimIncrease,
        DefaultMaximumMissingExpectedFactIncrease,
        DefaultFailWhenBaselineOrCandidateIsMissing);

    public double MaximumFailureRateRegression { get; init; } =
        RequireFinite(MaximumFailureRateRegression, nameof(MaximumFailureRateRegression));

    public bool Allows(EvaluationRunComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        return AllowsFailureRate(comparison)
            && AllowsUngroundedClaims(comparison)
            && AllowsMissingExpectedFacts(comparison);
    }

    public bool AllowsFailureRate(EvaluationRunComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        return comparison.FailureRateChange <= MaximumFailureRateRegression;
    }

    public bool AllowsUngroundedClaims(EvaluationRunComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        return Change(comparison, EvaluationFailureCategory.UngroundedClaim)
            <= MaximumUngroundedClaimIncrease;
    }

    public bool AllowsMissingExpectedFacts(EvaluationRunComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        return Change(comparison, EvaluationFailureCategory.MissingExpectedFact)
            <= MaximumMissingExpectedFactIncrease;
    }

    public bool AllowsRuns(EvaluationSnapshot? baseline, EvaluationSnapshot? candidate)
    {
        if (!FailWhenBaselineOrCandidateIsMissing)
        {
            return true;
        }

        return baseline is not null && candidate is not null;
    }

    private static int Change(EvaluationRunComparison comparison, EvaluationFailureCategory category) =>
        comparison.CategoryCountChanges[category];

    private static double RequireFinite(double value, string paramName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                "A failure-rate regression limit must be a finite number of percentage points.");
        }

        return value;
    }
}
