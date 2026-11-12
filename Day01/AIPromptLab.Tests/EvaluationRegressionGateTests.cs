namespace AIPromptLab.Tests;

public sealed class EvaluationRegressionGateTests
{
    private static DateTimeOffset EvaluatedAt { get; } =
        new(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Gate_passes_when_every_rule_holds_for_the_same_case_count()
    {
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1),
            Snapshot("candidate", 20, 3, ungrounded: 1, missing: 2),
            EvaluationRegressionPolicy.Default);

        var comparison = result.Comparison;
        Assert.NotNull(comparison);
        Assert.Equal(5d, comparison.FailureRateChange);
        Assert.Equal(comparison.Latest.FailureRate - comparison.Previous.FailureRate, comparison.FailureRateChange);
        Assert.Equal(0, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(1, comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
        Assert.Equal(
            string.Join(
                '\n',
                "Regression gate: PASSED",
                "Dataset: 20 -> 20 cases",
                "Failure rate: 10% -> 15% (change 5 percentage points, limit 5)",
                "UngroundedClaim: 1 -> 1 (change 0, limit 0)",
                "MissingExpectedFact: 1 -> 2 (change 1, limit 1)"),
            result.Report);
        Assert.Equal(result.Report, result.ToString());
    }

    [Fact]
    public void Gate_fails_when_the_failure_rate_regresses_by_more_than_five_points()
    {
        var baseline = Snapshot("baseline", 100, 15);
        var candidate = Snapshot("candidate", 100, 22);
        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);

        var comparison = result.Comparison;
        Assert.NotNull(comparison);
        Assert.Equal(15d, baseline.FailureRate);
        Assert.Equal(22d, candidate.FailureRate);
        Assert.Equal(candidate.FailureRate - baseline.FailureRate, comparison.FailureRateChange);
        Assert.Equal(7d, comparison.FailureRateChange);
        Assert.False(result.Passed);
        Assert.Equal(
            ["The failure rate regressed by 7 percentage points, from 15% to 22%. The limit is 5 percentage points."],
            result.FailureReasons);
    }

    [Fact]
    public void Gate_allows_the_same_seven_point_change_when_the_policy_limit_is_seven()
    {
        var policy = EvaluationRegressionPolicy.Default with { MaximumFailureRateRegression = 7d };
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 100, 15),
            Snapshot("candidate", 100, 22),
            policy);

        Assert.Equal(7d, result.Comparison!.FailureRateChange);
        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
        Assert.Contains("limit 7)", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_fails_when_ungrounded_claims_increase()
    {
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1),
            Snapshot("candidate", 20, 2, ungrounded: 2, missing: 1),
            EvaluationRegressionPolicy.Default);

        Assert.False(result.Passed);
        Assert.Equal(
            ["Ungrounded-claim failures increased by 1, from 1 to 2. The limit is 0."],
            result.FailureReasons);
    }

    [Fact]
    public void Gate_allows_one_additional_missing_fact_and_rejects_two()
    {
        var baseline = Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1);
        var oneMore = EvaluationRegressionGate.Evaluate(
            baseline,
            Snapshot("one-more", 20, 2, ungrounded: 1, missing: 2),
            EvaluationRegressionPolicy.Default);
        var twoMore = EvaluationRegressionGate.Evaluate(
            baseline,
            Snapshot("two-more", 20, 2, ungrounded: 1, missing: 3),
            EvaluationRegressionPolicy.Default);

        Assert.True(oneMore.Passed);
        Assert.Empty(oneMore.FailureReasons);
        Assert.False(twoMore.Passed);
        Assert.Equal(
            ["Missing expected facts increased by 2, from 1 to 3. The limit is 1."],
            twoMore.FailureReasons);
    }

    [Fact]
    public void Gate_collects_every_blocking_reason()
    {
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1),
            Snapshot("candidate", 20, 4, ungrounded: 2, missing: 3),
            EvaluationRegressionPolicy.Default);

        Assert.False(result.Passed);
        Assert.Equal(
            [
                "The failure rate regressed by 10 percentage points, from 10% to 20%. The limit is 5 percentage points.",
                "Ungrounded-claim failures increased by 1, from 1 to 2. The limit is 0.",
                "Missing expected facts increased by 2, from 1 to 3. The limit is 1."
            ],
            result.FailureReasons);
        Assert.Equal(3, result.Report.Split('\n').Count(line => line.StartsWith("- ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Gate_fails_a_different_dataset_size_even_when_the_rate_is_unchanged()
    {
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1),
            Snapshot("candidate", 10, 1, ungrounded: 1, missing: 1),
            EvaluationRegressionPolicy.Default);

        var comparison = result.Comparison;
        Assert.NotNull(comparison);
        Assert.True(comparison.DatasetSizeChanged);
        Assert.Equal(0d, comparison.FailureRateChange);
        Assert.False(result.Passed);
        Assert.Equal(
            ["The dataset size changed from 20 to 10 cases. These runs are not the same fixed evaluation dataset."],
            result.FailureReasons);
    }

    [Fact]
    public void Gate_reports_the_size_change_and_the_rate_regression_together()
    {
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 20, 0),
            Snapshot("candidate", 10, 1, ungrounded: 1),
            EvaluationRegressionPolicy.Default);

        Assert.Equal(10d, result.Comparison!.FailureRateChange);
        Assert.Equal(1, result.Comparison.FailedCaseChange);
        Assert.False(result.Passed);
        Assert.Equal(
            [
                "The dataset size changed from 20 to 10 cases. These runs are not the same fixed evaluation dataset.",
                "The failure rate regressed by 10 percentage points, from 0% to 10%. The limit is 5 percentage points.",
                "Ungrounded-claim failures increased by 1, from 0 to 1. The limit is 0."
            ],
            result.FailureReasons);
    }

    [Fact]
    public void Gate_fails_when_the_same_count_uses_different_cases()
    {
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 2, 0, caseIds: ["alpha", "beta"]),
            Snapshot("candidate", 2, 0, caseIds: ["alpha", "gamma"]),
            EvaluationRegressionPolicy.Default);

        Assert.Equal(0d, result.Comparison!.FailureRateChange);
        Assert.False(result.Comparison.DatasetSizeChanged);
        Assert.False(result.Passed);
        Assert.Equal(
            ["The evaluation cases changed from 'alpha', 'beta' to 'alpha', 'gamma'. These runs are not the same fixed evaluation dataset."],
            result.FailureReasons);
        Assert.Contains("different evaluation cases", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_accepts_the_same_cases_in_a_different_order()
    {
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 2, 0, caseIds: ["alpha", "beta"]),
            Snapshot("candidate", 2, 0, caseIds: ["beta", "alpha"]),
            EvaluationRegressionPolicy.Default);

        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
        Assert.Contains("same evaluation cases", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_fails_when_only_one_run_records_its_cases()
    {
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 2, 0, caseIds: ["alpha", "beta"]),
            Snapshot("candidate", 2, 0),
            EvaluationRegressionPolicy.Default);

        Assert.False(result.Passed);
        Assert.Equal(
            ["The candidate snapshot does not record its evaluation cases. These runs cannot be treated as the same fixed evaluation dataset."],
            result.FailureReasons);
    }

    [Fact]
    public void Gate_passes_two_captures_of_the_fixed_evaluation_dataset()
    {
        var baseline = EvaluationSnapshot.Capture(
            "baseline",
            EvaluatedAt,
            EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases));
        var candidate = EvaluationSnapshot.Capture(
            "candidate",
            EvaluatedAt.AddMinutes(5),
            EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases));

        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);

        Assert.Equal(baseline.CaseIds, candidate.CaseIds);
        Assert.Equal(6, baseline.TotalCases);
        Assert.Equal(0d, result.Comparison!.FailureRateChange);
        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
        Assert.Contains("same evaluation cases", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_rejects_a_smaller_dataset_when_its_failure_rate_improves()
    {
        var baseline = EvaluationSnapshot.Capture(
            "baseline",
            EvaluatedAt,
            EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases));
        var passingCase = AnswerEvaluationSet.PipelineCases.Single(
            item => item.Id == AnswerEvaluationSet.CorrectAndGrounded);
        var candidate = EvaluationSnapshot.Capture(
            "candidate",
            EvaluatedAt.AddMinutes(5),
            EvaluationPipeline.Evaluate([passingCase]));

        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);

        Assert.True(result.Comparison!.FailureRateChange < 0d);
        Assert.False(result.Passed);
        Assert.Equal(
            [
                "The dataset size changed from 6 to 1 cases. These runs are not the same fixed evaluation dataset.",
                "The evaluation cases changed from 'Correct and grounded answer', 'Answer contains an unsupported claim', 'Answer misses an important expected fact', 'Relevant answer with the wrong fact', 'Empty answer', 'Fully correct answer with different wording' to 'Correct and grounded answer'. These runs are not the same fixed evaluation dataset."
            ],
            result.FailureReasons);
        Assert.DoesNotContain("failure rate regressed", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_fails_when_the_baseline_or_the_candidate_is_missing()
    {
        var policy = EvaluationRegressionPolicy.Default;
        var baseline = Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1);
        var candidate = Snapshot("candidate", 20, 2, ungrounded: 1, missing: 1);

        var missingBaseline = EvaluationRegressionGate.Evaluate(null, candidate, policy);
        var missingCandidate = EvaluationRegressionGate.Evaluate(baseline, null, policy);
        var missingBoth = EvaluationRegressionGate.Evaluate(null, null, policy);

        Assert.False(missingBaseline.Passed);
        Assert.Null(missingBaseline.Comparison);
        Assert.Equal(["The baseline snapshot is missing."], missingBaseline.FailureReasons);
        Assert.Equal(
            "Regression gate: FAILED\n- The baseline snapshot is missing.",
            missingBaseline.Report);

        Assert.False(missingCandidate.Passed);
        Assert.Equal(["The candidate snapshot is missing."], missingCandidate.FailureReasons);

        Assert.False(missingBoth.Passed);
        Assert.Equal(
            [
                "The baseline snapshot is missing.",
                "The candidate snapshot is missing."
            ],
            missingBoth.FailureReasons);
    }

    [Fact]
    public void Gate_skips_the_missing_snapshot_rule_when_the_policy_turns_it_off()
    {
        var policy = EvaluationRegressionPolicy.Default with
        {
            FailWhenBaselineOrCandidateIsMissing = false
        };

        var result = EvaluationRegressionGate.Evaluate(null, null, policy);

        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
        Assert.Null(result.Comparison);
    }

    [Fact]
    public void Gate_rejects_a_null_policy()
    {
        var baseline = Snapshot("baseline", 1, 0);
        var exception = Assert.Throws<ArgumentNullException>(
            () => EvaluationRegressionGate.Evaluate(baseline, baseline, null!));

        Assert.Equal("policy", exception.ParamName);
    }

    [Fact]
    public void Gate_repeats_the_same_result_for_the_same_runs()
    {
        var baseline = Snapshot("baseline", 100, 15);
        var candidate = Snapshot("candidate", 100, 22);
        var first = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);
        var second = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);

        Assert.Equal(first.Report, second.Report);
        Assert.Equal(first.Passed, second.Passed);
        Assert.Equal(first.FailureReasons, second.FailureReasons);
        Assert.False(first.Passed);

        var sameCases = new[] { "alpha", "beta" };
        var passFirst = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 2, 1, ungrounded: 1, caseIds: sameCases),
            Snapshot("candidate", 2, 1, ungrounded: 1, caseIds: sameCases),
            EvaluationRegressionPolicy.Default);
        var passSecond = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 2, 1, ungrounded: 1, caseIds: sameCases),
            Snapshot("candidate", 2, 1, ungrounded: 1, caseIds: sameCases),
            EvaluationRegressionPolicy.Default);

        Assert.Equal(passFirst.Report, passSecond.Report);
        Assert.Equal(passFirst.Passed, passSecond.Passed);
        Assert.Equal(passFirst.FailureReasons, passSecond.FailureReasons);
        Assert.True(passFirst.Passed);
        Assert.Equal(passFirst.Comparison!.FailureRateChange, passSecond.Comparison!.FailureRateChange);
    }

    [Fact]
    public void Gate_passes_when_there_is_no_regression()
    {
        var cases = new[] { "alpha", "beta" };
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 2, 1, ungrounded: 1, caseIds: cases),
            Snapshot("candidate", 2, 1, ungrounded: 1, caseIds: cases),
            EvaluationRegressionPolicy.Default);

        var comparison = result.Comparison;
        Assert.NotNull(comparison);
        Assert.Equal(0d, comparison.FailureRateChange);
        Assert.Equal(EvaluationRateDirection.Same, comparison.Direction);
        Assert.Equal(0, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(0, comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.False(comparison.DatasetSizeChanged);
        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
    }

    [Fact]
    public void Gate_passes_when_the_regression_is_within_the_tolerance()
    {
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 20, 2, ungrounded: 1, missing: 1),
            Snapshot("candidate", 20, 3, ungrounded: 1, missing: 1),
            EvaluationRegressionPolicy.Default);

        var comparison = result.Comparison;
        Assert.NotNull(comparison);
        Assert.Equal(5d, comparison.FailureRateChange);
        Assert.Equal(EvaluationRateDirection.Worsened, comparison.Direction);
        Assert.True(comparison.FailureRateChange <= result.Policy.MaximumFailureRateRegression);
        Assert.Equal(0, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(0, comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
    }

    [Fact]
    public void Gate_fails_when_the_failure_rate_improves_but_ungrounded_claims_increase()
    {
        var cases = new[] { "alpha", "beta", "gamma", "delta" };
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 4, 2, ungrounded: 0, missing: 1, caseIds: cases),
            Snapshot("candidate", 4, 1, ungrounded: 1, missing: 1, caseIds: cases),
            EvaluationRegressionPolicy.Default);

        var comparison = result.Comparison;
        Assert.NotNull(comparison);
        Assert.Equal(-25d, comparison.FailureRateChange);
        Assert.Equal(EvaluationRateDirection.Improved, comparison.Direction);
        Assert.Equal(1, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(0, comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.False(result.Passed);
        Assert.Equal(
            ["Ungrounded-claim failures increased by 1, from 0 to 1. The limit is 0."],
            result.FailureReasons);
        Assert.DoesNotContain("failure rate regressed", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Gate_accepts_a_category_at_its_limit_and_rejects_one_past_it()
    {
        var cases = new[] { "alpha", "beta", "gamma", "delta" };
        var baseline = Snapshot("baseline", 4, 1, ungrounded: 1, missing: 1, caseIds: cases);
        var policy = EvaluationRegressionPolicy.Default;

        var atLimit = EvaluationRegressionGate.Evaluate(
            baseline,
            Snapshot("at-limit", 4, 1, ungrounded: 1, missing: 2, caseIds: cases),
            policy);
        var ungroundedPastLimit = EvaluationRegressionGate.Evaluate(
            baseline,
            Snapshot("ungrounded", 4, 1, ungrounded: 2, missing: 1, caseIds: cases),
            policy);
        var missingPastLimit = EvaluationRegressionGate.Evaluate(
            baseline,
            Snapshot("missing", 4, 1, ungrounded: 1, missing: 3, caseIds: cases),
            policy);

        Assert.Equal(0, atLimit.Comparison!.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(1, atLimit.Comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(0d, atLimit.Comparison.FailureRateChange);
        Assert.True(atLimit.Passed);
        Assert.Empty(atLimit.FailureReasons);

        Assert.Equal(1, ungroundedPastLimit.Comparison!.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.False(ungroundedPastLimit.Passed);
        Assert.Equal(
            ["Ungrounded-claim failures increased by 1, from 1 to 2. The limit is 0."],
            ungroundedPastLimit.FailureReasons);

        Assert.Equal(2, missingPastLimit.Comparison!.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.False(missingPastLimit.Passed);
        Assert.Equal(
            ["Missing expected facts increased by 2, from 1 to 3. The limit is 1."],
            missingPastLimit.FailureReasons);
    }

    [Fact]
    public void Gate_fails_safely_when_the_comparison_data_is_missing_or_invalid()
    {
        var policy = EvaluationRegressionPolicy.Default;
        var baseline = Snapshot("baseline", 2, 0, caseIds: ["alpha", "beta"]);
        var candidate = Snapshot("candidate", 2, 0, caseIds: ["alpha", "beta"]);

        var missingBaseline = EvaluationRegressionGate.Evaluate(null, candidate, policy);
        var missingCandidate = EvaluationRegressionGate.Evaluate(baseline, null, policy);
        var missingBoth = EvaluationRegressionGate.Evaluate(null, null, policy);
        var differentCases = EvaluationRegressionGate.Evaluate(
            baseline,
            Snapshot("candidate", 2, 0, caseIds: ["alpha", "gamma"]),
            policy);
        var differentSize = EvaluationRegressionGate.Evaluate(
            baseline,
            Snapshot("candidate", 1, 0, caseIds: ["alpha"]),
            policy);

        Assert.False(missingBaseline.Passed);
        Assert.Null(missingBaseline.Comparison);
        Assert.Equal(["The baseline snapshot is missing."], missingBaseline.FailureReasons);

        Assert.False(missingCandidate.Passed);
        Assert.Null(missingCandidate.Comparison);
        Assert.Equal(["The candidate snapshot is missing."], missingCandidate.FailureReasons);

        Assert.False(missingBoth.Passed);
        Assert.Null(missingBoth.Comparison);
        Assert.Equal(
            [
                "The baseline snapshot is missing.",
                "The candidate snapshot is missing."
            ],
            missingBoth.FailureReasons);

        Assert.False(differentCases.Passed);
        Assert.NotNull(differentCases.Comparison);
        Assert.Equal(
            ["The evaluation cases changed from 'alpha', 'beta' to 'alpha', 'gamma'. These runs are not the same fixed evaluation dataset."],
            differentCases.FailureReasons);

        Assert.False(differentSize.Passed);
        Assert.NotNull(differentSize.Comparison);
        Assert.Contains(
            "The dataset size changed from 2 to 1 cases. These runs are not the same fixed evaluation dataset.",
            differentSize.FailureReasons);
        Assert.Contains(
            "The evaluation cases changed from 'alpha', 'beta' to 'alpha'. These runs are not the same fixed evaluation dataset.",
            differentSize.FailureReasons);
    }

    private static EvaluationSnapshot Snapshot(
        string runId,
        int totalCases,
        int failedCases,
        int ungrounded = 0,
        int missing = 0,
        int empty = 0,
        int other = 0,
        IReadOnlyList<string>? caseIds = null)
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
            },
            caseIds);
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
