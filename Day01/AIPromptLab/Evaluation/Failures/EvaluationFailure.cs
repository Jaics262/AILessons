public sealed record EvaluationFailure(EvaluationFailureCode Code, string Reason)
{
    public EvaluationFailureCode Code { get; init; } = RequireCode(Code);

    public string Reason { get; init; } = RequireReason(Reason);

    public EvaluationFailureCategory Category => EvaluationFailureClassifier.Classify(Code);

    public static EvaluationFailure EmptyAnswer() =>
        new(EvaluationFailureCode.EmptyAnswer, "The answer is empty.");

    public static EvaluationFailure NoClaim() =>
        new(EvaluationFailureCode.NoClaim, "The answer must contain at least one claim.");

    public static EvaluationFailure UngroundedClaim(string claim)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claim);
        return new(
            EvaluationFailureCode.UngroundedClaim,
            $"The claim '{claim}' is not supported by the context.");
    }

    public static EvaluationFailure MissingExpectedFact(string fact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fact);
        return new(
            EvaluationFailureCode.MissingExpectedFact,
            $"The answer misses the expected fact '{fact}'.");
    }

    private static EvaluationFailureCode RequireCode(EvaluationFailureCode code)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), "Failure code must be a defined value.");
        }

        return code;
    }

    private static string RequireReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return reason.Trim();
    }
}
