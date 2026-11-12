namespace AIPromptLab.Tests;

public sealed class EvaluationRegressionPolicyTests
{
    private static DateTimeOffset EvaluatedAt { get; } =
        new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_uses_the_learning_rules()
    {
        var policy = EvaluationRegressionPolicy.Default;

        Assert.Equal(5d, policy.MaximumFailureRateRegression);
        Assert.Equal(0, policy.MaximumUngroundedClaimIncrease);
        Assert.Equal(1, policy.MaximumMissingExpectedFactIncrease);
        Assert.True(policy.FailWhenBaselineOrCandidateIsMissing);
        Assert.Equal(
            EvaluationRegressionPolicy.DefaultMaximumFailureRateRegression,
            policy.MaximumFailureRateRegression);
        Assert.Equal(
            EvaluationRegressionPolicy.DefaultMaximumUngroundedClaimIncrease,
            policy.MaximumUngroundedClaimIncrease);
        Assert.Equal(
            EvaluationRegressionPolicy.DefaultMaximumMissingExpectedFactIncrease,
            policy.MaximumMissingExpectedFactIncrease);
        Assert.Equal(
            EvaluationRegressionPolicy.DefaultFailWhenBaselineOrCandidateIsMissing,
            policy.FailWhenBaselineOrCandidateIsMissing);
    }

    [Fact]
    public void Constructor_accepts_a_custom_policy()
    {
        var policy = new EvaluationRegressionPolicy(10d, 2, 3, false);

        Assert.Equal(10d, policy.MaximumFailureRateRegression);
        Assert.Equal(2, policy.MaximumUngroundedClaimIncrease);
        Assert.Equal(3, policy.MaximumMissingExpectedFactIncrease);
        Assert.False(policy.FailWhenBaselineOrCandidateIsMissing);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(5d)]
    [InlineData(-1d)]
    [InlineData(100d)]
    public void Constructor_accepts_a_finite_failure_rate_limit(double maximumFailureRateRegression)
    {
        var policy = new EvaluationRegressionPolicy(maximumFailureRateRegression, 0, 1);

        Assert.Equal(maximumFailureRateRegression, policy.MaximumFailureRateRegression);
        Assert.True(policy.FailWhenBaselineOrCandidateIsMissing);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_rejects_a_non_finite_failure_rate_limit(double maximumFailureRateRegression)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EvaluationRegressionPolicy(maximumFailureRateRegression, 0, 1));

        Assert.Equal("MaximumFailureRateRegression", exception.ParamName);
    }

    [Fact]
    public void Default_allows_a_worsening_at_the_stated_limits()
    {
        var baseline = Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1);
        var candidate = Snapshot("candidate", 20, 3, ungrounded: 1, missing: 2);
        var comparison = EvaluationRunComparer.Compare(baseline, candidate);
        var policy = EvaluationRegressionPolicy.Default;

        Assert.Equal(5d, comparison.FailureRateChange);
        Assert.Equal(0, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(1, comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.True(policy.AllowsRuns(baseline, candidate));
        Assert.True(policy.AllowsFailureRate(comparison));
        Assert.True(policy.AllowsUngroundedClaims(comparison));
        Assert.True(policy.AllowsMissingExpectedFacts(comparison));
        Assert.True(policy.Allows(comparison));
    }

    [Fact]
    public void Default_rejects_a_failure_rate_more_than_five_points_worse()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1),
            Snapshot("candidate", 20, 4, ungrounded: 1, missing: 1));
        var policy = EvaluationRegressionPolicy.Default;

        Assert.Equal(10d, comparison.FailureRateChange);
        Assert.False(policy.AllowsFailureRate(comparison));
        Assert.True(policy.AllowsUngroundedClaims(comparison));
        Assert.True(policy.AllowsMissingExpectedFacts(comparison));
        Assert.False(policy.Allows(comparison));
    }

    [Fact]
    public void Default_rejects_an_increase_in_ungrounded_claims()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1),
            Snapshot("candidate", 20, 2, ungrounded: 2, missing: 1));
        var policy = EvaluationRegressionPolicy.Default;

        Assert.Equal(0d, comparison.FailureRateChange);
        Assert.Equal(1, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.False(policy.AllowsUngroundedClaims(comparison));
        Assert.True(policy.AllowsFailureRate(comparison));
        Assert.True(policy.AllowsMissingExpectedFacts(comparison));
        Assert.False(policy.Allows(comparison));
    }

    [Fact]
    public void Default_allows_one_additional_missing_fact_and_rejects_two()
    {
        var baseline = Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1);
        var oneMore = EvaluationRunComparer.Compare(
            baseline,
            Snapshot("one-more", 20, 2, ungrounded: 1, missing: 2));
        var twoMore = EvaluationRunComparer.Compare(
            baseline,
            Snapshot("two-more", 20, 2, ungrounded: 1, missing: 3));
        var policy = EvaluationRegressionPolicy.Default;

        Assert.Equal(1, oneMore.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.True(policy.AllowsMissingExpectedFacts(oneMore));
        Assert.True(policy.Allows(oneMore));

        Assert.Equal(2, twoMore.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.False(policy.AllowsMissingExpectedFacts(twoMore));
        Assert.True(policy.AllowsFailureRate(twoMore));
        Assert.True(policy.AllowsUngroundedClaims(twoMore));
        Assert.False(policy.Allows(twoMore));
    }

    [Fact]
    public void Default_allows_an_improvement()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1),
            Snapshot("candidate", 20, 1, ungrounded: 0, missing: 0));
        var policy = EvaluationRegressionPolicy.Default;

        Assert.Equal(-5d, comparison.FailureRateChange);
        Assert.Equal(EvaluationRateDirection.Improved, comparison.Direction);
        Assert.Equal(-1, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(-1, comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.True(policy.Allows(comparison));
    }

    [Fact]
    public void Failure_rate_rule_uses_the_comparison_rate_when_the_dataset_size_changes()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("baseline", 20, 0),
            Snapshot("candidate", 10, 1, ungrounded: 1));
        var policy = EvaluationRegressionPolicy.Default;

        Assert.True(comparison.DatasetSizeChanged);
        Assert.Equal(10d, comparison.FailureRateChange);
        Assert.Equal(1, comparison.FailedCaseChange);
        Assert.False(policy.AllowsFailureRate(comparison));
    }

    [Fact]
    public void A_looser_policy_allows_the_comparison_the_default_rejects()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1),
            Snapshot("candidate", 20, 4, ungrounded: 2, missing: 3));
        var policy = EvaluationRegressionPolicy.Default with
        {
            MaximumFailureRateRegression = 10d,
            MaximumUngroundedClaimIncrease = 1,
            MaximumMissingExpectedFactIncrease = 2
        };

        Assert.Equal(10d, comparison.FailureRateChange);
        Assert.Equal(1, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(2, comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.False(EvaluationRegressionPolicy.Default.Allows(comparison));
        Assert.True(policy.AllowsFailureRate(comparison));
        Assert.True(policy.AllowsUngroundedClaims(comparison));
        Assert.True(policy.AllowsMissingExpectedFacts(comparison));
        Assert.True(policy.Allows(comparison));
    }

    [Fact]
    public void AllowsRuns_fails_when_the_baseline_or_the_candidate_is_missing()
    {
        var policy = EvaluationRegressionPolicy.Default;
        var baseline = Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1);
        var candidate = Snapshot("candidate", 20, 2, ungrounded: 1, missing: 1);

        Assert.False(policy.AllowsRuns(null, candidate));
        Assert.False(policy.AllowsRuns(baseline, null));
        Assert.False(policy.AllowsRuns(null, null));
        Assert.True(policy.AllowsRuns(baseline, candidate));
    }

    [Fact]
    public void AllowsRuns_can_accept_a_missing_run_when_that_rule_is_turned_off()
    {
        var policy = EvaluationRegressionPolicy.Default with
        {
            FailWhenBaselineOrCandidateIsMissing = false
        };

        Assert.False(policy.FailWhenBaselineOrCandidateIsMissing);
        Assert.True(policy.AllowsRuns(null, null));
    }

    [Fact]
    public void AllowsRuns_accepts_empty_runs()
    {
        var baseline = Snapshot("baseline", 0, 0);
        var candidate = Snapshot("candidate", 0, 0);
        var comparison = EvaluationRunComparer.Compare(baseline, candidate);
        var policy = EvaluationRegressionPolicy.Default;

        Assert.Equal(0, baseline.TotalCases);
        Assert.Equal(0d, comparison.FailureRateChange);
        Assert.True(policy.AllowsRuns(baseline, candidate));
        Assert.True(policy.Allows(comparison));
    }

    [Fact]
    public void Allows_rejects_a_null_comparison()
    {
        var policy = EvaluationRegressionPolicy.Default;

        var missing = Assert.Throws<ArgumentNullException>(() => policy.Allows(null!));
        var missingRate = Assert.Throws<ArgumentNullException>(() => policy.AllowsFailureRate(null!));
        var missingClaims = Assert.Throws<ArgumentNullException>(() => policy.AllowsUngroundedClaims(null!));
        var missingFacts = Assert.Throws<ArgumentNullException>(() => policy.AllowsMissingExpectedFacts(null!));

        Assert.Equal("comparison", missing.ParamName);
        Assert.Equal("comparison", missingRate.ParamName);
        Assert.Equal("comparison", missingClaims.ParamName);
        Assert.Equal("comparison", missingFacts.ParamName);
    }

    private static EvaluationSnapshot Snapshot(
        string runId,
        int totalCases,
        int failedCases,
        int ungrounded = 0,
        int missing = 0,
        int empty = 0,
        int other = 0)
    {
        return new EvaluationSnapshot(
            runId,
            EvaluatedAt,
            totalCases,
            failedCases,
            Rate(failedCases, totalCases),
            new Dictionary<EvaluationFailureCategory, int>
            {
                [EvaluationFailureCategory.UngroundedClaim] = ungrounded,
                [EvaluationFailureCategory.MissingExpectedFact] = missing,
                [EvaluationFailureCategory.EmptyAnswer] = empty,
                [EvaluationFailureCategory.Other] = other
            });
    }

    private static double Rate(int failedCases, int totalCases)
    {
        if (totalCases == 0)
        {
            return 0d;
        }

        return (double)failedCases / totalCases * 100d;
    }
}
