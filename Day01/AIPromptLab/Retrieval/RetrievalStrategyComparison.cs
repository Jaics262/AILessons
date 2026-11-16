public static class RetrievalStrategyComparison
{
    public const int K = 3;

    public static RetrievalStrategyScore EvaluateKeyword(
        IReadOnlyList<MemoryEntry> memories,
        IReadOnlyList<RetrievalTestCase> cases)
    {
        ArgumentNullException.ThrowIfNull(memories);
        var scorer = new KeywordMemoryScorer();
        return Average(cases, query => scorer.Search(memories, query));
    }

    public static RetrievalStrategyScore EvaluateVector(
        IReadOnlyList<MemoryEntry> memories,
        IReadOnlyList<RetrievalTestCase> cases,
        ITextEmbedder embedder)
    {
        ArgumentNullException.ThrowIfNull(memories);
        ArgumentNullException.ThrowIfNull(embedder);

        var retriever = new EmbeddingMemoryRetriever(embedder);
        foreach (var memory in memories)
        {
            retriever.Index(memory);
        }

        return Average(cases, query => retriever.Search(memories, query));
    }

    private static RetrievalStrategyScore Average(
        IReadOnlyList<RetrievalTestCase> cases,
        Func<string, IReadOnlyList<MemoryEntry>> search)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(search);

        if (cases.Count == 0)
        {
            throw new ArgumentException("At least one retrieval case is required.", nameof(cases));
        }

        double precision = 0;
        double recall = 0;
        foreach (var testCase in cases)
        {
            ArgumentNullException.ThrowIfNull(testCase);
            var retrievedIds = search(testCase.Query).Select(memory => memory.Id).ToArray();
            precision += RetrievalMetrics.PrecisionAtK(retrievedIds, testCase.ExpectedMemoryIds, K);
            recall += RetrievalMetrics.RecallAtK(retrievedIds, testCase.ExpectedMemoryIds, K);
        }

        return new RetrievalStrategyScore(precision / cases.Count, recall / cases.Count);
    }
}
