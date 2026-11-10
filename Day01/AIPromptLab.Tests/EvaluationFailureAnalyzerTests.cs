namespace AIPromptLab.Tests;

public sealed class EvaluationFailureAnalyzerTests
{
    [Fact]
    public void Analyze_counts_failures_and_breaks_ties_toward_the_earliest_category()
    {
        var analysis = EvaluationFailureAnalyzer.Analyze(
            EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases));

        Assert.Equal(6, analysis.TotalCases);
        Assert.Equal(4, analysis.FailedCases);
        Assert.Equal((double)4 / 6 * 100d, analysis.FailureRate);
        Assert.Equal(
            [
                Pair(EvaluationFailureCategory.UngroundedClaim, 2),
                Pair(EvaluationFailureCategory.MissingExpectedFact, 2),
                Pair(EvaluationFailureCategory.EmptyAnswer, 1),
                Pair(EvaluationFailureCategory.Other, 0)
            ],
            analysis.CategoryCounts.ToArray());
        Assert.Equal(EvaluationFailureCategory.UngroundedClaim, analysis.MostCommonCategory);
        Assert.Equal(
            string.Join(
                '\n',
                "Failure analysis:",
                "Total: 6",
                "Failed: 4",
                "Failure rate: 66.667",
                "UngroundedClaim: 2",
                "MissingExpectedFact: 2",
                "EmptyAnswer: 1",
                "Other: 0",
                "Most common: UngroundedClaim"),
            analysis.Report);
        Assert.Equal(analysis.Report, analysis.ToString());
    }

    [Fact]
    public void Analyze_returns_a_zero_failure_rate_when_no_case_fails()
    {
        var analysis = EvaluationFailureAnalyzer.Analyze(EvaluationPipeline.Evaluate(
        [
            Case(AnswerEvaluationSet.CorrectAndGrounded),
            Case(AnswerEvaluationSet.DifferentWording)
        ]));

        Assert.Equal(2, analysis.TotalCases);
        Assert.Equal(0, analysis.FailedCases);
        Assert.Equal(0d, analysis.FailureRate);
        Assert.All(analysis.CategoryCounts.Values, count => Assert.Equal(0, count));
        Assert.Null(analysis.MostCommonCategory);
        Assert.StartsWith("Failure analysis: no failures.", analysis.Report, StringComparison.Ordinal);
        Assert.Contains("Failure rate: 0.000", analysis.Report, StringComparison.Ordinal);
        Assert.EndsWith("Most common: none", analysis.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_returns_a_100_percent_failure_rate_when_every_case_fails()
    {
        var analysis = EvaluationFailureAnalyzer.Analyze(EvaluationPipeline.Evaluate(
        [
            Case(AnswerEvaluationSet.UnsupportedClaim),
            Case(AnswerEvaluationSet.MissingExpectedFact),
            Case(AnswerEvaluationSet.WrongFact),
            Case(AnswerEvaluationSet.EmptyAnswer)
        ]));

        Assert.Equal(4, analysis.TotalCases);
        Assert.Equal(4, analysis.FailedCases);
        Assert.Equal(100d, analysis.FailureRate);
        Assert.NotNull(analysis.MostCommonCategory);
        Assert.Contains("Failure rate: 100.000", analysis.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_identifies_the_category_that_occurs_most_often()
    {
        var analysis = EvaluationFailureAnalyzer.Analyze(EvaluationPipeline.Evaluate(
        [
            Case(AnswerEvaluationSet.UnsupportedClaim),
            new EvaluationCase(
                "second unsupported claim",
                AnswerSupportExample.Question,
                AnswerSupportExample.Context,
                AnswerSupportExample.UnsupportedAnswer,
                AnswerEvaluationSet.ExpectedFacts),
            Case(AnswerEvaluationSet.EmptyAnswer)
        ]));

        Assert.Equal(2, analysis.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(1, analysis.CategoryCounts[EvaluationFailureCategory.EmptyAnswer]);
        Assert.Equal(0, analysis.CategoryCounts[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(0, analysis.CategoryCounts[EvaluationFailureCategory.Other]);
        Assert.All(
            analysis.CategoryCounts,
            pair => Assert.True(
                pair.Key == EvaluationFailureCategory.UngroundedClaim
                || pair.Value < analysis.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]));
        Assert.Equal(EvaluationFailureCategory.UngroundedClaim, analysis.MostCommonCategory);
    }

    // Category counts are failure occurrences. FailedCases counts cases.
    [Fact]
    public void Analyze_counts_failure_occurrences_when_one_case_has_multiple_categories()
    {
        var mixed = EvaluationFailureAnalyzer.Analyze(EvaluationPipeline.Evaluate(
        [
            Case(AnswerEvaluationSet.WrongFact)
        ]));
        var repeated = EvaluationFailureAnalyzer.Analyze(EvaluationPipeline.Evaluate(
        [
            new EvaluationCase(
                "two unsupported claims",
                AnswerSupportExample.Question,
                "Someone mentioned .NET 10 and Retry-After.",
                AnswerSupportExample.SupportedAnswer,
                AnswerEvaluationSet.ExpectedFacts)
        ]));

        Assert.Equal(1, mixed.TotalCases);
        Assert.Equal(1, mixed.FailedCases);
        Assert.Equal(1, mixed.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(1, mixed.CategoryCounts[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(0, mixed.CategoryCounts[EvaluationFailureCategory.EmptyAnswer]);
        Assert.Equal(0, mixed.CategoryCounts[EvaluationFailureCategory.Other]);

        Assert.Equal(1, repeated.TotalCases);
        Assert.Equal(1, repeated.FailedCases);
        Assert.Equal(100d, repeated.FailureRate);
        Assert.Equal(2, repeated.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(0, repeated.CategoryCounts[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(EvaluationFailureCategory.UngroundedClaim, repeated.MostCommonCategory);
    }

    [Fact]
    public void Analyze_keeps_a_zero_rate_when_the_dataset_is_empty()
    {
        var analysis = EvaluationFailureAnalyzer.Analyze(EvaluationPipeline.Evaluate([]));

        Assert.Equal(0, analysis.TotalCases);
        Assert.Equal(0, analysis.FailedCases);
        Assert.Equal(0d, analysis.FailureRate);
        Assert.Null(analysis.MostCommonCategory);
        Assert.Equal(4, analysis.CategoryCounts.Count);
        Assert.All(analysis.CategoryCounts.Values, count => Assert.Equal(0, count));
        Assert.Equal(
            string.Join(
                '\n',
                "Failure analysis: no cases.",
                "Total: 0",
                "Failed: 0",
                "Failure rate: 0.000",
                "UngroundedClaim: 0",
                "MissingExpectedFact: 0",
                "EmptyAnswer: 0",
                "Other: 0",
                "Most common: none"),
            analysis.Report);
    }

    [Fact]
    public void Analyze_repeats_the_same_analysis_for_the_fixed_set()
    {
        var report = EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases);

        var first = EvaluationFailureAnalyzer.Analyze(report);
        var second = EvaluationFailureAnalyzer.Analyze(report);

        Assert.Equal(first.TotalCases, second.TotalCases);
        Assert.Equal(first.FailedCases, second.FailedCases);
        Assert.Equal(first.FailureRate, second.FailureRate);
        Assert.Equal(first.CategoryCounts.ToArray(), second.CategoryCounts.ToArray());
        Assert.Equal(first.MostCommonCategory, second.MostCommonCategory);
        Assert.Equal(first.Report, second.Report);
    }

    [Fact]
    public void Analyze_rejects_a_null_report()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => EvaluationFailureAnalyzer.Analyze(null!));

        Assert.Equal("report", exception.ParamName);
    }

    [Fact]
    public void Constructor_copies_category_counts()
    {
        var counts = Counts(ungrounded: 1);

        var analysis = new EvaluationFailureAnalysis(
            1,
            1,
            100d,
            counts,
            EvaluationFailureCategory.UngroundedClaim);
        counts[EvaluationFailureCategory.UngroundedClaim] = 4;

        Assert.Equal(1, analysis.CategoryCounts[EvaluationFailureCategory.UngroundedClaim]);
    }

    [Fact]
    public void Constructor_rejects_a_rate_that_does_not_follow_the_failed_cases()
    {
        var mismatch = Assert.Throws<ArgumentException>(
            () => new EvaluationFailureAnalysis(2, 1, 0d, Counts(ungrounded: 1), EvaluationFailureCategory.UngroundedClaim));
        var empty = Assert.Throws<ArgumentException>(
            () => new EvaluationFailureAnalysis(0, 0, 100d, Counts(), null));
        var infinite = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EvaluationFailureAnalysis(1, 1, double.PositiveInfinity, Counts(ungrounded: 1), EvaluationFailureCategory.UngroundedClaim));

        Assert.Equal("failureRate", mismatch.ParamName);
        Assert.Equal("failureRate", empty.ParamName);
        Assert.Equal("failureRate", infinite.ParamName);
    }

    [Fact]
    public void Constructor_rejects_counts_that_disagree_with_the_most_common_category()
    {
        var tiedLater = Assert.Throws<ArgumentException>(
            () => new EvaluationFailureAnalysis(
                2,
                2,
                100d,
                Counts(empty: 1, other: 1),
                EvaluationFailureCategory.Other));
        var none = Assert.Throws<ArgumentException>(
            () => new EvaluationFailureAnalysis(1, 1, 100d, Counts(ungrounded: 1), null));
        var whenEmpty = Assert.Throws<ArgumentException>(
            () => new EvaluationFailureAnalysis(0, 0, 0d, Counts(), EvaluationFailureCategory.EmptyAnswer));
        var undefined = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EvaluationFailureAnalysis(0, 0, 0d, Counts(), (EvaluationFailureCategory)99));

        Assert.Equal("mostCommonCategory", tiedLater.ParamName);
        Assert.Equal("mostCommonCategory", none.ParamName);
        Assert.Equal("mostCommonCategory", whenEmpty.ParamName);
        Assert.Equal("mostCommonCategory", undefined.ParamName);
    }

    [Fact]
    public void Constructor_rejects_incomplete_or_negative_counts()
    {
        var missing = Assert.Throws<ArgumentException>(
            () => new EvaluationFailureAnalysis(
                0,
                0,
                0d,
                new Dictionary<EvaluationFailureCategory, int>(),
                null));
        var negative = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EvaluationFailureAnalysis(1, 1, 100d, Counts(ungrounded: -1), null));
        var extra = Assert.Throws<ArgumentException>(
            () => new EvaluationFailureAnalysis(
                0,
                0,
                0d,
                new Dictionary<EvaluationFailureCategory, int>(Counts())
                {
                    [(EvaluationFailureCategory)99] = 1
                },
                null));
        var failed = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EvaluationFailureAnalysis(1, 2, 200d, Counts(), null));

        Assert.Equal("categoryCounts", missing.ParamName);
        Assert.Equal("categoryCounts", negative.ParamName);
        Assert.Equal("categoryCounts", extra.ParamName);
        Assert.Equal("failedCases", failed.ParamName);
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
        int count)
    {
        return new KeyValuePair<EvaluationFailureCategory, int>(category, count);
    }
}
