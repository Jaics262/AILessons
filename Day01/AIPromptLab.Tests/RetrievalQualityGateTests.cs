namespace AIPromptLab.Tests;

public sealed class RetrievalQualityGateTests
{
    [Fact]
    public void Gate_passes_when_both_metrics_meet_the_thresholds()
    {
        var result = JudgeKeyword();

        Assert.Equal(0.25d, result.Thresholds.MinimumPrecisionAt3);
        Assert.Equal(0.80d, result.Thresholds.MinimumRecallAt3);
        Assert.Equal(0.30d, result.PrecisionAt3);
        Assert.Equal(0.90d, result.RecallAt3);
        Assert.True(result.PrecisionAt3 >= result.Thresholds.MinimumPrecisionAt3);
        Assert.True(result.RecallAt3 >= result.Thresholds.MinimumRecallAt3);
        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
    }

    [Theory]
    [InlineData(0.125d, 1d, "Precision@3 is 0.125, below the minimum 0.250.")]
    [InlineData(0.5d, 0.5d, "Recall@3 is 0.500, below the minimum 0.800.")]
    public void Gate_fails_when_either_metric_is_below_its_threshold(
        double precision,
        double recall,
        string reason)
    {
        var result = RetrievalQualityResult.From(
            new RetrievalStrategyScore(precision, recall),
            RetrievalQualityThresholds.Default);

        Assert.False(result.Passed);
        Assert.Equal(precision, result.PrecisionAt3);
        Assert.Equal(recall, result.RecallAt3);
        Assert.Equal([reason], result.FailureReasons);
    }

    [Fact]
    public void Gate_reports_both_failures_when_vector_retrieval_misses_both_thresholds()
    {
        var result = JudgeVector();

        Assert.Equal(0.25d, RetrievalQualityThresholds.DefaultMinimumPrecisionAt3);
        Assert.Equal(0.80d, RetrievalQualityThresholds.DefaultMinimumRecallAt3);
        Assert.Equal(1d / 6d, result.PrecisionAt3);
        Assert.Equal(0.5d, result.RecallAt3);
        Assert.True(result.PrecisionAt3 < result.Thresholds.MinimumPrecisionAt3);
        Assert.True(result.RecallAt3 < result.Thresholds.MinimumRecallAt3);
        Assert.False(result.Passed);
        Assert.Equal(
            [
                "Precision@3 is 0.167, below the minimum 0.250.",
                "Recall@3 is 0.500, below the minimum 0.800."
            ],
            result.FailureReasons);
    }

    [Fact]
    public void Gate_passes_when_both_metrics_equal_the_thresholds()
    {
        var thresholds = RetrievalQualityThresholds.Default;
        var result = RetrievalQualityResult.From(
            new RetrievalStrategyScore(thresholds.MinimumPrecisionAt3, thresholds.MinimumRecallAt3),
            thresholds);

        Assert.Equal(thresholds.MinimumPrecisionAt3, result.PrecisionAt3);
        Assert.Equal(thresholds.MinimumRecallAt3, result.RecallAt3);
        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
    }

    [Fact]
    public void Gate_repeats_the_same_result_for_the_evaluation_dataset()
    {
        var keyword = JudgeKeyword();
        var keywordAgain = JudgeKeyword();
        var vector = JudgeVector();
        var vectorAgain = JudgeVector();

        Assert.Equal(keyword.Report, keywordAgain.Report);
        Assert.Equal(keyword.Passed, keywordAgain.Passed);
        Assert.Equal(keyword.PrecisionAt3, keywordAgain.PrecisionAt3);
        Assert.Equal(keyword.RecallAt3, keywordAgain.RecallAt3);
        Assert.Equal(keyword.FailureReasons, keywordAgain.FailureReasons);
        Assert.Equal(vector.Report, vectorAgain.Report);
        Assert.Equal(vector.Passed, vectorAgain.Passed);
        Assert.Equal(vector.PrecisionAt3, vectorAgain.PrecisionAt3);
        Assert.Equal(vector.RecallAt3, vectorAgain.RecallAt3);
        Assert.Equal(vector.FailureReasons, vectorAgain.FailureReasons);
        Assert.True(keyword.Passed);
        Assert.False(vector.Passed);
    }

    [Fact]
    public void Gate_uses_the_existing_precision_and_recall_calculations()
    {
        var memories = RetrievalEvaluationSet.Memories;
        var keywordMetrics = AverageMetrics(query => new KeywordMemoryScorer().Search(memories, query));
        var keyword = JudgeKeyword();

        Assert.Equal(RetrievalStrategyComparison.K, 3);
        Assert.Equal(keywordMetrics.Precision, keyword.PrecisionAt3);
        Assert.Equal(keywordMetrics.Recall, keyword.RecallAt3);
        Assert.True(keyword.Passed);

        var retriever = new EmbeddingMemoryRetriever(new HashedTokenEmbedder());
        foreach (var memory in memories)
        {
            retriever.Index(memory);
        }

        var vectorMetrics = AverageMetrics(query => retriever.Search(memories, query));
        var vector = JudgeVector();

        Assert.Equal(vectorMetrics.Precision, vector.PrecisionAt3);
        Assert.Equal(vectorMetrics.Recall, vector.RecallAt3);
        Assert.False(vector.Passed);
        Assert.Equal(2, vector.FailureReasons.Count);
    }

    private static RetrievalQualityResult JudgeKeyword()
    {
        var score = RetrievalStrategyComparison.EvaluateKeyword(
            RetrievalEvaluationSet.Memories,
            RetrievalEvaluationSet.Cases);
        return RetrievalQualityResult.From(score, RetrievalQualityThresholds.Default);
    }

    private static RetrievalQualityResult JudgeVector()
    {
        var score = RetrievalStrategyComparison.EvaluateVector(
            RetrievalEvaluationSet.Memories,
            RetrievalEvaluationSet.Cases,
            new HashedTokenEmbedder());
        return RetrievalQualityResult.From(score, RetrievalQualityThresholds.Default);
    }

    private static (double Precision, double Recall) AverageMetrics(
        Func<string, IReadOnlyList<MemoryEntry>> search)
    {
        var cases = RetrievalEvaluationSet.Cases;
        double precision = 0;
        double recall = 0;
        foreach (var testCase in cases)
        {
            var retrievedIds = search(testCase.Query).Select(memory => memory.Id).ToArray();
            precision += RetrievalMetrics.PrecisionAtK(
                retrievedIds,
                testCase.ExpectedMemoryIds,
                RetrievalStrategyComparison.K);
            recall += RetrievalMetrics.RecallAtK(
                retrievedIds,
                testCase.ExpectedMemoryIds,
                RetrievalStrategyComparison.K);
        }

        return (precision / cases.Count, recall / cases.Count);
    }
}
