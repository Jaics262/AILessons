using System.Text;

// Keyword overlap only. This is not an embedding or semantic search.
public sealed class KeywordMemoryScorer
{
    public const int ResultLimit = 3;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "be", "for", "how", "in", "is", "must", "of", "on", "or", "should", "the", "to", "with"
    };

    public IReadOnlyList<MemoryEntry> Search(IReadOnlyList<MemoryEntry> memories, string? query)
    {
        ArgumentNullException.ThrowIfNull(memories);

        var queryTokens = Tokenize(query);
        if (queryTokens.Count == 0)
        {
            return [];
        }

        var ranked = new List<RankedMemory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < memories.Count; index++)
        {
            var memory = memories[index];
            ArgumentNullException.ThrowIfNull(memory);
            if (!seen.Add(memory.Id))
            {
                continue;
            }

            ranked.Add(new RankedMemory(memory, Score(queryTokens, Tokenize(memory.Text)), index));
        }

        ranked.Sort(static (left, right) =>
        {
            var byScore = right.Score.CompareTo(left.Score);
            return byScore != 0 ? byScore : left.Index.CompareTo(right.Index);
        });

        var take = Math.Min(ResultLimit, ranked.Count);
        var results = new List<MemoryEntry>(take);
        for (var index = 0; index < take; index++)
        {
            results.Add(ranked[index].Memory);
        }

        return results;
    }

    private static int Score(HashSet<string> queryTokens, HashSet<string> memoryTokens)
    {
        var score = 0;
        foreach (var queryToken in queryTokens)
        {
            foreach (var memoryToken in memoryTokens)
            {
                if (Matches(queryToken, memoryToken))
                {
                    score++;
                    break;
                }
            }
        }

        return score;
    }

    private static bool Matches(string queryToken, string memoryToken)
    {
        if (string.Equals(queryToken, memoryToken, StringComparison.Ordinal))
        {
            return true;
        }

        var queryIsShorter = queryToken.Length <= memoryToken.Length;
        var shorter = queryIsShorter ? queryToken : memoryToken;
        var longer = queryIsShorter ? memoryToken : queryToken;
        return shorter.Length >= 4 && longer.StartsWith(shorter, StringComparison.Ordinal);
    }

    private static HashSet<string> Tokenize(string? text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        var current = new StringBuilder();
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(char.ToLowerInvariant(character));
                continue;
            }

            AddToken(tokens, current);
        }

        AddToken(tokens, current);
        return tokens;
    }

    private static void AddToken(HashSet<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        var token = current.ToString();
        current.Clear();
        if (token.Length < 2 || StopWords.Contains(token))
        {
            return;
        }

        tokens.Add(token);
    }

    private readonly record struct RankedMemory(MemoryEntry Memory, int Score, int Index);
}
