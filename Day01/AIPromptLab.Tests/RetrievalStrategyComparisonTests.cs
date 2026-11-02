namespace AIPromptLab.Tests;

public sealed class RetrievalStrategyComparisonTests
{
    [Fact]
    public void Comparison_reports_precision_and_recall_at_3()
    {
        var memories = RetrievalEvaluationSet.Memories;
        var cases = RetrievalEvaluationSet.Cases;
        var storedIds = memories.Select(memory => memory.Id).ToArray();
        var keyword = RetrievalStrategyComparison.EvaluateKeyword(memories, cases);
        var vector = RetrievalStrategyComparison.EvaluateVector(memories, cases, new HashedTokenEmbedder());

        Assert.Equal(0.3d, keyword.PrecisionAt3);
        Assert.Equal(0.9d, keyword.RecallAt3);
        Assert.Equal(1d / 6d, vector.PrecisionAt3);
        Assert.Equal(0.5d, vector.RecallAt3);
        Assert.Equal(storedIds, memories.Select(memory => memory.Id));
        Assert.Equal(
            cases.SelectMany(testCase => testCase.ExpectedMemoryIds).Order(StringComparer.Ordinal),
            storedIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Evaluate_averages_precision_and_recall_across_queries()
    {
        var createdAt = DateTimeOffset.Parse("2026-10-30T09:00:00Z");
        var memories = new List<MemoryEntry>
        {
            new("a", "alpha signal", createdAt),
            new("b", "beta signal", createdAt),
            new("c", "gamma signal", createdAt),
            new("d", "delta only", createdAt)
        };
        var cases = new List<RetrievalTestCase>
        {
            new("alpha signal", ["a"]),
            new("nope", ["d"])
        };
        var embedder = new FixedEmbedder();
        embedder.Set("alpha signal", [1f, 0f, 0f]);
        embedder.Set("beta signal", [0f, 1f, 0f]);
        embedder.Set("gamma signal", [0f, 1f, 0f]);
        embedder.Set("delta only", [0f, 0f, 1f]);
        embedder.Set("nope", [0f, 0f, 1f]);

        var keyword = RetrievalStrategyComparison.EvaluateKeyword(memories, cases);
        var vector = RetrievalStrategyComparison.EvaluateVector(memories, cases, embedder);

        Assert.Equal(1d / 6d, keyword.PrecisionAt3);
        Assert.Equal(0.5d, keyword.RecallAt3);
        Assert.Equal(1d / 3d, vector.PrecisionAt3);
        Assert.Equal(1d, vector.RecallAt3);
        Assert.Equal(["a", "b", "c", "d"], memories.Select(memory => memory.Id));
    }

    private sealed class FixedEmbedder : ITextEmbedder
    {
        private readonly Dictionary<string, float[]> _vectors = new(StringComparer.Ordinal);

        public void Set(string text, float[] vector)
        {
            _vectors[text] = vector;
        }

        public TextEmbedding Embed(string text)
        {
            return new TextEmbedding(text, _vectors[text.Trim()]);
        }
    }
}
