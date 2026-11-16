// Scores answer cases with the Day 16 groundedness mappings.
// A case passes when every claim is supported and a supported claim states each expected fact.
// An unsupported claim that uses an expected fact's wording is reported once, as unsupported.
// Each failure is stored as a code. Classification maps that code to a category, and the report still prints the readable reason.
public static class EvaluationPipeline
{
    public static EvaluationReport Evaluate(IReadOnlyList<EvaluationCase> cases) =>
        Evaluate(cases, EvaluationThresholds.Default, AnswerSupportExample.Mappings);

    public static EvaluationReport Evaluate(
        IReadOnlyList<EvaluationCase> cases,
        EvaluationThresholds thresholds) =>
        Evaluate(cases, thresholds, AnswerSupportExample.Mappings);

    public static EvaluationReport Evaluate(
        IReadOnlyList<EvaluationCase> cases,
        EvaluationThresholds thresholds,
        IReadOnlyList<ClaimEvidence> mappings)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(thresholds);
        if (cases.Count == 0)
        {
            return new EvaluationReport(0, 0, 0, false, [], thresholds);
        }

        var evidenceByClaim = AnswerSupport.Index(mappings);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new EvaluationCaseResult[cases.Count];
        var passedCases = 0;
        for (var index = 0; index < cases.Count; index++)
        {
            var item = cases[index] ?? throw new ArgumentException(
                "Evaluation cases cannot contain null values.",
                nameof(cases));
            if (!seen.Add(item.Id))
            {
                throw new ArgumentException($"The case '{item.Id}' is listed more than once.", nameof(cases));
            }

            var result = EvaluateCase(item, evidenceByClaim, mappings);
            results[index] = result;
            if (result.Passed)
            {
                passedCases++;
            }
        }

        var failedCases = results.Length - passedCases;
        return new EvaluationReport(
            results.Length,
            passedCases,
            failedCases,
            thresholds.IsMet(passedCases, results.Length),
            results,
            thresholds);
    }

    private static EvaluationCaseResult EvaluateCase(
        EvaluationCase item,
        IReadOnlyDictionary<string, string> evidenceByClaim,
        IReadOnlyList<ClaimEvidence> mappings)
    {
        if (string.IsNullOrWhiteSpace(item.Answer))
        {
            return Classify(item.Id, [EvaluationFailure.EmptyAnswer()]);
        }

        AnswerSupportResult support;
        try
        {
            support = AnswerSupport.Evaluate(item.Context, item.Answer, mappings);
        }
        catch (ArgumentException exception) when (exception.ParamName == "answer")
        {
            return Classify(item.Id, [EvaluationFailure.NoClaim()]);
        }

        var failures = new List<EvaluationFailure>();
        foreach (var claim in support.Claims)
        {
            if (!claim.IsSupported)
            {
                failures.Add(EvaluationFailure.UngroundedClaim(claim.Text));
            }
        }

        foreach (var fact in item.ExpectedFacts)
        {
            if (!IsStated(support, evidenceByClaim, fact))
            {
                failures.Add(EvaluationFailure.MissingExpectedFact(fact));
            }
        }

        return Classify(item.Id, failures);
    }

    private static EvaluationCaseResult Classify(string caseId, IReadOnlyList<EvaluationFailure> failures)
    {
        var categories = EvaluationFailureClassifier.Classify(failures);
        return new EvaluationCaseResult(caseId, categories.Count == 0, failures);
    }

    private static bool IsStated(
        AnswerSupportResult support,
        IReadOnlyDictionary<string, string> evidenceByClaim,
        string expectedFact)
    {
        var fact = AnswerSupport.NormalizeClaim(expectedFact);
        foreach (var claim in support.Claims)
        {
            if (string.Equals(claim.Text, fact, StringComparison.Ordinal))
            {
                return true;
            }

            if (evidenceByClaim.TryGetValue(claim.Text, out var evidence)
                && string.Equals(AnswerSupport.NormalizeClaim(evidence), fact, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
