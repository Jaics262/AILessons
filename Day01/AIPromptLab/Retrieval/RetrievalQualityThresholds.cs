// Learning defaults for the retrieval report, not a production standard.
// Keyword retrieval from the Day 14 set meets both. The current vector retriever does not.
public sealed record RetrievalQualityThresholds(double MinimumPrecisionAt3, double MinimumRecallAt3)
{
    public const double DefaultMinimumPrecisionAt3 = 0.25;

    public const double DefaultMinimumRecallAt3 = 0.80;

    public static RetrievalQualityThresholds Default { get; } = new(
        DefaultMinimumPrecisionAt3,
        DefaultMinimumRecallAt3);

    public double MinimumPrecisionAt3 { get; init; } = RequireUnitInterval(
        MinimumPrecisionAt3,
        nameof(MinimumPrecisionAt3));

    public double MinimumRecallAt3 { get; init; } = RequireUnitInterval(
        MinimumRecallAt3,
        nameof(MinimumRecallAt3));

    private static double RequireUnitInterval(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0d || value > 1d)
        {
            throw new ArgumentOutOfRangeException(paramName, "A retrieval threshold must be from 0 through 1.");
        }

        return value;
    }
}
