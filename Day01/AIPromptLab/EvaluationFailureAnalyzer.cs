// Counts failed cases and failure occurrences on an evaluation report.
// Category counts are occurrences. One answer with two unsupported claims is one failed case and two UngroundedClaim counts.
// One answer with an ungrounded claim and a missing fact increments both categories and remains one failed case.
// The failure rate is a percentage of cases. An empty dataset yields 0 without dividing by zero.
public static class EvaluationFailureAnalyzer
{
    public static EvaluationFailureAnalysis Analyze(EvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var counts = new Dictionary<EvaluationFailureCategory, int>();
        foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
        {
            counts.Add(category, 0);
        }

        foreach (var result in report.Results)
        {
            foreach (var category in result.FailureCategories)
            {
                counts[category]++;
            }
        }

        return new EvaluationFailureAnalysis(
            report.TotalCases,
            report.FailedCases,
            EvaluationFailureAnalysis.Percentage(report.FailedCases, report.TotalCases),
            counts,
            EvaluationFailureAnalysis.Leader(counts));
    }
}
