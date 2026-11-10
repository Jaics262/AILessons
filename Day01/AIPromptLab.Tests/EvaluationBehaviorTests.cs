namespace AIPromptLab.Tests;

public sealed class EvaluationBehaviorTests
{
    [Fact]
    public void All_passing_cases_make_the_report_pass()
    {
        var report = EvaluationPipeline.Evaluate(
        [
            Case(AnswerEvaluationSet.CorrectAndGrounded),
            Case(AnswerEvaluationSet.DifferentWording)
        ]);

        Assert.True(report.Passed);
        Assert.Equal(2, report.TotalCases);
        Assert.Equal(2, report.PassedCases);
        Assert.Equal(0, report.FailedCases);
        Assert.All(report.Results, result =>
        {
            Assert.True(result.Passed);
            Assert.Empty(result.FailureReasons);
        });
        Assert.StartsWith("Evaluation: PASS\nPass rate: 1.000 (minimum 1.000)", report.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void One_failing_case_makes_the_report_fail()
    {
        var report = EvaluationPipeline.Evaluate(
        [
            Case(AnswerEvaluationSet.CorrectAndGrounded),
            Case(AnswerEvaluationSet.MissingExpectedFact)
        ]);

        Assert.False(report.Passed);
        Assert.Equal(1, report.PassedCases);
        Assert.Equal(1, report.FailedCases);
        Assert.Empty(Result(report, AnswerEvaluationSet.CorrectAndGrounded).FailureReasons);
        Assert.Equal(
            ["The answer misses the expected fact 'HTTP retries respect the Retry-After header'."],
            Result(report, AnswerEvaluationSet.MissingExpectedFact).FailureReasons);
    }

    [Fact]
    public void Multiple_failures_keep_every_failure_reason()
    {
        var report = EvaluationPipeline.Evaluate(
        [
            Case(AnswerEvaluationSet.UnsupportedClaim),
            Case(AnswerEvaluationSet.MissingExpectedFact),
            Case(AnswerEvaluationSet.WrongFact)
        ]);

        Assert.False(report.Passed);
        Assert.Equal(3, report.FailedCases);
        Assert.Equal(0, report.PassedCases);
        Assert.Equal(
            [
                "The claim 'all requests are retried five times' is not supported by the context.",
                "The answer misses the expected fact 'HTTP retries respect the Retry-After header'.",
                "The claim 'The application uses .NET 8' is not supported by the context.",
                "The answer misses the expected fact 'The application uses .NET 10'."
            ],
            report.Results.SelectMany(result => result.FailureReasons).ToArray());
        Assert.Equal(
            [
                EvaluationFailureCategory.UngroundedClaim,
                EvaluationFailureCategory.MissingExpectedFact,
                EvaluationFailureCategory.UngroundedClaim,
                EvaluationFailureCategory.MissingExpectedFact
            ],
            report.Results.SelectMany(result => result.FailureCategories).ToArray());
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(0d)]
    public void Empty_dataset_fails_explicitly(double minimumPassRate)
    {
        var first = EvaluationPipeline.Evaluate([], new EvaluationThresholds(minimumPassRate));
        var second = EvaluationPipeline.Evaluate([], new EvaluationThresholds(minimumPassRate));

        Assert.False(first.Passed);
        Assert.Equal(0, first.TotalCases);
        Assert.Empty(first.Results);
        Assert.Equal(first.Report, second.Report);
        Assert.Equal(
            string.Join(
                '\n',
                "Evaluation: FAIL",
                "The evaluation dataset is empty.",
                "Passed: 0",
                "Failed: 0",
                "Total: 0"),
            first.Report);
    }

    [Fact]
    public void Same_input_produces_the_same_report()
    {
        var cases = new[]
        {
            Case(AnswerEvaluationSet.CorrectAndGrounded),
            Case(AnswerEvaluationSet.UnsupportedClaim),
            Case(AnswerEvaluationSet.DifferentWording)
        };

        var first = EvaluationPipeline.Evaluate(cases);
        var second = EvaluationPipeline.Evaluate(cases);

        Assert.Equal(first.Report, second.Report);
        Assert.Equal(first.Passed, second.Passed);
        Assert.Equal(first.Results.Select(result => result.CaseId).ToArray(), second.Results.Select(result => result.CaseId).ToArray());
        Assert.Equal(first.Results.Select(result => result.Passed).ToArray(), second.Results.Select(result => result.Passed).ToArray());
        Assert.Equal(
            first.Results.SelectMany(result => result.FailureReasons).ToArray(),
            second.Results.SelectMany(result => result.FailureReasons).ToArray());
        Assert.Equal(
            first.Results.SelectMany(result => result.FailureCategories).ToArray(),
            second.Results.SelectMany(result => result.FailureCategories).ToArray());
    }

    [Fact]
    public void Different_wording_passes_only_through_a_predefined_mapping()
    {
        var mapped = EvaluationPipeline.Evaluate(
        [
            new EvaluationCase(
                "mapped-alias",
                AnswerSupportExample.Question,
                AnswerSupportExample.Context,
                "The application uses .NET 10, and retries respect Retry-After.",
                AnswerEvaluationSet.ExpectedFacts)
        ]);
        var unmapped = EvaluationPipeline.Evaluate(
        [
            new EvaluationCase(
                "unmapped-paraphrase",
                AnswerSupportExample.Question,
                AnswerSupportExample.Context,
                "The application uses .NET 10, and retries honor Retry-After.",
                AnswerEvaluationSet.ExpectedFacts)
        ]);

        Assert.True(Assert.Single(mapped.Results).Passed);
        Assert.True(mapped.Passed);
        Assert.Contains(
            new ClaimEvidence(AnswerEvaluationSet.RetryFactAlias, AnswerEvaluationSet.RetryFact + "."),
            AnswerSupportExample.Mappings);
        var failed = Assert.Single(unmapped.Results);
        Assert.False(unmapped.Passed);
        Assert.False(failed.Passed);
        Assert.Equal(
            [
                "The claim 'retries honor Retry-After' is not supported by the context.",
                "The answer misses the expected fact 'HTTP retries respect the Retry-After header'."
            ],
            failed.FailureReasons);
        Assert.Equal(
            [EvaluationFailureCode.UngroundedClaim, EvaluationFailureCode.MissingExpectedFact],
            failed.Failures.Select(failure => failure.Code).ToArray());
        Assert.Equal(
            [EvaluationFailureCategory.UngroundedClaim, EvaluationFailureCategory.MissingExpectedFact],
            failed.FailureCategories);
    }

    private static EvaluationCase Case(string id)
    {
        return Assert.Single(AnswerEvaluationSet.PipelineCases, item => item.Id == id);
    }

    private static EvaluationCaseResult Result(EvaluationReport report, string id)
    {
        return Assert.Single(report.Results, item => item.CaseId == id);
    }
}
