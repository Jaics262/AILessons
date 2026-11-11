namespace AIPromptLab.Tests;

public sealed class EvaluationHistoryComparisonTests
{
    private static DateTimeOffset At(int hour) =>
        new(2026, 11, 11, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Add_then_retrieve_returns_the_stored_snapshot()
    {
        var history = new InMemoryEvaluationHistory();
        var earlier = Snapshot("run-earlier", At(9), total: 20, failed: 6, ungrounded: 4, missing: 2);
        var later = Snapshot("run-later", At(11), total: 20, failed: 3, ungrounded: 1, missing: 1, empty: 1);

        history.Add(later);
        history.Add(earlier);

        var stored = history.Retrieve();
        var found = history.Find("run-earlier");

        Assert.Equal(["run-earlier", "run-later"], stored.Select(snapshot => snapshot.RunId));
        Assert.NotNull(found);
        Assert.Equal(At(9), found.EvaluatedAt);
        Assert.Equal(20, found.TotalCases);
        Assert.Equal(6, found.FailedCases);
        Assert.Equal(30d, found.FailureRate);
        Assert.Equal(4, found.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(2, found.CategoryCounts[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(0, found.CategoryCounts[EvaluationFailureCategory.EmptyAnswer]);
        Assert.Equal(3, stored[1].FailedCases);
        Assert.Equal(15d, stored[1].FailureRate);
    }

    [Fact]
    public void Retrieve_orders_runs_from_earliest_to_latest()
    {
        var history = new InMemoryEvaluationHistory();
        history.Add(Snapshot("noon", At(12), total: 4, failed: 2, ungrounded: 2));
        history.Add(Snapshot("nine", At(9), total: 4, failed: 1, ungrounded: 1));
        history.Add(Snapshot("ten-utc", new DateTimeOffset(2026, 11, 11, 15, 30, 0, TimeSpan.FromHours(5.5)), total: 4, failed: 0));

        Assert.Equal(
            ["nine", "ten-utc", "noon"],
            history.Retrieve().Select(snapshot => snapshot.RunId));
    }

    [Fact]
    public void Add_rejects_a_duplicate_run_id_and_keeps_the_first_snapshot()
    {
        var history = new InMemoryEvaluationHistory();
        history.Add(Snapshot("run-1", At(9), total: 20, failed: 6, ungrounded: 4, missing: 2));

        var exception = Assert.Throws<InvalidOperationException>(
            () => history.Add(Snapshot("run-1", At(12), total: 20, failed: 1, ungrounded: 1)));

        Assert.Contains("run-1", exception.Message, StringComparison.Ordinal);
        var stored = Assert.Single(history.Retrieve());
        Assert.Equal(At(9), stored.EvaluatedAt);
        Assert.Equal(6, stored.FailedCases);
        Assert.Equal(4, stored.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(2, stored.CategoryCounts[EvaluationFailureCategory.MissingExpectedFact]);
    }

    [Fact]
    public void Retrieve_returns_an_empty_collection_when_no_history_exists()
    {
        var history = new InMemoryEvaluationHistory().Retrieve();

        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public void Compare_reports_improvement_deterioration_and_an_unchanged_rate()
    {
        var improved = EvaluationRunComparer.Compare(
            Snapshot("previous", At(9), total: 20, failed: 6, ungrounded: 6),
            Snapshot("latest", At(10), total: 20, failed: 3, ungrounded: 3));
        var worsened = EvaluationRunComparer.Compare(
            Snapshot("previous", At(9), total: 20, failed: 3, ungrounded: 3),
            Snapshot("latest", At(10), total: 20, failed: 6, ungrounded: 6));
        var same = EvaluationRunComparer.Compare(
            Snapshot("previous", At(9), total: 20, failed: 6, ungrounded: 6),
            Snapshot("latest", At(10), total: 10, failed: 3, ungrounded: 3));

        Assert.Equal(-15d, improved.FailureRateChange);
        Assert.Equal(-3, improved.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Improved, improved.Direction);
        Assert.EndsWith(
            "The failure rate improved by 15 percentage points.",
            improved.Report,
            StringComparison.Ordinal);

        Assert.Equal(15d, worsened.FailureRateChange);
        Assert.Equal(3, worsened.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Worsened, worsened.Direction);
        Assert.EndsWith(
            "The failure rate worsened by 15 percentage points.",
            worsened.Report,
            StringComparison.Ordinal);

        Assert.Equal(0d, same.FailureRateChange);
        Assert.Equal(-3, same.FailedCaseChange);
        Assert.Equal(EvaluationRateDirection.Same, same.Direction);
        Assert.True(same.DatasetSizeChanged);
        Assert.EndsWith("The failure rate stayed the same.", same.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_reports_failure_category_differences()
    {
        var comparison = EvaluationRunComparer.Compare(
            Snapshot("previous", At(9), total: 20, failed: 6, ungrounded: 4, missing: 2),
            Snapshot("latest", At(10), total: 20, failed: 3, ungrounded: 1, missing: 1, empty: 1));

        Assert.Equal(-3, comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(-1, comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(1, comparison.CategoryCountChanges[EvaluationFailureCategory.EmptyAnswer]);
        Assert.Equal(0, comparison.CategoryCountChanges[EvaluationFailureCategory.Other]);
        Assert.Contains("UngroundedClaim: 4 -> 1 (change -3)", comparison.Report, StringComparison.Ordinal);
        Assert.Contains("MissingExpectedFact: 2 -> 1 (change -1)", comparison.Report, StringComparison.Ordinal);
        Assert.Contains("EmptyAnswer: 0 -> 1 (change 1)", comparison.Report, StringComparison.Ordinal);
        Assert.Contains("Other: 0 -> 0 (change 0)", comparison.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_uses_a_zero_rate_for_a_snapshot_with_no_cases()
    {
        var history = new InMemoryEvaluationHistory();
        var empty = Snapshot("empty", At(8), total: 0, failed: 0);
        var later = Snapshot("later", At(10), total: 2, failed: 1, ungrounded: 1);
        history.Add(later);
        history.Add(empty);

        var stored = history.Retrieve();
        Assert.Equal(["empty", "later"], stored.Select(snapshot => snapshot.RunId));
        Assert.Equal(0, stored[0].TotalCases);
        Assert.Equal(0, stored[0].FailedCases);
        Assert.Equal(0d, stored[0].FailureRate);
        Assert.All(stored[0].CategoryCounts.Values, count => Assert.Equal(0, count));

        var bothEmpty = EvaluationRunComparer.Compare(empty, Snapshot("also-empty", At(9), total: 0, failed: 0));
        var fromEmpty = EvaluationRunComparer.Compare(stored[0], stored[1]);

        Assert.Equal(EvaluationRateDirection.Same, bothEmpty.Direction);
        Assert.Equal(0d, bothEmpty.FailureRateChange);
        Assert.Contains("Both runs have no cases. Their failure rates are 0.", bothEmpty.Report, StringComparison.Ordinal);

        Assert.Equal(EvaluationRateDirection.Worsened, fromEmpty.Direction);
        Assert.Equal(50d, fromEmpty.FailureRateChange);
        Assert.Equal(1, fromEmpty.FailedCaseChange);
        Assert.Contains("The previous run has no cases. Its failure rate is 0.", fromEmpty.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void History_keeps_stored_counts_when_the_original_input_dictionary_changes()
    {
        var counts = Counts(ungrounded: 2, missing: 1);
        var snapshot = Snapshot("run-1", At(9), total: 4, failed: 2, counts);
        var history = new InMemoryEvaluationHistory();
        history.Add(snapshot);

        counts[EvaluationFailureCategory.UngroundedClaim] = 9;
        counts[EvaluationFailureCategory.MissingExpectedFact] = 0;
        counts[EvaluationFailureCategory.EmptyAnswer] = 5;

        var stored = history.Find("run-1");
        var listed = Assert.Single(history.Retrieve());

        Assert.NotNull(stored);
        Assert.Equal(2, stored.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(1, stored.CategoryCounts[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(0, stored.CategoryCounts[EvaluationFailureCategory.EmptyAnswer]);
        Assert.Equal(2, stored.FailedCases);
        Assert.Equal(50d, stored.FailureRate);
        Assert.Equal(2, listed.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(1, listed.CategoryCounts[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(0, listed.CategoryCounts[EvaluationFailureCategory.EmptyAnswer]);
    }

    private static EvaluationSnapshot Snapshot(
        string runId,
        DateTimeOffset evaluatedAt,
        int total,
        int failed,
        int ungrounded = 0,
        int missing = 0,
        int empty = 0,
        int other = 0)
    {
        return Snapshot(runId, evaluatedAt, total, failed, Counts(ungrounded, missing, empty, other));
    }

    private static EvaluationSnapshot Snapshot(
        string runId,
        DateTimeOffset evaluatedAt,
        int total,
        int failed,
        Dictionary<EvaluationFailureCategory, int> counts)
    {
        return new EvaluationSnapshot(
            runId,
            evaluatedAt,
            total,
            failed,
            Rate(failed, total),
            counts);
    }

    private static double Rate(int failed, int total)
    {
        if (total == 0)
        {
            return 0d;
        }

        return (double)failed / total * 100d;
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
}
