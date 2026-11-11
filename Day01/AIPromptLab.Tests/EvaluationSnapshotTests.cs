namespace AIPromptLab.Tests;

public sealed class EvaluationSnapshotTests
{
    private static DateTimeOffset EvaluatedAt { get; } =
        new(2026, 11, 11, 11, 12, 0, TimeSpan.Zero);

    [Fact]
    public void Capture_copies_the_analysis_of_one_completed_run()
    {
        var report = EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases);
        var analysis = EvaluationFailureAnalyzer.Analyze(report);

        var snapshot = EvaluationSnapshot.Capture("  run-1  ", EvaluatedAt, report);

        Assert.Equal("run-1", snapshot.RunId);
        Assert.Equal(EvaluatedAt, snapshot.EvaluatedAt);
        Assert.Equal(analysis.TotalCases, snapshot.TotalCases);
        Assert.Equal(analysis.FailedCases, snapshot.FailedCases);
        Assert.Equal(analysis.FailureRate, snapshot.FailureRate);
        Assert.Equal(6, snapshot.TotalCases);
        Assert.Equal(4, snapshot.FailedCases);
        Assert.Equal((double)4 / 6 * 100d, snapshot.FailureRate);
        Assert.Equal(analysis.CategoryCounts.ToArray(), snapshot.CategoryCounts.ToArray());
        Assert.NotSame(analysis.CategoryCounts, snapshot.CategoryCounts);
    }

    [Fact]
    public void Capture_keeps_occurrence_counts_for_one_failed_case()
    {
        var report = EvaluationPipeline.Evaluate(
        [
            new EvaluationCase(
                "two unsupported claims",
                AnswerSupportExample.Question,
                "Someone mentioned .NET 10 and Retry-After.",
                AnswerSupportExample.SupportedAnswer,
                AnswerEvaluationSet.ExpectedFacts)
        ]);
        var analysis = EvaluationFailureAnalyzer.Analyze(report);

        var snapshot = EvaluationSnapshot.Capture("run-occurrences", EvaluatedAt, report);

        Assert.Equal(analysis.TotalCases, snapshot.TotalCases);
        Assert.Equal(1, snapshot.TotalCases);
        Assert.Equal(1, snapshot.FailedCases);
        Assert.Equal(100d, snapshot.FailureRate);
        Assert.Equal(2, snapshot.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(0, snapshot.CategoryCounts[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(analysis.CategoryCounts.ToArray(), snapshot.CategoryCounts.ToArray());
    }

    [Fact]
    public void Capture_keeps_a_zero_rate_when_the_run_has_no_failures()
    {
        var passing = EvaluationSnapshot.Capture(
            "run-pass",
            EvaluatedAt,
            EvaluationPipeline.Evaluate(
            [
                Case(AnswerEvaluationSet.CorrectAndGrounded),
                Case(AnswerEvaluationSet.DifferentWording)
            ]));
        var empty = EvaluationSnapshot.Capture(
            "run-empty",
            EvaluatedAt,
            EvaluationPipeline.Evaluate([]));

        Assert.Equal(2, passing.TotalCases);
        Assert.Equal(0, passing.FailedCases);
        Assert.Equal(0d, passing.FailureRate);
        Assert.All(passing.CategoryCounts.Values, count => Assert.Equal(0, count));

        Assert.Equal(0, empty.TotalCases);
        Assert.Equal(0, empty.FailedCases);
        Assert.Equal(0d, empty.FailureRate);
        Assert.Equal(4, empty.CategoryCounts.Count);
        Assert.All(empty.CategoryCounts.Values, count => Assert.Equal(0, count));
    }

    [Fact]
    public void Capture_keeps_the_run_when_a_later_report_is_different()
    {
        var first = EvaluationSnapshot.Capture(
            "run-1",
            EvaluatedAt,
            EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases));
        var second = EvaluationSnapshot.Capture(
            "run-2",
            EvaluatedAt.AddMinutes(10),
            EvaluationPipeline.Evaluate([]));

        Assert.Equal(6, first.TotalCases);
        Assert.Equal(4, first.FailedCases);
        Assert.Equal((double)4 / 6 * 100d, first.FailureRate);
        Assert.Equal(2, first.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(0, second.TotalCases);
        Assert.Equal(0, second.FailedCases);
        Assert.Equal("run-2", second.RunId);
        Assert.Equal(EvaluatedAt.AddMinutes(10), second.EvaluatedAt);
    }

    [Fact]
    public void Capture_keeps_category_counts_when_the_report_results_change()
    {
        var report = EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases);
        var snapshot = EvaluationSnapshot.Capture("run-1", EvaluatedAt, report);
        var ungrounded = snapshot.CategoryCounts[EvaluationFailureCategory.UngroundedClaim];
        var empty = snapshot.CategoryCounts[EvaluationFailureCategory.EmptyAnswer];
        var result = Assert.Single(
            report.Results,
            item => item.CaseId == AnswerEvaluationSet.UnsupportedClaim);
        var failures = Assert.IsAssignableFrom<EvaluationFailure[]>(result.Failures);

        failures[0] = EvaluationFailure.EmptyAnswer();

        Assert.Equal(ungrounded, snapshot.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(empty, snapshot.CategoryCounts[EvaluationFailureCategory.EmptyAnswer]);
        Assert.Contains(
            EvaluationFailureCategory.EmptyAnswer,
            result.FailureCategories);
    }

    [Fact]
    public void Constructor_copies_category_counts()
    {
        var counts = Counts(ungrounded: 1);

        var snapshot = new EvaluationSnapshot("run-1", EvaluatedAt, 1, 1, 100d, counts);
        counts[EvaluationFailureCategory.UngroundedClaim] = 4;

        Assert.NotSame(counts, snapshot.CategoryCounts);
        Assert.Equal(1, snapshot.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(1, snapshot.TotalCases);
        Assert.Equal(1, snapshot.FailedCases);
        Assert.Equal(100d, snapshot.FailureRate);
    }

    [Fact]
    public void Capture_rejects_a_null_report()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => EvaluationSnapshot.Capture("run-1", EvaluatedAt, null!));

        Assert.Equal("report", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_blank_run_id()
    {
        var missing = Assert.Throws<ArgumentNullException>(() => Snapshot(null!));
        var blank = Assert.Throws<ArgumentException>(() => Snapshot("   "));

        Assert.Equal("runId", missing.ParamName);
        Assert.Equal("runId", blank.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_rate_that_does_not_follow_the_failed_cases()
    {
        var mismatch = Assert.Throws<ArgumentException>(
            () => Snapshot(2, 1, 0d, Counts(ungrounded: 1)));
        var empty = Assert.Throws<ArgumentException>(
            () => Snapshot(0, 0, 100d, Counts()));
        var infinite = Assert.Throws<ArgumentOutOfRangeException>(
            () => Snapshot(1, 1, double.PositiveInfinity, Counts(ungrounded: 1)));

        Assert.Equal("failureRate", mismatch.ParamName);
        Assert.Equal("failureRate", empty.ParamName);
        Assert.Equal("failureRate", infinite.ParamName);
    }

    [Fact]
    public void Constructor_rejects_incomplete_or_negative_counts()
    {
        var missing = Assert.Throws<ArgumentException>(
            () => Snapshot(0, 0, 0d, new Dictionary<EvaluationFailureCategory, int>()));
        var negative = Assert.Throws<ArgumentOutOfRangeException>(
            () => Snapshot(1, 1, 100d, Counts(ungrounded: -1)));
        var extra = Assert.Throws<ArgumentException>(
            () => Snapshot(
                0,
                0,
                0d,
                new Dictionary<EvaluationFailureCategory, int>(Counts())
                {
                    [(EvaluationFailureCategory)99] = 1
                }));
        var failed = Assert.Throws<ArgumentOutOfRangeException>(
            () => Snapshot(1, 2, 200d, Counts()));
        var counts = Assert.Throws<ArgumentNullException>(
            () => Snapshot(0, 0, 0d, null!));

        Assert.Equal("categoryCounts", missing.ParamName);
        Assert.Equal("categoryCounts", negative.ParamName);
        Assert.Equal("categoryCounts", extra.ParamName);
        Assert.Equal("failedCases", failed.ParamName);
        Assert.Equal("categoryCounts", counts.ParamName);
    }

    private static EvaluationSnapshot Snapshot(string runId)
    {
        return Snapshot(runId, 0, 0, 0d, Counts());
    }

    private static EvaluationSnapshot Snapshot(
        int totalCases,
        int failedCases,
        double failureRate,
        IReadOnlyDictionary<EvaluationFailureCategory, int> categoryCounts)
    {
        return Snapshot("run-1", totalCases, failedCases, failureRate, categoryCounts);
    }

    private static EvaluationSnapshot Snapshot(
        string runId,
        int totalCases,
        int failedCases,
        double failureRate,
        IReadOnlyDictionary<EvaluationFailureCategory, int> categoryCounts)
    {
        return new EvaluationSnapshot(
            runId,
            EvaluatedAt,
            totalCases,
            failedCases,
            failureRate,
            categoryCounts);
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
}
