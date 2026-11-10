public sealed class AnswerCitationResult
{
    private AnswerCitationResult(IReadOnlyList<CitedClaim> claims, IReadOnlyList<string> uncitedClaims)
    {
        Claims = claims;
        UncitedClaims = uncitedClaims;
    }

    public bool IsFullyCited => UncitedClaims.Count == 0;

    public IReadOnlyList<CitedClaim> Claims { get; }

    public IReadOnlyList<string> UncitedClaims { get; }

    public string Report
    {
        get
        {
            var lines = new List<string>
            {
                $"Answer citations: {(IsFullyCited ? "COMPLETE" : "INCOMPLETE")}"
            };

            foreach (var claim in Claims)
            {
                lines.Add($"- {claim.Text}: {claim.Evidence ?? "(no citation)"}");
            }

            return string.Join('\n', lines);
        }
    }

    public override string ToString() => Report;

    internal static AnswerCitationResult Create(IReadOnlyList<CitedClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        if (claims.Count == 0)
        {
            throw new ArgumentException("At least one claim is required.", nameof(claims));
        }

        var copy = new CitedClaim[claims.Count];
        var uncited = new List<string>();
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i] ?? throw new ArgumentException("A cited claim cannot be null.", nameof(claims));
            copy[i] = claim;
            if (!claim.IsCited)
            {
                uncited.Add(claim.Text);
            }
        }

        return new AnswerCitationResult(copy, uncited);
    }
}
