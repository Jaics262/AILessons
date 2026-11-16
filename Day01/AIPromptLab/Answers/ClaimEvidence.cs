// A controlled claim and the context sentence that supports it.
public sealed record ClaimEvidence(string Claim, string Evidence)
{
    public string Claim { get; init; } = Require(Claim, nameof(Claim));

    public string Evidence { get; init; } = Require(Evidence, nameof(Evidence));

    private static string Require(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim();
    }
}
