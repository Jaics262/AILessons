// Predefined vectors for tests. This does not call an embedding API.
public sealed class FakeTextEmbedder : ITextEmbedder
{
    public const int VectorLength = 4;

    public const string TemporaryServerFailure = "How do I handle a temporary server failure?";

    public const string TransientHttpError = "What should happen when an HTTP request returns a transient error?";

    public const string DotNetProject = "The project uses .NET 10 and xUnit.";

    public const string RetryAfter = "HTTP retries should respect Retry-After.";

    public const string VerbatimTurns = "Recent conversation turns must remain verbatim.";

    public const string RetryDelayQuestion = "How should the application handle HTTP retry delays?";

    private static readonly IReadOnlyDictionary<string, float[]> KnownVectors = new Dictionary<string, float[]>(StringComparer.Ordinal)
    {
        [TemporaryServerFailure] = [0.90f, 0.10f, 0.00f, 0.00f],
        [TransientHttpError] = [0.88f, 0.12f, 0.02f, 0.00f],
        [DotNetProject] = [0.00f, 0.00f, 1.00f, 0.00f],
        [RetryAfter] = [0.00f, 0.95f, 0.05f, 0.00f],
        [VerbatimTurns] = [0.00f, 0.00f, 0.00f, 1.00f],
        [RetryDelayQuestion] = [0.05f, 0.90f, 0.05f, 0.00f]
    };

    public TextEmbedding Embed(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var phrase = text.Trim();
        if (!KnownVectors.TryGetValue(phrase, out var vector))
        {
            throw new InvalidOperationException($"No embedding is defined for '{phrase}'.");
        }

        return new TextEmbedding(phrase, vector.ToArray());
    }
}
