namespace AIPromptLab.Tests;

public sealed class EvaluationThresholdsTests
{
    [Fact]
    public void Default_requires_every_case_to_pass()
    {
        var thresholds = EvaluationThresholds.Default;

        Assert.Equal(1d, thresholds.MinimumPassRate);
        Assert.Equal(EvaluationThresholds.DefaultMinimumPassRate, thresholds.MinimumPassRate);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(0.5d)]
    [InlineData(1d)]
    public void Constructor_accepts_a_rate_from_zero_through_one(double minimumPassRate)
    {
        var thresholds = new EvaluationThresholds(minimumPassRate);

        Assert.Equal(minimumPassRate, thresholds.MinimumPassRate);
    }

    [Theory]
    [InlineData(-0.01d)]
    [InlineData(1.01d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_rejects_a_rate_outside_zero_to_one(double minimumPassRate)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EvaluationThresholds(minimumPassRate));

        Assert.Equal("MinimumPassRate", exception.ParamName);
    }

    [Theory]
    [InlineData(2, 2, 1d, true)]
    [InlineData(1, 2, 0.5d, true)]
    [InlineData(1, 2, 0.51d, false)]
    [InlineData(0, 4, 0d, true)]
    [InlineData(2, 6, 1d, false)]
    public void IsMet_compares_the_pass_rate_with_the_threshold(
        int passedCases,
        int totalCases,
        double minimumPassRate,
        bool expected)
    {
        var thresholds = new EvaluationThresholds(minimumPassRate);

        Assert.Equal(expected, thresholds.IsMet(passedCases, totalCases));
    }
}
