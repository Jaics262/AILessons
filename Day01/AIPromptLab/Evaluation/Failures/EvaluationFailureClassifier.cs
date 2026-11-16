// Maps a failure code to a category. The readable reason is not parsed.
public static class EvaluationFailureClassifier
{
    public static EvaluationFailureCategory Classify(EvaluationFailureCode code)
    {
        return code switch
        {
            EvaluationFailureCode.UngroundedClaim => EvaluationFailureCategory.UngroundedClaim,
            EvaluationFailureCode.MissingExpectedFact => EvaluationFailureCategory.MissingExpectedFact,
            EvaluationFailureCode.EmptyAnswer => EvaluationFailureCategory.EmptyAnswer,
            // A claim-less answer is not a blank string and does not name a missing fact.
            EvaluationFailureCode.NoClaim => EvaluationFailureCategory.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(code), "Failure code must be a defined value.")
        };
    }

    public static IReadOnlyList<EvaluationFailureCategory> Classify(IReadOnlyList<EvaluationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        var categories = new EvaluationFailureCategory[failures.Count];
        for (var index = 0; index < failures.Count; index++)
        {
            var failure = failures[index] ?? throw new ArgumentException(
                "Failures cannot contain null values.",
                nameof(failures));
            categories[index] = Classify(failure.Code);
        }

        return categories;
    }
}
