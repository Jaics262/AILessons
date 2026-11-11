// One completed evaluation run, kept for later comparisons.
// Totals, the failure rate, and category counts come from the report's failure analysis.
// Those counts are copied, so a later change to the report or the source dictionary leaves this run unchanged.
// The failure rate stays the analysis percentage: failed / total × 100. An empty run keeps 0.
public sealed record EvaluationSnapshot(
    string RunId,
    DateTimeOffset EvaluatedAt,
    int TotalCases,
    int FailedCases,
    double FailureRate,
    IReadOnlyDictionary<EvaluationFailureCategory, int> CategoryCounts)
{
    public string RunId { get; init; } = RequireRunId(RunId);

    public IReadOnlyDictionary<EvaluationFailureCategory, int> CategoryCounts { get; init; } =
        ValidatedCounts(TotalCases, FailedCases, FailureRate, CategoryCounts);

    public static EvaluationSnapshot Capture(
        string runId,
        DateTimeOffset evaluatedAt,
        EvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var analysis = EvaluationFailureAnalyzer.Analyze(report);
        return new EvaluationSnapshot(
            runId,
            evaluatedAt,
            analysis.TotalCases,
            analysis.FailedCases,
            analysis.FailureRate,
            analysis.CategoryCounts);
    }

    private static string RequireRunId(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return runId.Trim();
    }

    private static IReadOnlyDictionary<EvaluationFailureCategory, int> ValidatedCounts(
        int totalCases,
        int failedCases,
        double failureRate,
        IReadOnlyDictionary<EvaluationFailureCategory, int> categoryCounts)
    {
        var analysis = new EvaluationFailureAnalysis(
            totalCases,
            failedCases,
            failureRate,
            categoryCounts,
            LeaderWhenCountsAreComplete(categoryCounts));
        return analysis.CategoryCounts;
    }

    private static EvaluationFailureCategory? LeaderWhenCountsAreComplete(
        IReadOnlyDictionary<EvaluationFailureCategory, int> categoryCounts)
    {
        if (categoryCounts is null)
        {
            return null;
        }

        foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
        {
            if (!categoryCounts.ContainsKey(category))
            {
                return null;
            }
        }

        return EvaluationFailureAnalysis.Leader(categoryCounts);
    }
}
