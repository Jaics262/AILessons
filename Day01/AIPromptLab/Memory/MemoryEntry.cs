public sealed record MemoryEntry(string Id, string Text, DateTimeOffset CreatedAt)
{
    public string Id { get; init; } = Require(Id, nameof(Id));

    public string Text { get; init; } = Require(Text, nameof(Text));

    public DateTimeOffset CreatedAt { get; init; } = CreatedAt;

    private static string Require(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }
}
