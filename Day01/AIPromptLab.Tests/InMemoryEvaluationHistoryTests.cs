namespace AIPromptLab.Tests;

public sealed class InMemoryEvaluationHistoryTests
{
    [Fact]
    public void Retrieve_returns_runs_from_the_earliest_time_to_the_latest()
    {
        var store = new InMemoryEvaluationHistory();
        var noon = Snapshot("noon", At(12, 0, TimeSpan.Zero), failed: 2);
        var ten = Snapshot("ten", At(10, 0, TimeSpan.Zero), failed: 1);
        var nineUtc = Snapshot("nine-utc", At(11, 0, TimeSpan.FromHours(2)), failed: 0);

        store.Add(noon);
        store.Add(ten);
        store.Add(nineUtc);
        var history = store.Retrieve();

        Assert.Equal(["nine-utc", "ten", "noon"], history.Select(snapshot => snapshot.RunId));
        Assert.Equal([0, 1, 2], history.Select(snapshot => snapshot.FailedCases));
        store.Add(Snapshot("later", At(13, 0, TimeSpan.Zero), failed: 3));
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void Retrieve_keeps_add_order_when_the_time_is_the_same()
    {
        var store = new InMemoryEvaluationHistory();
        var first = At(12, 0, TimeSpan.Zero);
        var sameInstant = At(17, 30, TimeSpan.FromHours(5.5));

        store.Add(Snapshot("first-added", first, failed: 1));
        store.Add(Snapshot("second-added", sameInstant, failed: 2));
        store.Add(Snapshot("third-added", first, failed: 3));

        Assert.Equal(
            ["first-added", "second-added", "third-added"],
            store.Retrieve().Select(snapshot => snapshot.RunId));
    }

    [Fact]
    public void Retrieve_returns_an_empty_list_when_no_history_exists()
    {
        var history = new InMemoryEvaluationHistory().Retrieve();

        Assert.Empty(history);
    }

    [Fact]
    public void Find_returns_the_snapshot_with_the_requested_run_id()
    {
        var store = new InMemoryEvaluationHistory();
        store.Add(Snapshot("run-1", At(10, 0, TimeSpan.Zero), failed: 1));
        store.Add(Snapshot("run-2", At(11, 0, TimeSpan.Zero), failed: 2));

        var found = store.Find("  run-2  ");

        Assert.NotNull(found);
        Assert.Equal("run-2", found.RunId);
        Assert.Equal(2, found.FailedCases);
        Assert.Equal(100d, found.FailureRate);
        Assert.Equal(2, found.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
    }

    [Fact]
    public void Find_returns_null_when_the_run_id_is_unknown()
    {
        var store = new InMemoryEvaluationHistory();
        store.Add(Snapshot("known", At(10, 0, TimeSpan.Zero), failed: 0));

        Assert.Null(store.Find("missing"));
        Assert.Null(new InMemoryEvaluationHistory().Find("missing"));
    }

    [Fact]
    public void Add_rejects_a_duplicate_run_id()
    {
        var store = new InMemoryEvaluationHistory();
        store.Add(Snapshot("run-1", At(10, 0, TimeSpan.Zero), failed: 1));

        var exception = Assert.Throws<InvalidOperationException>(
            () => store.Add(Snapshot("  run-1  ", At(11, 0, TimeSpan.Zero), failed: 0)));

        Assert.Contains("run-1", exception.Message, StringComparison.Ordinal);
        var stored = Assert.Single(store.Retrieve());
        Assert.Equal(At(10, 0, TimeSpan.Zero), stored.EvaluatedAt);
        Assert.Equal(1, stored.FailedCases);
        Assert.Equal(1, stored.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
    }

    [Fact]
    public void Add_rejects_a_null_snapshot()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new InMemoryEvaluationHistory().Add(null!));

        Assert.Equal("snapshot", exception.ParamName);
    }

    [Fact]
    public void Add_keeps_stored_counts_when_the_callers_snapshot_changes()
    {
        var store = new InMemoryEvaluationHistory();
        var snapshot = Snapshot("run-1", At(10, 0, TimeSpan.Zero), failed: 1);
        store.Add(snapshot);

        CountsOf(snapshot)[EvaluationFailureCategory.UngroundedClaim] = 9;
        CountsOf(snapshot)[EvaluationFailureCategory.EmptyAnswer] = 4;

        var stored = store.Find("run-1");
        Assert.NotNull(stored);
        Assert.NotSame(snapshot, stored);
        Assert.Equal(1, stored.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(0, stored.CategoryCounts[EvaluationFailureCategory.EmptyAnswer]);
        Assert.Equal(1, stored.FailedCases);
    }

    [Fact]
    public void Find_returns_a_copy_so_a_later_edit_does_not_change_history()
    {
        var store = new InMemoryEvaluationHistory();
        store.Add(Snapshot("run-1", At(10, 0, TimeSpan.Zero), failed: 1));
        var returned = store.Find("run-1");

        Assert.NotNull(returned);
        CountsOf(returned)[EvaluationFailureCategory.UngroundedClaim] = 9;

        var again = store.Find("run-1");
        Assert.NotNull(again);
        Assert.NotSame(returned, again);
        Assert.NotSame(returned.CategoryCounts, again.CategoryCounts);
        Assert.Equal(1, again.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(
            [1],
            store.Retrieve().Select(snapshot => snapshot.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]));
    }

    [Fact]
    public void Find_rejects_a_blank_run_id()
    {
        var store = new InMemoryEvaluationHistory();
        var missing = Assert.Throws<ArgumentNullException>(() => store.Find(null!));
        var blank = Assert.Throws<ArgumentException>(() => store.Find("   "));

        Assert.Equal("runId", missing.ParamName);
        Assert.Equal("runId", blank.ParamName);
    }

    private static Dictionary<EvaluationFailureCategory, int> CountsOf(EvaluationSnapshot snapshot)
    {
        return Assert.IsAssignableFrom<Dictionary<EvaluationFailureCategory, int>>(snapshot.CategoryCounts);
    }

    private static EvaluationSnapshot Snapshot(string runId, DateTimeOffset evaluatedAt, int failed)
    {
        return new EvaluationSnapshot(
            runId,
            evaluatedAt,
            TotalCases: failed == 0 ? 1 : failed,
            FailedCases: failed,
            FailureRate: failed == 0 ? 0d : 100d,
            Counts(ungrounded: failed));
    }

    private static DateTimeOffset At(int hour, int minute, TimeSpan offset)
    {
        return new DateTimeOffset(2026, 11, 11, hour, minute, 0, offset);
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
