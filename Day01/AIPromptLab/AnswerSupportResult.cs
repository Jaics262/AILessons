public sealed class AnswerSupportResult
{
    private AnswerSupportResult(IReadOnlyList<Claim> claims, IReadOnlyList<string> unsupportedClaims)
    {
        Claims = claims;
        UnsupportedClaims = unsupportedClaims;
    }

    public bool IsGrounded => UnsupportedClaims.Count == 0;

    public bool IsSupported => IsGrounded;

    public IReadOnlyList<Claim> Claims { get; }

    public IReadOnlyList<string> UnsupportedClaims { get; }

    public sealed record Claim(string Text, bool IsSupported);

    internal static AnswerSupportResult Create(IReadOnlyList<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        var unsupported = new List<string>();
        foreach (var claim in claims)
        {
            ArgumentNullException.ThrowIfNull(claim);
            if (!claim.IsSupported)
            {
                unsupported.Add(claim.Text);
            }
        }

        return new AnswerSupportResult(claims, unsupported);
    }
}
