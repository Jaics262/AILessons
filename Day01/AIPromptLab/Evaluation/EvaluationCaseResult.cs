public sealed record EvaluationCaseResult(
    string CaseId,
    bool Passed,
    IReadOnlyList<EvaluationFailure> Failures)
{
    public string CaseId { get; init; } = RequireCaseId(CaseId);

    public IReadOnlyList<EvaluationFailure> Failures { get; init; } = CopyFailures(Failures);

    public bool Passed { get; init; } = RequirePassed(Passed, Failures);

    public IReadOnlyList<string> FailureReasons => ReasonsFrom(Failures);

    public IReadOnlyList<EvaluationFailureCategory> FailureCategories =>
        EvaluationFailureClassifier.Classify(Failures);

    private static string RequireCaseId(string caseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        return caseId.Trim();
    }

    private static IReadOnlyList<EvaluationFailure> CopyFailures(IReadOnlyList<EvaluationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        var copy = new EvaluationFailure[failures.Count];
        for (var index = 0; index < failures.Count; index++)
        {
            copy[index] = failures[index] ?? throw new ArgumentException(
                "Failures cannot contain null values.",
                nameof(failures));
        }

        return copy;
    }

    private static string[] ReasonsFrom(IReadOnlyList<EvaluationFailure> failures)
    {
        var reasons = new string[failures.Count];
        for (var index = 0; index < failures.Count; index++)
        {
            reasons[index] = failures[index].Reason;
        }

        return reasons;
    }

    private static bool RequirePassed(bool passed, IReadOnlyList<EvaluationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        var hasFailure = false;
        foreach (var failure in failures)
        {
            if (failure is not null)
            {
                hasFailure = true;
                break;
            }
        }

        if (passed == hasFailure)
        {
            throw new ArgumentException(
                "Passed must be true only when there are no failure reasons.",
                nameof(passed));
        }

        return passed;
    }
}
