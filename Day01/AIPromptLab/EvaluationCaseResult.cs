public sealed record EvaluationCaseResult(
    string CaseId,
    bool Passed,
    IReadOnlyList<string> FailureReasons)
{
    public string CaseId { get; init; } = RequireCaseId(CaseId);

    public IReadOnlyList<string> FailureReasons { get; init; } = CopyReasons(FailureReasons);

    public bool Passed { get; init; } = RequirePassed(Passed, FailureReasons);

    private static string RequireCaseId(string caseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        return caseId.Trim();
    }

    private static IReadOnlyList<string> CopyReasons(IReadOnlyList<string> failureReasons)
    {
        ArgumentNullException.ThrowIfNull(failureReasons);
        var copy = new string[failureReasons.Count];
        for (var index = 0; index < failureReasons.Count; index++)
        {
            var reason = failureReasons[index];
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "Failure reasons cannot contain null or blank values.",
                    nameof(failureReasons));
            }

            copy[index] = reason.Trim();
        }

        return copy;
    }

    private static bool RequirePassed(bool passed, IReadOnlyList<string> failureReasons)
    {
        ArgumentNullException.ThrowIfNull(failureReasons);
        var hasReason = false;
        foreach (var reason in failureReasons)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                hasReason = true;
                break;
            }
        }

        if (passed == hasReason)
        {
            throw new ArgumentException(
                "Passed must be true only when there are no failure reasons.",
                nameof(passed));
        }

        return passed;
    }
}
