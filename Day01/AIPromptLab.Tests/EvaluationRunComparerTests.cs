namespace AIPromptLab.Tests;

public sealed class EvaluationRunComparerTests
{
    private static DateTimeOffset EvaluatedAt { get; } =
        new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Compare_reports_an_improvement_in_percentage_points()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("previous", 20, 6, ungrounded: 4, missing: 2),
            Snapshot("latest", 20, 3, ungrounded: 1, missing: 1, empty: 1));

        Assert.Equal(-15d, comparison.FailureRateChange);
        Assert.Equal(-3, comparison.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Improved, comparison.Direction);
        Assert.False(comparison.DatasetSizeChanged);
        Assert.Equal(
            [
                Pair(EvaluationFailureCategory.UngroundedClaim, -3),
                Pair(EvaluationFailureCategory.MissingExpectedFact, -1),
                Pair(EvaluationFailureCategory.EmptyAnswer, 1),
                Pair(EvaluationFailureCategory.Other, 0)
            ],
            comparison.CategoryCountChanges.ToArray());
        Assert.Equal(
            string.Join(
                '\n',
                "Evaluation comparison:",
                "Previous: previous",
                "Latest: latest",
                "Total cases: 20 -> 20",
                "Failed cases: 6 -> 3",
                "Failed-case change: -3",
                "Failure rate: 30% -> 15%",
                "Failure-rate change: -15 percentage points",
                "UngroundedClaim: 4 -> 1 (change -3)",
                "MissingExpectedFact: 2 -> 1 (change -1)",
                "EmptyAnswer: 0 -> 1 (change 1)",
                "Other: 0 -> 0 (change 0)",
                "The failure rate improved by 15 percentage points."),
            comparison.Report);
        Assert.Equal(comparison.Report, comparison.ToString());
        Assert.DoesNotContain("50", comparison.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_reports_a_higher_failure_rate_as_worsened()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("previous", 20, 3, ungrounded: 3),
            Snapshot("latest", 20, 6, ungrounded: 6));

        Assert.Equal(15d, comparison.FailureRateChange);
        Assert.Equal(3, comparison.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Worsened, comparison.Direction);
        Assert.EndsWith(
            "The failure rate worsened by 15 percentage points.",
            comparison.Report,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_reports_an_equal_failure_rate_as_the_same()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("previous", 10, 2, ungrounded: 2),
            Snapshot("latest", 10, 2, empty: 2));

        Assert.Equal(0d, comparison.FailureRateChange);
        Assert.Equal(0, comparison.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Same, comparison.Direction);
        Assert.Equal(-2, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(2, comparison.CategoryCountChanges[EvaluationFailureCategory.EmptyAnswer]);
        Assert.EndsWith("The failure rate stayed the same.", comparison.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_keeps_rates_comparable_when_the_dataset_size_changes()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("previous", 20, 6, ungrounded: 6),
            Snapshot("latest", 10, 3, ungrounded: 3));

        Assert.True(comparison.DatasetSizeChanged);
        Assert.Equal(0d, comparison.FailureRateChange);
        Assert.Equal(-3, comparison.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Same, comparison.Direction);
        Assert.Contains(
            "Dataset size changed from 20 to 10 cases. The failure rate is the share of failed cases in each run. The failed-case change is the raw count between those sizes.",
            comparison.Report,
            StringComparison.Ordinal);
        Assert.EndsWith("The failure rate stayed the same.", comparison.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_can_improve_the_rate_while_the_failed_count_rises()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("previous", 2, 1, ungrounded: 1),
            Snapshot("latest", 10, 2, ungrounded: 2));

        Assert.True(comparison.DatasetSizeChanged);
        Assert.Equal(-30d, comparison.FailureRateChange);
        Assert.Equal(1, comparison.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Improved, comparison.Direction);
        Assert.Equal(1, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.EndsWith(
            "The failure rate improved by 30 percentage points.",
            comparison.Report,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_uses_a_zero_rate_when_a_run_has_no_cases()
    {
        var bothEmpty = EvaluationRunComparer.Compare(
            Snapshot("empty-a", 0, 0),
            Snapshot("empty-b", 0, 0));
        var fromEmpty = EvaluationRunComparer.Compare(
            Snapshot("empty", 0, 0),
            Snapshot("latest", 2, 1, ungrounded: 1));
        var toEmpty = EvaluationRunComparer.Compare(
            Snapshot("previous", 2, 1, ungrounded: 1),
            Snapshot("empty", 0, 0));

        Assert.Equal(0d, bothEmpty.FailureRateChange);
        Assert.Equal(0, bothEmpty.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Same, bothEmpty.Direction);
        Assert.False(bothEmpty.DatasetSizeChanged);
        Assert.Contains(
            "Both runs have no cases. Their failure rates are 0.",
            bothEmpty.Report,
            StringComparison.Ordinal);
        Assert.EndsWith("The failure rate stayed the same.", bothEmpty.Report, StringComparison.Ordinal);

        Assert.Equal(50d, fromEmpty.FailureRateChange);
        Assert.Equal(1, fromEmpty.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Worsened, fromEmpty.Direction);
        Assert.True(fromEmpty.DatasetSizeChanged);
        Assert.Contains(
            "The previous run has no cases. Its failure rate is 0.",
            fromEmpty.Report,
            StringComparison.Ordinal);
        Assert.EndsWith(
            "The failure rate worsened by 50 percentage points.",
            fromEmpty.Report,
            StringComparison.Ordinal);

        Assert.Equal(-50d, toEmpty.FailureRateChange);
        Assert.Equal(-1, toEmpty.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Improved, toEmpty.Direction);
        Assert.Contains(
            "The latest run has no cases. Its failure rate is 0.",
            toEmpty.Report,
            StringComparison.Ordinal);
        Assert.EndsWith(
            "The failure rate improved by 50 percentage points.",
            toEmpty.Report,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_reads_category_counts_from_the_snapshots()
    {
        var previous = EvaluationSnapshot.Capture(
            "previous",
            EvaluatedAt,
            EvaluationPipeline.Evaluate(
            [
                new EvaluationCase(
                    "two unsupported claims",
                    AnswerSupportExample.Question,
                    "Someone mentioned .NET 10 and Retry-After.",
                    AnswerSupportExample.SupportedAnswer,
                    AnswerEvaluationSet.ExpectedFacts)
            ]));
        var latest = EvaluationSnapshot.Capture(
            "latest",
            EvaluatedAt.AddMinutes(10),
            EvaluationPipeline.Evaluate(
            [
                Case(AnswerEvaluationSet.CorrectAndGrounded)
            ]));

        var comparison = EvaluationRunComparer.Compare(previous, latest);

        Assert.Equal(-100d, comparison.FailureRateChange);
        Assert.Equal(-1, comparison.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Improved, comparison.Direction);
        Assert.Equal(-2, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(0, comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
    }

    [Fact]
    public void Compare_keeps_category_changes_when_the_source_counts_change()
    {
        var counts = Counts(ungrounded: 4, missing: 2);
        var previous = Snapshot("previous", 20, 6, counts);
        var latest = Snapshot("latest", 20, 3, Counts(ungrounded: 1, missing: 1, empty: 1));
        var comparison = EvaluationRunComparer.Compare(previous, latest);

        counts[EvaluationFailureCategory.UngroundedClaim] = 40;
        CountsOf(previous)[EvaluationFailureCategory.UngroundedClaim] = 40;
        CountsOf(comparison.Previous)[EvaluationFailureCategory.UngroundedClaim] = 40;

        Assert.Equal(4, comparison.PreviousCategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(-3, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Contains("UngroundedClaim: 4 -> 1 (change -3)", comparison.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_rejects_a_null_snapshot()
    {
        var previous = Snapshot("previous", 1, 0);
        var missingPrevious = Assert.Throws<ArgumentNullException>(
            () => EvaluationRunComparer.Compare(null!, previous));
        var missingLatest = Assert.Throws<ArgumentNullException>(
            () => EvaluationRunComparer.Compare(previous, null!));

        Assert.Equal("previous", missingPrevious.ParamName);
        Assert.Equal("latest", missingLatest.ParamName);
    }

    private static Dictionary<EvaluationFailureCategory, int> CountsOf(EvaluationSnapshot snapshot)
    {
        return Assert.IsAssignableFrom<Dictionary<EvaluationFailureCategory, int>>(snapshot.CategoryCounts);
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
        return Snapshot(runId, totalCases, failedCases, Counts(ungrounded, missing, empty, other));
    }

    private static EvaluationSnapshot Snapshot(
        string runId,
        int totalCases,
        int failedCases,
        Dictionary<EvaluationFailureCategory, int> counts)
    {
        return new EvaluationSnapshot(
            runId,
            EvaluatedAt,
            totalCases,
            failedCases,
            Rate(failedCases, totalCases),
            counts);
    }

    private static double Rate(int failedCases, int totalCases)
    {
        if (totalCases == 0)
        {
            return 0d;
        }

        return (double)failedCases / totalCases * 100d;
    }

    private static EvaluationCase Case(string id)
    {
        return Assert.Single(AnswerEvaluationSet.PipelineCases, item => item.Id == id);
    }

    private static Dictionary<EvaluationFailureCategory, int> Counts(
        int ungrounded = 0,
        int missing = 0,
        int empty = 0,
        int other = 0)
    {
        return new Dictionary<EvaluationFailureCategory, int>
        {
            [EvaluationFailureCategory.UngroundedClaim] = ungrounded,
            [EvaluationFailureCategory.MissingExpectedFact] = missing,
            [EvaluationFailureCategory.EmptyAnswer] = empty,
            [EvaluationFailureCategory.Other] = other
        };
    }

    private static KeyValuePair<EvaluationFailureCategory, int> Pair(
        EvaluationFailureCategory category,
        int change)
    {
        return new KeyValuePair<EvaluationFailureCategory, int>(category, change);
    }
}
