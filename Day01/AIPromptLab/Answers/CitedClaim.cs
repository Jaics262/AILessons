// One controlled claim and the mapped evidence sentence that cites it.
// A null evidence sentence means the claim has no citation.
public sealed record CitedClaim
{
    public CitedClaim(string text, string? evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (evidence is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
            evidence = evidence.Trim();
        }

        Text = text.Trim();
        Evidence = evidence;
    }

    public string Text { get; }

    public string? Evidence { get; }

    public bool IsCited => Evidence is not null;
}
