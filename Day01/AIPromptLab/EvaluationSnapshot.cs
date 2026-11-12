// One completed evaluation run, kept for later comparisons.
// Totals, the failure rate, and category counts come from the report's failure analysis.
// Those counts are copied, so a later change to the report or the source dictionary leaves this run unchanged.
// The failure rate stays the analysis percentage: failed / total × 100. An empty run keeps 0.
// CaseIds is the evaluation dataset in result order. Null means this run did not record its cases.
// A recorded list has one id per case. The regression gate uses it to see whether two runs scored the same dataset.
public sealed record EvaluationSnapshot(
    string RunId,
    DateTimeOffset EvaluatedAt,
    int TotalCases,
    int FailedCases,
    double FailureRate,
    IReadOnlyDictionary<EvaluationFailureCategory, int> CategoryCounts,
    IReadOnlyList<string>? CaseIds = null)
{
    public string RunId { get; init; } = RequireRunId(RunId);

    public IReadOnlyDictionary<EvaluationFailureCategory, int> CategoryCounts { get; init; } =
        ValidatedCounts(TotalCases, FailedCases, FailureRate, CategoryCounts);

    public IReadOnlyList<string>? CaseIds { get; init; } = CopyCaseIds(CaseIds, TotalCases);

    public static EvaluationSnapshot Capture(
        string runId,
        DateTimeOffset evaluatedAt,
        EvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var analysis = EvaluationFailureAnalyzer.Analyze(report);
        var caseIds = new string[report.Results.Count];
        for (var index = 0; index < report.Results.Count; index++)
        {
            caseIds[index] = report.Results[index].CaseId;
        }

        return new EvaluationSnapshot(
            runId,
            evaluatedAt,
            analysis.TotalCases,
            analysis.FailedCases,
            analysis.FailureRate,
            analysis.CategoryCounts,
            caseIds);
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

    private static IReadOnlyList<string>? CopyCaseIds(IReadOnlyList<string>? caseIds, int totalCases)
    {
        if (caseIds is null)
        {
            return null;
        }

        if (caseIds.Count != totalCases)
        {
            throw new ArgumentException(
                "Case ids must have one entry for each case.",
                nameof(caseIds));
        }

        var copy = new string[caseIds.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < caseIds.Count; index++)
        {
            var caseId = caseIds[index];
            if (string.IsNullOrWhiteSpace(caseId))
            {
                throw new ArgumentException(
                    "Case ids cannot contain null or blank values.",
                    nameof(caseIds));
            }

            var trimmed = caseId.Trim();
            if (!seen.Add(trimmed))
            {
                throw new ArgumentException(
                    $"The case '{trimmed}' is listed more than once.",
                    nameof(caseIds));
            }

            copy[index] = trimmed;
        }

        return copy;
    }
}
