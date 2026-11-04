// Grounds answers in the controlled comma-separated claim format.
// A claim passes only when a predefined mapping names it and the context contains that mapping's evidence sentence.
public static class AnswerSupport
{
    public static AnswerSupportResult Evaluate(string context, string answer) =>
        Evaluate(context, answer, AnswerSupportExample.Mappings);

    public static AnswerSupportResult Evaluate(
        string context,
        string answer,
        IReadOnlyList<ClaimEvidence> mappings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(answer);
        var evidenceByClaim = Index(mappings);

        var claims = new List<AnswerSupportResult.Claim>();
        foreach (var part in answer.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var text = NormalizeClaim(part);
            if (text.Length == 0)
            {
                continue;
            }

            var supported = evidenceByClaim.TryGetValue(text, out var evidence)
                && context.Contains(evidence, StringComparison.Ordinal);
            claims.Add(new AnswerSupportResult.Claim(text, supported));
        }

        if (claims.Count == 0)
        {
            throw new ArgumentException("The answer must contain at least one claim.", nameof(answer));
        }

        return AnswerSupportResult.Create(claims);
    }

    private static Dictionary<string, string> Index(IReadOnlyList<ClaimEvidence> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);

        var evidenceByClaim = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var mapping in mappings)
        {
            ArgumentNullException.ThrowIfNull(mapping);
            var claim = NormalizeClaim(mapping.Claim);
            if (claim.Length == 0)
            {
                throw new ArgumentException("A claim mapping cannot be blank.", nameof(mappings));
            }

            if (!evidenceByClaim.TryAdd(claim, mapping.Evidence))
            {
                throw new ArgumentException($"The claim '{claim}' is mapped more than once.", nameof(mappings));
            }
        }

        return evidenceByClaim;
    }

    private static string NormalizeClaim(string claim)
    {
        var text = claim.Trim().TrimEnd('.', '?', '!');
        if (text.StartsWith("and ", StringComparison.OrdinalIgnoreCase))
        {
            text = text[4..].TrimStart();
        }

        return text;
    }
}
