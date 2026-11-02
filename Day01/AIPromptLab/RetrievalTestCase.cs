public sealed record RetrievalTestCase(string Query, string[] ExpectedMemoryIds)
{
    public string Query { get; init; } = RequireQuery(Query);

    public string[] ExpectedMemoryIds { get; init; } = CopyIds(ExpectedMemoryIds);

    private static string RequireQuery(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return query.Trim();
    }

    private static string[] CopyIds(string[] expectedMemoryIds)
    {
        ArgumentNullException.ThrowIfNull(expectedMemoryIds);

        if (expectedMemoryIds.Length == 0)
        {
            throw new ArgumentException("At least one expected memory id is required.", nameof(expectedMemoryIds));
        }

        var copy = new string[expectedMemoryIds.Length];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < expectedMemoryIds.Length; index++)
        {
            var id = expectedMemoryIds[index];
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Expected memory ids cannot contain null or blank values.",
                    nameof(expectedMemoryIds));
            }

            var trimmed = id.Trim();
            if (!seen.Add(trimmed))
            {
                throw new ArgumentException("Expected memory ids must be unique.", nameof(expectedMemoryIds));
            }

            copy[index] = trimmed;
        }

        return copy;
    }
}
