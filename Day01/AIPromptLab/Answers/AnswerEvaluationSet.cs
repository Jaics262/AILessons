// Fixed answers about the lab's framework and retry behavior.
// Labels are the expected evaluation result. Nothing here calls Gemini.
public static class AnswerEvaluationSet
{
    public const string CorrectAndGrounded = "Correct and grounded answer";

    public const string UnsupportedClaim = "Answer contains an unsupported claim";

    public const string MissingExpectedFact = "Answer misses an important expected fact";

    public const string WrongFact = "Relevant answer with the wrong fact";

    public const string EmptyAnswer = "Empty answer";

    public const string DifferentWording = "Fully correct answer with different wording";

    public const string FrameworkFact = "The application uses .NET 10";

    public const string RetryFact = "HTTP retries respect the Retry-After header";

    public const string RetryFactAlias = "retries respect Retry-After";

    public static IReadOnlyList<string> ExpectedFacts { get; } =
    [
        FrameworkFact,
        RetryFact
    ];

    public static IReadOnlyList<AnswerEvaluationCase> Cases { get; } =
    [
        new(
            CorrectAndGrounded,
            AnswerSupportExample.Question,
            AnswerSupportExample.Context,
            AnswerSupportExample.SupportedAnswer,
            AnswerEvaluationOutcome.Pass),
        new(
            UnsupportedClaim,
            AnswerSupportExample.Question,
            AnswerSupportExample.Context,
            AnswerSupportExample.UnsupportedAnswer,
            AnswerEvaluationOutcome.Fail),
        new(
            MissingExpectedFact,
            AnswerSupportExample.Question,
            AnswerSupportExample.Context,
            "The application uses .NET 10.",
            AnswerEvaluationOutcome.Fail),
        new(
            WrongFact,
            AnswerSupportExample.Question,
            AnswerSupportExample.Context,
            "The application uses .NET 8, and HTTP retries respect the Retry-After header.",
            AnswerEvaluationOutcome.Fail),
        new(
            EmptyAnswer,
            AnswerSupportExample.Question,
            AnswerSupportExample.Context,
            "",
            AnswerEvaluationOutcome.Fail),
        new(
            DifferentWording,
            AnswerSupportExample.Question,
            AnswerSupportExample.Context,
            "The application uses .NET 10, and retries respect Retry-After.",
            AnswerEvaluationOutcome.Pass)
    ];

    public static IReadOnlyList<EvaluationCase> PipelineCases { get; } = CreatePipelineCases();

    private static EvaluationCase[] CreatePipelineCases()
    {
        var cases = new EvaluationCase[Cases.Count];
        for (var index = 0; index < Cases.Count; index++)
        {
            var item = Cases[index];
            cases[index] = new EvaluationCase(
                item.Scenario,
                item.Question,
                item.Context,
                item.Answer,
                ExpectedFacts);
        }

        return cases;
    }
}
