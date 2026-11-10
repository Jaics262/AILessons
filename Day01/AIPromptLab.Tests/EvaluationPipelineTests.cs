namespace AIPromptLab.Tests;

public sealed class EvaluationPipelineTests
{
    [Fact]
    public void Evaluate_scores_the_fixed_set_and_fails_the_default_threshold()
    {
        var report = EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases);

        Assert.False(report.Passed);
        Assert.Equal(6, report.TotalCases);
        Assert.Equal(2, report.PassedCases);
        Assert.Equal(4, report.FailedCases);
        Assert.Same(EvaluationThresholds.Default, report.Thresholds);
        Assert.Equal(
            AnswerEvaluationSet.Cases.Select(item => item.ExpectedResult == AnswerEvaluationOutcome.Pass).ToArray(),
            report.Results.Select(item => item.Passed).ToArray());
        Assert.Equal(
            [
                "The claim 'all requests are retried five times' is not supported by the context."
            ],
            Result(report, AnswerEvaluationSet.UnsupportedClaim).FailureReasons);
        Assert.Equal(
            [EvaluationFailureCode.UngroundedClaim],
            Codes(report, AnswerEvaluationSet.UnsupportedClaim));
        Assert.Equal(
            [EvaluationFailureCategory.UngroundedClaim],
            Result(report, AnswerEvaluationSet.UnsupportedClaim).FailureCategories);
        Assert.Equal(
            [
                "The answer misses the expected fact 'HTTP retries respect the Retry-After header'."
            ],
            Result(report, AnswerEvaluationSet.MissingExpectedFact).FailureReasons);
        Assert.Equal(
            [EvaluationFailureCode.MissingExpectedFact],
            Codes(report, AnswerEvaluationSet.MissingExpectedFact));
        Assert.Equal(
            [EvaluationFailureCategory.MissingExpectedFact],
            Result(report, AnswerEvaluationSet.MissingExpectedFact).FailureCategories);
        Assert.Equal(
            [
                "The claim 'The application uses .NET 8' is not supported by the context.",
                "The answer misses the expected fact 'The application uses .NET 10'."
            ],
            Result(report, AnswerEvaluationSet.WrongFact).FailureReasons);
        Assert.Equal(
            [EvaluationFailureCode.UngroundedClaim, EvaluationFailureCode.MissingExpectedFact],
            Codes(report, AnswerEvaluationSet.WrongFact));
        Assert.Equal(
            [EvaluationFailureCategory.UngroundedClaim, EvaluationFailureCategory.MissingExpectedFact],
            Result(report, AnswerEvaluationSet.WrongFact).FailureCategories);
        Assert.Equal(
            ["The answer is empty."],
            Result(report, AnswerEvaluationSet.EmptyAnswer).FailureReasons);
        Assert.Equal(
            [EvaluationFailureCode.EmptyAnswer],
            Codes(report, AnswerEvaluationSet.EmptyAnswer));
        Assert.Equal(
            [EvaluationFailureCategory.EmptyAnswer],
            Result(report, AnswerEvaluationSet.EmptyAnswer).FailureCategories);
        Assert.Empty(Result(report, AnswerEvaluationSet.CorrectAndGrounded).FailureReasons);
        Assert.Empty(Result(report, AnswerEvaluationSet.DifferentWording).FailureReasons);
        Assert.Equal(
            string.Join(
                '\n',
                "Evaluation: FAIL",
                "Pass rate: 0.333 (minimum 1.000)",
                "Passed: 2",
                "Failed: 4",
                "Total: 6",
                "Correct and grounded answer: PASS",
                "Answer contains an unsupported claim: FAIL",
                "- The claim 'all requests are retried five times' is not supported by the context.",
                "Answer misses an important expected fact: FAIL",
                "- The answer misses the expected fact 'HTTP retries respect the Retry-After header'.",
                "Relevant answer with the wrong fact: FAIL",
                "- The claim 'The application uses .NET 8' is not supported by the context.",
                "- The answer misses the expected fact 'The application uses .NET 10'.",
                "Empty answer: FAIL",
                "- The answer is empty.",
                "Fully correct answer with different wording: PASS"),
            report.Report);
        Assert.Equal(report.Report, report.ToString());
    }

    [Fact]
    public void Evaluate_passes_when_the_pass_rate_meets_a_lower_threshold()
    {
        var thresholds = new EvaluationThresholds(2d / 6d);

        var report = EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases, thresholds);

        Assert.True(report.Passed);
        Assert.Equal(2, report.PassedCases);
        Assert.Equal(4, report.FailedCases);
        Assert.Same(thresholds, report.Thresholds);
        Assert.StartsWith("Evaluation: PASS\nPass rate: 0.333 (minimum 0.333)", report.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_fails_when_the_pass_rate_misses_the_threshold()
    {
        var report = EvaluationPipeline.Evaluate(
            AnswerEvaluationSet.PipelineCases,
            new EvaluationThresholds((2d / 6d) + 0.001d));

        Assert.False(report.Passed);
        Assert.Equal(2, report.PassedCases);
    }

    [Fact]
    public void Evaluate_passes_the_two_correct_answers_at_the_default_threshold()
    {
        var cases = AnswerEvaluationSet.PipelineCases
            .Where(item => item.Id is AnswerEvaluationSet.CorrectAndGrounded or AnswerEvaluationSet.DifferentWording)
            .ToArray();

        var report = EvaluationPipeline.Evaluate(cases);

        Assert.True(report.Passed);
        Assert.Equal(2, report.TotalCases);
        Assert.Equal(2, report.PassedCases);
        Assert.Equal(0, report.FailedCases);
        Assert.All(report.Results, result => Assert.True(result.Passed));
    }

    [Fact]
    public void Evaluate_passes_a_failed_case_when_the_threshold_is_zero()
    {
        var report = EvaluationPipeline.Evaluate(
            [Case(AnswerEvaluationSet.EmptyAnswer)],
            new EvaluationThresholds(0d));

        Assert.True(report.Passed);
        Assert.Equal(0, report.PassedCases);
        Assert.Equal(1, report.FailedCases);
        Assert.False(Assert.Single(report.Results).Passed);
    }

    [Fact]
    public void Evaluate_repeats_the_same_report_for_the_fixed_set()
    {
        var first = EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases);
        var second = EvaluationPipeline.Evaluate(AnswerEvaluationSet.PipelineCases);

        Assert.Equal(first.Report, second.Report);
        Assert.Equal(
            first.Results.Select(item => item.FailureReasons.ToArray()).ToArray(),
            second.Results.Select(item => item.FailureReasons.ToArray()).ToArray());
        Assert.Equal(
            first.Results.Select(item => item.Failures.Select(failure => failure.Code).ToArray()).ToArray(),
            second.Results.Select(item => item.Failures.Select(failure => failure.Code).ToArray()).ToArray());
        Assert.Equal(
            first.Results.Select(item => item.FailureCategories.ToArray()).ToArray(),
            second.Results.Select(item => item.FailureCategories.ToArray()).ToArray());
    }

    [Fact]
    public void Evaluate_does_not_treat_keyword_overlap_as_an_expected_fact()
    {
        var report = EvaluationPipeline.Evaluate(
        [
            new EvaluationCase(
                "keyword-overlap",
                AnswerSupportExample.Question,
                "Someone mentioned .NET 10 and Retry-After.",
                AnswerSupportExample.SupportedAnswer,
                AnswerEvaluationSet.ExpectedFacts)
        ]);

        var result = Assert.Single(report.Results);
        Assert.False(result.Passed);
        Assert.Equal(
            [
                "The claim 'The application uses .NET 10' is not supported by the context.",
                "The claim 'HTTP retries respect the Retry-After header' is not supported by the context."
            ],
            result.FailureReasons);
        Assert.Equal(
            [EvaluationFailureCode.UngroundedClaim, EvaluationFailureCode.UngroundedClaim],
            result.Failures.Select(failure => failure.Code).ToArray());
        Assert.Equal(
            [EvaluationFailureCategory.UngroundedClaim, EvaluationFailureCategory.UngroundedClaim],
            result.FailureCategories);
    }

    [Fact]
    public void Evaluate_records_an_answer_that_has_no_claim()
    {
        var report = EvaluationPipeline.Evaluate(
        [
            new EvaluationCase(
                "comma",
                AnswerSupportExample.Question,
                AnswerSupportExample.Context,
                ",",
                AnswerEvaluationSet.ExpectedFacts)
        ]);

        var result = Assert.Single(report.Results);
        Assert.Equal(["The answer must contain at least one claim."], result.FailureReasons);
        Assert.Equal([EvaluationFailureCode.NoClaim], result.Failures.Select(failure => failure.Code).ToArray());
        Assert.Equal([EvaluationFailureCategory.Other], result.FailureCategories);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_records_a_blank_answer_as_empty(string answer)
    {
        var report = EvaluationPipeline.Evaluate(
        [
            new EvaluationCase(
                "blank",
                AnswerSupportExample.Question,
                AnswerSupportExample.Context,
                answer,
                AnswerEvaluationSet.ExpectedFacts)
        ]);

        var result = Assert.Single(report.Results);
        Assert.Equal(["The answer is empty."], result.FailureReasons);
        Assert.Equal(EvaluationFailureCategory.EmptyAnswer, Assert.Single(result.FailureCategories));
    }

    [Fact]
    public void Evaluate_fails_an_empty_case_list()
    {
        var report = EvaluationPipeline.Evaluate([]);

        Assert.False(report.Passed);
        Assert.Equal(0, report.TotalCases);
        Assert.Equal(0, report.PassedCases);
        Assert.Equal(0, report.FailedCases);
        Assert.Empty(report.Results);
        Assert.Equal(
            string.Join(
                '\n',
                "Evaluation: FAIL",
                "The evaluation dataset is empty.",
                "Passed: 0",
                "Failed: 0",
                "Total: 0"),
            report.Report);
    }

    [Fact]
    public void Evaluate_rejects_a_null_case_list_and_a_null_case()
    {
        var missing = Assert.Throws<ArgumentNullException>(() => EvaluationPipeline.Evaluate(null!));
        var nullCase = Assert.Throws<ArgumentException>(
            () => EvaluationPipeline.Evaluate([null!]));

        Assert.Equal("cases", missing.ParamName);
        Assert.Equal("cases", nullCase.ParamName);
    }

    [Fact]
    public void Evaluate_rejects_a_duplicate_case_id()
    {
        var item = Case(AnswerEvaluationSet.CorrectAndGrounded);

        var exception = Assert.Throws<ArgumentException>(() => EvaluationPipeline.Evaluate([item, item]));

        Assert.Equal("cases", exception.ParamName);
        Assert.Contains(AnswerEvaluationSet.CorrectAndGrounded, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_copies_the_expected_facts()
    {
        var facts = new List<string> { AnswerEvaluationSet.FrameworkFact, AnswerEvaluationSet.RetryFact };

        var item = new EvaluationCase(
            "copy",
            AnswerSupportExample.Question,
            AnswerSupportExample.Context,
            AnswerSupportExample.SupportedAnswer,
            facts);
        facts[0] = "changed";

        Assert.Equal(
            [AnswerEvaluationSet.FrameworkFact, AnswerEvaluationSet.RetryFact],
            item.ExpectedFacts.ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_id(string? id)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => Create(id: id!));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_null_answer_and_missing_facts()
    {
        var answer = Assert.Throws<ArgumentNullException>(() => Create(answer: null!));
        var facts = Assert.Throws<ArgumentNullException>(
            () => new EvaluationCase(
                "case",
                AnswerSupportExample.Question,
                AnswerSupportExample.Context,
                AnswerSupportExample.SupportedAnswer,
                null!));
        var empty = Assert.Throws<ArgumentException>(() => Create(expectedFacts: []));

        Assert.Equal("answer", answer.ParamName);
        Assert.Equal("expectedFacts", facts.ParamName);
        Assert.Equal("expectedFacts", empty.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_blank_or_duplicate_expected_fact()
    {
        var blank = Assert.Throws<ArgumentException>(
            () => Create(expectedFacts: [AnswerEvaluationSet.FrameworkFact, " "]));
        var duplicate = Assert.Throws<ArgumentException>(
            () => Create(expectedFacts: [AnswerEvaluationSet.FrameworkFact, AnswerEvaluationSet.FrameworkFact + "."]));

        Assert.Equal("expectedFacts", blank.ParamName);
        Assert.Equal("expectedFacts", duplicate.ParamName);
    }

    [Fact]
    public void Report_rejects_a_pass_flag_that_disagrees_with_the_threshold()
    {
        var result = new EvaluationCaseResult("Empty answer", false, [EvaluationFailure.EmptyAnswer()]);

        var exception = Assert.Throws<ArgumentException>(
            () => new EvaluationReport(1, 0, 1, true, [result], EvaluationThresholds.Default));

        Assert.Equal("passed", exception.ParamName);
    }

    private static EvaluationCase Case(string id)
    {
        return Assert.Single(AnswerEvaluationSet.PipelineCases, item => item.Id == id);
    }

    private static EvaluationCaseResult Result(EvaluationReport report, string id)
    {
        return Assert.Single(report.Results, item => item.CaseId == id);
    }

    private static EvaluationFailureCode[] Codes(EvaluationReport report, string id)
    {
        return Result(report, id).Failures.Select(failure => failure.Code).ToArray();
    }

    private static EvaluationCase Create(
        string id = "case",
        string question = AnswerSupportExample.Question,
        string context = AnswerSupportExample.Context,
        string answer = AnswerSupportExample.SupportedAnswer,
        IReadOnlyList<string>? expectedFacts = null)
    {
        return new EvaluationCase(
            id,
            question,
            context,
            answer,
            expectedFacts ?? AnswerEvaluationSet.ExpectedFacts);
    }
}
