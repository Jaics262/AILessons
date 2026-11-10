// One fixed answer and the pass or fail label the evaluation set expects.
public sealed record AnswerEvaluationCase(
    string Scenario,
    string Question,
    string Context,
    string Answer,
    AnswerEvaluationOutcome ExpectedResult)
{
    public string Scenario { get; init; } = RequireScenario(Scenario);

    public string Question { get; init; } = RequireQuestion(Question);

    public string Context { get; init; } = RequireContext(Context);

    public string Answer { get; init; } = RequireAnswer(Answer);

    public AnswerEvaluationOutcome ExpectedResult { get; init; } = RequireOutcome(ExpectedResult);

    private static string RequireScenario(string scenario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        return scenario.Trim();
    }

    private static string RequireQuestion(string question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        return question.Trim();
    }

    private static string RequireContext(string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        return context.Trim();
    }

    private static string RequireAnswer(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return answer;
    }

    private static AnswerEvaluationOutcome RequireOutcome(AnswerEvaluationOutcome expectedResult)
    {
        if (!Enum.IsDefined(expectedResult))
        {
            throw new ArgumentOutOfRangeException(nameof(expectedResult), "Expected result must be Pass or Fail.");
        }

        return expectedResult;
    }
}
