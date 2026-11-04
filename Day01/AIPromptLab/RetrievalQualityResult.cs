using System.Globalization;

public sealed class RetrievalQualityResult
{
    private RetrievalQualityResult(
        double precisionAt3,
        double recallAt3,
        RetrievalQualityThresholds thresholds,
        IReadOnlyList<string> failureReasons)
    {
        PrecisionAt3 = precisionAt3;
        RecallAt3 = recallAt3;
        Thresholds = thresholds;
        FailureReasons = failureReasons;
    }

    public bool Passed => FailureReasons.Count == 0;

    public double PrecisionAt3 { get; }

    public double RecallAt3 { get; }

    public RetrievalQualityThresholds Thresholds { get; }

    public IReadOnlyList<string> FailureReasons { get; }

    public string Report =>
        string.Join(
            '\n',
            $"Retrieval quality: {(Passed ? "PASSED" : "FAILED")}",
            MetricLine("Precision@3:", PrecisionAt3, Thresholds.MinimumPrecisionAt3),
            MetricLine("Recall@3:", RecallAt3, Thresholds.MinimumRecallAt3));

    public static RetrievalQualityResult From(
        RetrievalStrategyScore score,
        RetrievalQualityThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(thresholds);
        RequireScore(score.PrecisionAt3, "Precision@3", nameof(score));
        RequireScore(score.RecallAt3, "Recall@3", nameof(score));

        var reasons = new List<string>();
        if (score.PrecisionAt3 < thresholds.MinimumPrecisionAt3)
        {
            reasons.Add(Reason("Precision@3", score.PrecisionAt3, thresholds.MinimumPrecisionAt3));
        }

        if (score.RecallAt3 < thresholds.MinimumRecallAt3)
        {
            reasons.Add(Reason("Recall@3", score.RecallAt3, thresholds.MinimumRecallAt3));
        }

        return new RetrievalQualityResult(
            score.PrecisionAt3,
            score.RecallAt3,
            thresholds,
            reasons.ToArray());
    }

    public override string ToString() => Report;

    private static string MetricLine(string label, double actual, double minimum) =>
        $"{label,-12} {Format(actual)} (minimum {Format(minimum)})";

    private static string Reason(string metric, double actual, double minimum) =>
        $"{metric} is {Format(actual)}, below the minimum {Format(minimum)}.";

    private static string Format(double value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);

    private static void RequireScore(double value, string metric, string paramName)
    {
        if (!double.IsFinite(value) || value < 0d || value > 1d)
        {
            throw new ArgumentOutOfRangeException(paramName, $"{metric} must be from 0 through 1.");
        }
    }
}
