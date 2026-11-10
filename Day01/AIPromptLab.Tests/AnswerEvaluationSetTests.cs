namespace AIPromptLab.Tests;

public sealed class AnswerEvaluationSetTests
{
    [Fact]
    public void Cases_cover_the_six_fixed_scenarios()
    {
        var cases = AnswerEvaluationSet.Cases;

        Assert.Equal(6, cases.Count);
        Assert.Equal(
            [
                AnswerEvaluationSet.CorrectAndGrounded,
                AnswerEvaluationSet.UnsupportedClaim,
                AnswerEvaluationSet.MissingExpectedFact,
                AnswerEvaluationSet.WrongFact,
                AnswerEvaluationSet.EmptyAnswer,
                AnswerEvaluationSet.DifferentWording
            ],
            cases.Select(item => item.Scenario));
        Assert.Equal(cases.Count, cases.Select(item => item.Scenario).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cases, item => Assert.Equal(AnswerSupportExample.Question, item.Question));
        Assert.All(cases, item => Assert.Equal(AnswerSupportExample.Context, item.Context));
        Assert.Equal(
            [AnswerEvaluationSet.FrameworkFact, AnswerEvaluationSet.RetryFact],
            AnswerEvaluationSet.ExpectedFacts.ToArray());
        Assert.Equal(
            cases.Select(item => item.Scenario).ToArray(),
            AnswerEvaluationSet.PipelineCases.Select(item => item.Id).ToArray());
        Assert.Equal(
            cases.Select(item => item.Answer).ToArray(),
            AnswerEvaluationSet.PipelineCases.Select(item => item.Answer).ToArray());
        Assert.All(
            AnswerEvaluationSet.PipelineCases,
            item => Assert.Equal(
                [AnswerEvaluationSet.FrameworkFact, AnswerEvaluationSet.RetryFact],
                item.ExpectedFacts.ToArray()));
    }

    [Fact]
    public void Correct_grounded_answer_passes()
    {
        var item = Case(AnswerEvaluationSet.CorrectAndGrounded);

        Assert.Equal(AnswerEvaluationOutcome.Pass, item.ExpectedResult);
        Assert.Equal(AnswerSupportExample.SupportedAnswer, item.Answer);
        Assert.Contains(AnswerEvaluationSet.FrameworkFact, item.Answer, StringComparison.Ordinal);
        Assert.Contains(AnswerEvaluationSet.RetryFact, item.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Unsupported_claim_fails()
    {
        var item = Case(AnswerEvaluationSet.UnsupportedClaim);

        Assert.Equal(AnswerEvaluationOutcome.Fail, item.ExpectedResult);
        Assert.Equal(AnswerSupportExample.UnsupportedAnswer, item.Answer);
        Assert.Contains("all requests are retried five times", item.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_expected_fact_fails()
    {
        var item = Case(AnswerEvaluationSet.MissingExpectedFact);

        Assert.Equal(AnswerEvaluationOutcome.Fail, item.ExpectedResult);
        Assert.Contains(AnswerEvaluationSet.FrameworkFact, item.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain(AnswerEvaluationSet.RetryFact, item.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Retry-After", item.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Relevant_answer_with_the_wrong_fact_fails()
    {
        var item = Case(AnswerEvaluationSet.WrongFact);

        Assert.Equal(AnswerEvaluationOutcome.Fail, item.ExpectedResult);
        Assert.Contains("The application uses .NET 8", item.Answer, StringComparison.Ordinal);
        Assert.Contains(AnswerEvaluationSet.RetryFact, item.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain(AnswerEvaluationSet.FrameworkFact, item.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_answer_fails()
    {
        var item = Case(AnswerEvaluationSet.EmptyAnswer);

        Assert.Equal(AnswerEvaluationOutcome.Fail, item.ExpectedResult);
        Assert.Equal("", item.Answer);
    }

    [Fact]
    public void Different_wording_of_the_correct_answer_passes()
    {
        var item = Case(AnswerEvaluationSet.DifferentWording);
        var correct = Case(AnswerEvaluationSet.CorrectAndGrounded);

        Assert.Equal(AnswerEvaluationOutcome.Pass, item.ExpectedResult);
        Assert.NotEqual(correct.Answer, item.Answer);
        Assert.Contains(AnswerEvaluationSet.FrameworkFact, item.Answer, StringComparison.Ordinal);
        Assert.Contains(AnswerEvaluationSet.RetryFactAlias, item.Answer, StringComparison.Ordinal);
        Assert.Contains(
            new ClaimEvidence(AnswerEvaluationSet.RetryFactAlias, AnswerEvaluationSet.RetryFact + "."),
            AnswerSupportExample.Mappings);
    }

    [Fact]
    public void Cases_keep_the_same_labels_when_read_twice()
    {
        var first = AnswerEvaluationSet.Cases.Select(item => (item.Scenario, item.Answer, item.ExpectedResult));
        var second = AnswerEvaluationSet.Cases.Select(item => (item.Scenario, item.Answer, item.ExpectedResult));

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_scenario(string? scenario)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => Create(scenario: scenario!));

        Assert.Equal("scenario", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_question(string? question)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => Create(question: question!));

        Assert.Equal("question", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_context(string? context)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => Create(context: context!));

        Assert.Equal("context", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_null_answer()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => Create(answer: null!));

        Assert.Equal("answer", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_an_undefined_expected_result()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Create(expectedResult: (AnswerEvaluationOutcome)0));

        Assert.Equal("expectedResult", exception.ParamName);
    }

    [Fact]
    public void Constructor_trims_text_and_keeps_an_empty_answer()
    {
        var item = new AnswerEvaluationCase(
            "  Empty answer  ",
            "  Which framework does the application use?  ",
            "  The application uses .NET 10.  ",
            "",
            AnswerEvaluationOutcome.Fail);

        Assert.Equal("Empty answer", item.Scenario);
        Assert.Equal("Which framework does the application use?", item.Question);
        Assert.Equal("The application uses .NET 10.", item.Context);
        Assert.Equal("", item.Answer);
        Assert.Equal(AnswerEvaluationOutcome.Fail, item.ExpectedResult);
    }

    private static AnswerEvaluationCase Case(string scenario)
    {
        return Assert.Single(AnswerEvaluationSet.Cases, item => item.Scenario == scenario);
    }

    private static AnswerEvaluationCase Create(
        string scenario = AnswerEvaluationSet.CorrectAndGrounded,
        string question = AnswerSupportExample.Question,
        string context = AnswerSupportExample.Context,
        string answer = AnswerSupportExample.SupportedAnswer,
        AnswerEvaluationOutcome expectedResult = AnswerEvaluationOutcome.Pass)
    {
        return new AnswerEvaluationCase(scenario, question, context, answer, expectedResult);
    }
}
