namespace AIPromptLab.Tests;

public sealed class EvaluationFailureClassifierTests
{
    [Theory]
    [InlineData(EvaluationFailureCode.UngroundedClaim, EvaluationFailureCategory.UngroundedClaim)]
    [InlineData(EvaluationFailureCode.MissingExpectedFact, EvaluationFailureCategory.MissingExpectedFact)]
    [InlineData(EvaluationFailureCode.EmptyAnswer, EvaluationFailureCategory.EmptyAnswer)]
    [InlineData(EvaluationFailureCode.NoClaim, EvaluationFailureCategory.Other)]
    public void Classify_maps_a_code_without_reading_a_message(
        EvaluationFailureCode code,
        EvaluationFailureCategory category)
    {
        Assert.Equal(category, EvaluationFailureClassifier.Classify(code));
    }

    [Fact]
    public void Classify_uses_the_code_when_the_readable_reasons_differ()
    {
        var failures = new EvaluationFailure[]
        {
            EvaluationFailure.UngroundedClaim("all requests are retried five times"),
            EvaluationFailure.UngroundedClaim("The application uses .NET 8"),
            EvaluationFailure.MissingExpectedFact(AnswerEvaluationSet.RetryFact),
            EvaluationFailure.EmptyAnswer(),
            EvaluationFailure.NoClaim()
        };

        Assert.Equal(
            [
                EvaluationFailureCategory.UngroundedClaim,
                EvaluationFailureCategory.UngroundedClaim,
                EvaluationFailureCategory.MissingExpectedFact,
                EvaluationFailureCategory.EmptyAnswer,
                EvaluationFailureCategory.Other
            ],
            EvaluationFailureClassifier.Classify(failures));
        Assert.Equal(
            [
                "The claim 'all requests are retried five times' is not supported by the context.",
                "The claim 'The application uses .NET 8' is not supported by the context.",
                "The answer misses the expected fact 'HTTP retries respect the Retry-After header'.",
                "The answer is empty.",
                "The answer must contain at least one claim."
            ],
            failures.Select(failure => failure.Reason).ToArray());
    }

    [Fact]
    public void Classify_rejects_an_undefined_code_and_a_null_failure()
    {
        var code = Assert.Throws<ArgumentOutOfRangeException>(
            () => EvaluationFailureClassifier.Classify((EvaluationFailureCode)99));
        var missing = Assert.Throws<ArgumentNullException>(
            () => EvaluationFailureClassifier.Classify((IReadOnlyList<EvaluationFailure>)null!));
        var nullFailure = Assert.Throws<ArgumentException>(
            () => EvaluationFailureClassifier.Classify([null!]));

        Assert.Equal("code", code.ParamName);
        Assert.Equal("failures", missing.ParamName);
        Assert.Equal("failures", nullFailure.ParamName);
    }

    [Fact]
    public void Constructor_rejects_an_undefined_code_and_a_blank_reason()
    {
        var code = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EvaluationFailure((EvaluationFailureCode)99, "The answer is empty."));
        var reason = Assert.ThrowsAny<ArgumentException>(
            () => new EvaluationFailure(EvaluationFailureCode.EmptyAnswer, " "));

        Assert.Equal("code", code.ParamName);
        Assert.Equal("reason", reason.ParamName);
    }

    [Fact]
    public void Case_result_copies_failures_and_classifies_them()
    {
        var failures = new List<EvaluationFailure> { EvaluationFailure.EmptyAnswer() };

        var result = new EvaluationCaseResult("Empty answer", false, failures);
        failures[0] = EvaluationFailure.NoClaim();

        Assert.Equal([EvaluationFailureCode.EmptyAnswer], result.Failures.Select(failure => failure.Code).ToArray());
        Assert.Equal(["The answer is empty."], result.FailureReasons);
        Assert.Equal([EvaluationFailureCategory.EmptyAnswer], result.FailureCategories);
    }

    [Fact]
    public void Case_result_rejects_a_null_failure()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new EvaluationCaseResult("case", false, [null!]));

        Assert.Equal("failures", exception.ParamName);
    }
}
