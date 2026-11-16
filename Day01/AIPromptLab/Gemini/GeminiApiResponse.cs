public sealed class GeminiPromptResult
{
    public GeminiPromptResult(
        LearningSummary? summary,
        IReadOnlyList<GeminiFunctionCall> functionCalls,
        IReadOnlyList<decimal>? totals = null)
    {
        Summary = summary;
        FunctionCalls = functionCalls;
        Totals = totals ?? [];
    }

    public LearningSummary? Summary { get; }

    public IReadOnlyList<GeminiFunctionCall> FunctionCalls { get; }

    public IReadOnlyList<decimal> Totals { get; }
}

public sealed class GeminiApiResponse
{
    public List<GeminiCandidate>? Candidates { get; init; }

    public GeminiPromptFeedback? PromptFeedback { get; init; }

    public string? ReadModelText()
    {
        var parts = RequireParts();
        var texts = new List<string>();
        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part.Text))
            {
                texts.Add(part.Text);
            }
        }

        if (texts.Count == 0)
        {
            if (parts.Exists(part => part.FunctionCall is not null))
            {
                return null;
            }

            throw new GeminiClientException(GeminiClientError.EmptyResponse);
        }

        return string.Concat(texts);
    }

    public IReadOnlyList<GeminiPart> ReadParts()
    {
        return RequireParts();
    }

    public IReadOnlyList<GeminiFunctionCall> ReadFunctionCalls()
    {
        var calls = new List<GeminiFunctionCall>();
        foreach (var part in RequireParts())
        {
            if (part.FunctionCall is not null)
            {
                calls.Add(part.FunctionCall);
            }
        }

        return calls;
    }

    private List<GeminiPart> RequireParts()
    {
        if (PromptFeedback?.BlockReason is not null)
        {
            throw new GeminiClientException(GeminiClientError.EmptyResponse);
        }

        if (Candidates is not { Count: > 0 } ||
            Candidates[0].Content?.Parts is not { } parts)
        {
            throw new GeminiClientException(GeminiClientError.EmptyResponse);
        }

        return parts;
    }
}

public sealed class GeminiCandidate
{
    public GeminiContent? Content { get; init; }
}

public sealed class GeminiContent
{
    public List<GeminiPart>? Parts { get; init; }
}

public sealed class GeminiPart
{
    public string? Text { get; init; }

    public string? ThoughtSignature { get; init; }

    public GeminiFunctionCall? FunctionCall { get; init; }
}

public sealed class GeminiFunctionCall
{
    public string? Id { get; init; }

    public string? Name { get; init; }

    public System.Text.Json.JsonElement Args { get; init; }
}

public sealed class GeminiPromptFeedback
{
    public string? BlockReason { get; init; }
}
