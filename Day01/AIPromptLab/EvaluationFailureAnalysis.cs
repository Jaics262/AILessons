using System.Globalization;

// Failure rate is the percentage of cases that failed: failed / total × 100.
// CategoryCounts counts failure occurrences. FailedCases counts cases.
// An empty dataset keeps a rate of 0. Most common is null when every category count is 0.
// A tie uses the earliest category.
public sealed record EvaluationFailureAnalysis(
    int TotalCases,
    int FailedCases,
    double FailureRate,
    IReadOnlyDictionary<EvaluationFailureCategory, int> CategoryCounts,
    EvaluationFailureCategory? MostCommonCategory)
{
    public int TotalCases { get; init; } = RequireTotal(TotalCases);

    public int FailedCases { get; init; } = RequireFailed(FailedCases, TotalCases);

    public double FailureRate { get; init; } = RequireRate(FailureRate, FailedCases, TotalCases);

    public IReadOnlyDictionary<EvaluationFailureCategory, int> CategoryCounts { get; init; } =
        CopyCounts(CategoryCounts);

    public EvaluationFailureCategory? MostCommonCategory { get; init; } =
        RequireMostCommon(MostCommonCategory, CategoryCounts);

    public string Report
    {
        get
        {
            string heading;
            if (TotalCases == 0)
            {
                heading = "Failure analysis: no cases.";
            }
            else if (FailedCases == 0)
            {
                heading = "Failure analysis: no failures.";
            }
            else
            {
                heading = "Failure analysis:";
            }

            var lines = new List<string>
            {
                heading,
                $"Total: {TotalCases}",
                $"Failed: {FailedCases}",
                $"Failure rate: {Format(FailureRate)}"
            };

            foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
            {
                lines.Add($"{category}: {CategoryCounts[category]}");
            }

            lines.Add($"Most common: {MostCommonCategory?.ToString() ?? "none"}");
            return string.Join('\n', lines);
        }
    }

    public override string ToString() => Report;

    internal static double Percentage(int failedCases, int totalCases)
    {
        if (totalCases == 0)
        {
            return 0d;
        }

        return (double)failedCases / totalCases * 100d;
    }

    private static int RequireTotal(int totalCases)
    {
        if (totalCases < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCases), "Total cases cannot be negative.");
        }

        return totalCases;
    }

    private static int RequireFailed(int failedCases, int totalCases)
    {
        if (failedCases < 0 || failedCases > totalCases)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failedCases),
                "Failed cases must be from 0 through the total.");
        }

        return failedCases;
    }

    private static double RequireRate(double failureRate, int failedCases, int totalCases)
    {
        if (!double.IsFinite(failureRate))
        {
            throw new ArgumentOutOfRangeException(nameof(failureRate), "Failure rate must be a finite percentage.");
        }

        if (failureRate != Percentage(failedCases, totalCases))
        {
            throw new ArgumentException(
                "Failure rate must be failed cases / total cases × 100, or 0 when there are no cases.",
                nameof(failureRate));
        }

        return failureRate;
    }

    private static IReadOnlyDictionary<EvaluationFailureCategory, int> CopyCounts(
        IReadOnlyDictionary<EvaluationFailureCategory, int> categoryCounts)
    {
        ArgumentNullException.ThrowIfNull(categoryCounts);
        var copy = new Dictionary<EvaluationFailureCategory, int>();
        foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
        {
            if (!categoryCounts.TryGetValue(category, out var count))
            {
                throw new ArgumentException(
                    "Category counts must include every failure category.",
                    nameof(categoryCounts));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(categoryCounts),
                    "A category count cannot be negative.");
            }

            copy.Add(category, count);
        }

        if (copy.Count != categoryCounts.Count)
        {
            throw new ArgumentException(
                "Category counts must include only defined failure categories.",
                nameof(categoryCounts));
        }

        return copy;
    }

    private static EvaluationFailureCategory? RequireMostCommon(
        EvaluationFailureCategory? mostCommonCategory,
        IReadOnlyDictionary<EvaluationFailureCategory, int> categoryCounts)
    {
        ArgumentNullException.ThrowIfNull(categoryCounts);
        if (mostCommonCategory is EvaluationFailureCategory category && !Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mostCommonCategory),
                "Most common category must be a defined failure category.");
        }

        if (mostCommonCategory != Leader(categoryCounts))
        {
            throw new ArgumentException(
                "Most common category must be the earliest category with the highest count, or null when every count is zero.",
                nameof(mostCommonCategory));
        }

        return mostCommonCategory;
    }

    internal static EvaluationFailureCategory? Leader(
        IReadOnlyDictionary<EvaluationFailureCategory, int> categoryCounts)
    {
        EvaluationFailureCategory? leader = null;
        var mostCount = 0;
        foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
        {
            if (!categoryCounts.TryGetValue(category, out var count))
            {
                throw new ArgumentException(
                    "Category counts must include every failure category.",
                    nameof(categoryCounts));
            }

            if (count > mostCount)
            {
                mostCount = count;
                leader = category;
            }
        }

        return leader;
    }

    private static string Format(double value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);
}
