// A case the evaluation pipeline can score. The gold pass or fail label stays on AnswerEvaluationCase.
public sealed record EvaluationCase(
    string Id,
    string Question,
    string Context,
    string Answer,
    IReadOnlyList<string> ExpectedFacts)
{
    public string Id { get; init; } = RequireId(Id);

    public string Question { get; init; } = RequireQuestion(Question);

    public string Context { get; init; } = RequireContext(Context);

    public string Answer { get; init; } = RequireAnswer(Answer);

    public IReadOnlyList<string> ExpectedFacts { get; init; } = CopyFacts(ExpectedFacts);

    private static string RequireId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return id.Trim();
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

    private static IReadOnlyList<string> CopyFacts(IReadOnlyList<string> expectedFacts)
    {
        ArgumentNullException.ThrowIfNull(expectedFacts);
        if (expectedFacts.Count == 0)
        {
            throw new ArgumentException("At least one expected fact is required.", nameof(expectedFacts));
        }

        var copy = new string[expectedFacts.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < expectedFacts.Count; index++)
        {
            var fact = expectedFacts[index];
            if (string.IsNullOrWhiteSpace(fact))
            {
                throw new ArgumentException(
                    "Expected facts cannot contain null or blank values.",
                    nameof(expectedFacts));
            }

            var trimmed = fact.Trim();
            var normalized = AnswerSupport.NormalizeClaim(trimmed);
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                throw new ArgumentException(
                    "Expected facts must be unique recognizable claims.",
                    nameof(expectedFacts));
            }

            copy[index] = trimmed;
        }

        return copy;
    }
}
