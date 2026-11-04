namespace AIPromptLab.Tests;

public sealed class RetrievalQualityResultTests
{
    [Fact]
    public void From_passes_keyword_retrieval_against_the_learning_thresholds()
    {
        var score = RetrievalStrategyComparison.EvaluateKeyword(
            RetrievalEvaluationSet.Memories,
            RetrievalEvaluationSet.Cases);

        var result = RetrievalQualityResult.From(score, RetrievalQualityThresholds.Default);

        Assert.True(result.Passed);
        Assert.Equal(score.PrecisionAt3, result.PrecisionAt3);
        Assert.Equal(score.RecallAt3, result.RecallAt3);
        Assert.Same(RetrievalQualityThresholds.Default, result.Thresholds);
        Assert.Empty(result.FailureReasons);
        Assert.Equal(
            "Retrieval quality: PASSED\n" +
            "Precision@3: 0.300 (minimum 0.250)\n" +
            "Recall@3:    0.900 (minimum 0.800)",
            result.Report);
    }

    [Fact]
    public void From_fails_vector_retrieval_against_the_learning_thresholds()
    {
        var score = RetrievalStrategyComparison.EvaluateVector(
            RetrievalEvaluationSet.Memories,
            RetrievalEvaluationSet.Cases,
            new HashedTokenEmbedder());

        var result = RetrievalQualityResult.From(score, RetrievalQualityThresholds.Default);

        Assert.False(result.Passed);
        Assert.Equal(score.PrecisionAt3, result.PrecisionAt3);
        Assert.Equal(score.RecallAt3, result.RecallAt3);
        Assert.Equal(1d / 6d, result.PrecisionAt3);
        Assert.Equal(0.5d, result.RecallAt3);
        Assert.Equal(
            [
                "Precision@3 is 0.167, below the minimum 0.250.",
                "Recall@3 is 0.500, below the minimum 0.800."
            ],
            result.FailureReasons);
        Assert.Equal(
            "Retrieval quality: FAILED\n" +
            "Precision@3: 0.167 (minimum 0.250)\n" +
            "Recall@3:    0.500 (minimum 0.800)",
            result.Report);
        Assert.Equal(result.Report, result.ToString());
    }

    [Fact]
    public void From_uses_the_configured_thresholds()
    {
        var score = new RetrievalStrategyScore(0.10d, 0.20d);
        var thresholds = new RetrievalQualityThresholds(0.10d, 0.50d);

        var result = RetrievalQualityResult.From(score, thresholds);

        Assert.False(result.Passed);
        Assert.Equal(0.10d, result.PrecisionAt3);
        Assert.Equal(0.20d, result.RecallAt3);
        Assert.Same(thresholds, result.Thresholds);
        Assert.Equal(["Recall@3 is 0.200, below the minimum 0.500."], result.FailureReasons);
        Assert.Equal(
            "Retrieval quality: FAILED\n" +
            "Precision@3: 0.100 (minimum 0.100)\n" +
            "Recall@3:    0.200 (minimum 0.500)",
            result.Report);
    }

    [Fact]
    public void From_passes_when_both_metrics_equal_their_thresholds()
    {
        var thresholds = new RetrievalQualityThresholds(0.25d, 0.80d);
        var result = RetrievalQualityResult.From(new RetrievalStrategyScore(0.25d, 0.80d), thresholds);

        Assert.True(result.Passed);
        Assert.Empty(result.FailureReasons);
    }

    [Fact]
    public void From_names_only_the_precision_miss()
    {
        var result = RetrievalQualityResult.From(
            new RetrievalStrategyScore(0.249d, 0.80d),
            RetrievalQualityThresholds.Default);

        Assert.False(result.Passed);
        Assert.Equal(["Precision@3 is 0.249, below the minimum 0.250."], result.FailureReasons);
    }

    [Fact]
    public void From_rejects_a_null_score_or_thresholds()
    {
        var score = new RetrievalStrategyScore(0.30d, 0.90d);
        var missingScore = Assert.Throws<ArgumentNullException>(
            () => RetrievalQualityResult.From(null!, RetrievalQualityThresholds.Default));
        var missingThresholds = Assert.Throws<ArgumentNullException>(
            () => RetrievalQualityResult.From(score, null!));

        Assert.Equal("score", missingScore.ParamName);
        Assert.Equal("thresholds", missingThresholds.ParamName);
    }

    [Theory]
    [InlineData(double.NaN, 0.80d, "Precision@3")]
    [InlineData(double.PositiveInfinity, 0.80d, "Precision@3")]
    [InlineData(-0.01d, 0.80d, "Precision@3")]
    [InlineData(1.01d, 0.80d, "Precision@3")]
    [InlineData(0.25d, double.NaN, "Recall@3")]
    [InlineData(0.25d, double.NegativeInfinity, "Recall@3")]
    [InlineData(0.25d, -0.01d, "Recall@3")]
    [InlineData(0.25d, 1.01d, "Recall@3")]
    public void From_rejects_a_score_outside_zero_to_one(double precision, double recall, string metric)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RetrievalQualityResult.From(
                new RetrievalStrategyScore(precision, recall),
                RetrievalQualityThresholds.Default));

        Assert.Equal("score", exception.ParamName);
        Assert.Contains(metric, exception.Message, StringComparison.Ordinal);
    }
}
