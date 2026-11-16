// Learning default for the answer report, not a production standard.
// A pass rate of 1 fails the fixed set, because four of its six answers are labeled Fail.
public sealed record EvaluationThresholds(double MinimumPassRate)
{
    public const double DefaultMinimumPassRate = 1d;

    public static EvaluationThresholds Default { get; } = new(DefaultMinimumPassRate);

    public double MinimumPassRate { get; init; } = RequireUnitInterval(MinimumPassRate, nameof(MinimumPassRate));

    public bool IsMet(int passedCases, int totalCases)
    {
        if (totalCases <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCases), "Total cases must be positive.");
        }

        if (passedCases < 0 || passedCases > totalCases)
        {
            throw new ArgumentOutOfRangeException(nameof(passedCases), "Passed cases must be from 0 through the total.");
        }

        return (double)passedCases / totalCases >= MinimumPassRate;
    }

    private static double RequireUnitInterval(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0d || value > 1d)
        {
            throw new ArgumentOutOfRangeException(paramName, "An evaluation threshold must be from 0 through 1.");
        }

        return value;
    }
}
