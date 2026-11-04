namespace AIPromptLab.Tests;

public sealed class RetrievalQualityThresholdsTests
{
    [Fact]
    public void Default_uses_the_learning_thresholds()
    {
        var thresholds = RetrievalQualityThresholds.Default;

        Assert.Equal(0.25d, thresholds.MinimumPrecisionAt3);
        Assert.Equal(0.80d, thresholds.MinimumRecallAt3);
        Assert.Equal(RetrievalQualityThresholds.DefaultMinimumPrecisionAt3, thresholds.MinimumPrecisionAt3);
        Assert.Equal(RetrievalQualityThresholds.DefaultMinimumRecallAt3, thresholds.MinimumRecallAt3);
    }

    [Fact]
    public void Constructor_accepts_a_custom_pair()
    {
        var thresholds = new RetrievalQualityThresholds(0.4d, 1d);

        Assert.Equal(0.4d, thresholds.MinimumPrecisionAt3);
        Assert.Equal(1d, thresholds.MinimumRecallAt3);
    }

    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(1d, 1d)]
    public void Constructor_accepts_the_ends_of_the_unit_interval(double precision, double recall)
    {
        var thresholds = new RetrievalQualityThresholds(precision, recall);

        Assert.Equal(precision, thresholds.MinimumPrecisionAt3);
        Assert.Equal(recall, thresholds.MinimumRecallAt3);
    }

    [Theory]
    [InlineData(-0.01d, 0.80d, "MinimumPrecisionAt3")]
    [InlineData(1.01d, 0.80d, "MinimumPrecisionAt3")]
    [InlineData(double.NaN, 0.80d, "MinimumPrecisionAt3")]
    [InlineData(double.PositiveInfinity, 0.80d, "MinimumPrecisionAt3")]
    [InlineData(double.NegativeInfinity, 0.80d, "MinimumPrecisionAt3")]
    [InlineData(0.25d, -0.01d, "MinimumRecallAt3")]
    [InlineData(0.25d, 1.01d, "MinimumRecallAt3")]
    [InlineData(0.25d, double.NaN, "MinimumRecallAt3")]
    [InlineData(0.25d, double.PositiveInfinity, "MinimumRecallAt3")]
    [InlineData(0.25d, double.NegativeInfinity, "MinimumRecallAt3")]
    public void Constructor_rejects_a_threshold_outside_zero_to_one(
        double precision,
        double recall,
        string paramName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new RetrievalQualityThresholds(precision, recall));

        Assert.Equal(paramName, exception.ParamName);
    }
}
