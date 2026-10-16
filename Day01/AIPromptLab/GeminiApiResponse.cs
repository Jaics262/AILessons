public sealed class GeminiApiResponse
{
    public List<GeminiCandidate>? Candidates { get; init; }

    public GeminiPromptFeedback? PromptFeedback { get; init; }

    public string ReadModelText()
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
            throw new GeminiClientException(GeminiClientError.EmptyResponse);
        }

        return string.Concat(texts);
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
}

public sealed class GeminiPromptFeedback
{
    public string? BlockReason { get; init; }
}
