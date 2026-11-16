// Ranks stored memories by cosine similarity. Embeddings stay in memory.
// KeywordMemoryScorer remains available for keyword overlap.
public sealed class EmbeddingMemoryRetriever
{
    public const int ResultLimit = 3;

    private readonly ITextEmbedder _embedder;
    private readonly Dictionary<string, float[]> _vectors = new(StringComparer.Ordinal);

    public EmbeddingMemoryRetriever(ITextEmbedder embedder)
    {
        ArgumentNullException.ThrowIfNull(embedder);
        _embedder = embedder;
    }

    public void Index(MemoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var embedding = _embedder.Embed(entry.Text);
        _vectors[entry.Id] = embedding.Vector.ToArray();
    }

    public IReadOnlyList<MemoryEntry> Search(IReadOnlyList<MemoryEntry> memories, string? query)
    {
        ArgumentNullException.ThrowIfNull(memories);

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var queryVector = _embedder.Embed(query).Vector;
        var ranked = new List<RankedMemory>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < memories.Count; index++)
        {
            var memory = memories[index];
            ArgumentNullException.ThrowIfNull(memory);
            if (!seen.Add(memory.Id) || !_vectors.TryGetValue(memory.Id, out var stored))
            {
                continue;
            }

            ranked.Add(new RankedMemory(memory, VectorSimilarity.Cosine(queryVector, stored), index));
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

    private readonly record struct RankedMemory(MemoryEntry Memory, double Score, int Index);
}
