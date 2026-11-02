using System.Text;

// Deterministic token vectors for retrieval comparison. This does not call an embedding API.
public sealed class HashedTokenEmbedder : ITextEmbedder
{
    public const int VectorLength = 32;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "be", "for", "how", "in", "is", "must", "of", "on", "or", "should", "the", "to", "with"
    };

    public TextEmbedding Embed(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var vector = new float[VectorLength];
        foreach (var token in Tokenize(text))
        {
            var hash = Hash(token);
            var bucket = (int)(hash % VectorLength);
            vector[bucket] += (hash & 0x80000000) == 0 ? 1f : -1f;
        }

        if (vector.All(value => value == 0f))
        {
            throw new ArgumentException("The text has no tokens to embed.", nameof(text));
        }

        return new TextEmbedding(text, vector);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var current = new StringBuilder();
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(char.ToLowerInvariant(character));
                continue;
            }

            var token = TakeToken(current);
            if (token is not null)
            {
                yield return token;
            }
        }

        var last = TakeToken(current);
        if (last is not null)
        {
            yield return last;
        }
    }

    private static string? TakeToken(StringBuilder current)
    {
        if (current.Length == 0)
        {
            return null;
        }

        var token = current.ToString();
        current.Clear();
        if (token.Length < 2 || StopWords.Contains(token))
        {
            return null;
        }

        return token;
    }

    private static uint Hash(string token)
    {
        uint hash = 2166136261;
        foreach (var character in token)
        {
            hash ^= character;
            hash *= 16777619;
        }

        return hash;
    }
}
