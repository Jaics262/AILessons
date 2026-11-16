// Cites each controlled claim with the mapped evidence sentence from Day 16.
// The same words in the context are not a citation unless that mapping names the claim.
public static class AnswerCitation
{
    public static AnswerCitationResult Cite(string context, string answer) =>
        Cite(context, answer, AnswerSupportExample.Mappings);

    public static AnswerCitationResult Cite(
        string context,
        string answer,
        IReadOnlyList<ClaimEvidence> mappings)
    {
        var support = AnswerSupport.Evaluate(context, answer, mappings);
        var evidenceByClaim = AnswerSupport.Index(mappings);

        var cited = new List<CitedClaim>(support.Claims.Count);
        foreach (var claim in support.Claims)
        {
            string? evidence = null;
            if (claim.IsSupported
                && evidenceByClaim.TryGetValue(claim.Text, out var mapped)
                && context.Contains(mapped, StringComparison.Ordinal))
            {
                evidence = mapped;
            }

            cited.Add(new CitedClaim(claim.Text, evidence));
        }

        return AnswerCitationResult.Create(cited);
    }
}
