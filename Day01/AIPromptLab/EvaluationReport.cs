using System.Globalization;

public sealed record EvaluationReport(
    int TotalCases,
    int PassedCases,
    int FailedCases,
    bool Passed,
    IReadOnlyList<EvaluationCaseResult> Results,
    EvaluationThresholds Thresholds)
{
    public IReadOnlyList<EvaluationCaseResult> Results { get; init; } = CopyResults(Results);

    public EvaluationThresholds Thresholds { get; init; } = RequireThresholds(Thresholds);

    public int TotalCases { get; init; } = RequireTotal(TotalCases, Results);

    public int PassedCases { get; init; } = RequirePassedCases(PassedCases, Results);

    public int FailedCases { get; init; } = RequireFailedCases(FailedCases, PassedCases, Results);

    public bool Passed { get; init; } = RequirePassed(Passed, PassedCases, Results, Thresholds);

    public string Report
    {
        get
        {
            if (TotalCases == 0)
            {
                return string.Join(
                    '\n',
                    "Evaluation: FAIL",
                    "The evaluation dataset is empty.",
                    "Passed: 0",
                    "Failed: 0",
                    "Total: 0");
            }

            var rate = (double)PassedCases / TotalCases;
            var lines = new List<string>
            {
                $"Evaluation: {(Passed ? "PASS" : "FAIL")}",
                $"Pass rate: {Format(rate)} (minimum {Format(Thresholds.MinimumPassRate)})",
                $"Passed: {PassedCases}",
                $"Failed: {FailedCases}",
                $"Total: {TotalCases}"
            };

            foreach (var result in Results)
            {
                lines.Add($"{result.CaseId}: {(result.Passed ? "PASS" : "FAIL")}");
                foreach (var reason in result.FailureReasons)
                {
                    lines.Add($"- {reason}");
                }
            }

            return string.Join('\n', lines);
        }
    }

    public override string ToString() => Report;

    private static IReadOnlyList<EvaluationCaseResult> CopyResults(IReadOnlyList<EvaluationCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var copy = new EvaluationCaseResult[results.Count];
        for (var index = 0; index < results.Count; index++)
        {
            copy[index] = results[index] ?? throw new ArgumentException(
                "Case results cannot contain null values.",
                nameof(results));
        }

        return copy;
    }

    private static EvaluationThresholds RequireThresholds(EvaluationThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        return thresholds;
    }

    private static int RequireTotal(int totalCases, IReadOnlyList<EvaluationCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (totalCases != results.Count)
        {
            throw new ArgumentException("Total cases must match the results.", nameof(totalCases));
        }

        return totalCases;
    }

    private static int RequirePassedCases(int passedCases, IReadOnlyList<EvaluationCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var actual = 0;
        foreach (var result in results)
        {
            if (result is not null && result.Passed)
            {
                actual++;
            }
        }

        if (passedCases != actual)
        {
            throw new ArgumentException("Passed cases must match the results.", nameof(passedCases));
        }

        return passedCases;
    }

    private static int RequireFailedCases(
        int failedCases,
        int passedCases,
        IReadOnlyList<EvaluationCaseResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (failedCases != results.Count - passedCases)
        {
            throw new ArgumentException("Failed cases must be the cases that did not pass.", nameof(failedCases));
        }

        return failedCases;
    }

    private static bool RequirePassed(
        bool passed,
        int passedCases,
        IReadOnlyList<EvaluationCaseResult> results,
        EvaluationThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(thresholds);
        if (results.Count == 0)
        {
            if (passed)
            {
                throw new ArgumentException("An empty dataset cannot pass.", nameof(passed));
            }

            return passed;
        }

        if (passed != thresholds.IsMet(passedCases, results.Count))
        {
            throw new ArgumentException(
                "Passed must follow the pass rate and the configured threshold.",
                nameof(passed));
        }

        return passed;
    }

    private static string Format(double value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);
}
