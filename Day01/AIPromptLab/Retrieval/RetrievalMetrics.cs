public static class RetrievalMetrics
{
    // Precision@K = relevant memories among the first K retrieved / those retrieved memories.
    // Fewer than K retrieved uses that shorter count as the denominator. An empty list scores 0.
    public static double PrecisionAtK(
        IReadOnlyList<string> retrievedIds,
        IReadOnlyList<string> relevantIds,
        int k)
    {
        ArgumentNullException.ThrowIfNull(retrievedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);
        RequireK(k);

        var relevant = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in relevantIds)
        {
            relevant.Add(RequireId(id, nameof(relevantIds)));
        }

        var scored = Math.Min(k, retrievedIds.Count);
        if (scored == 0)
        {
            return 0d;
        }

        var relevantRetrieved = 0;
        for (var index = 0; index < scored; index++)
        {
            if (relevant.Contains(RequireId(retrievedIds[index], nameof(retrievedIds))))
            {
                relevantRetrieved++;
            }
        }

        return relevantRetrieved / (double)scored;
    }

    // Recall@K = relevant memories found in the first K retrieved / expected relevant memories.
    // Each expected id counts once. An empty retrieved list scores 0.
    public static double RecallAtK(
        IReadOnlyList<string> retrievedIds,
        IReadOnlyList<string> relevantIds,
        int k)
    {
        ArgumentNullException.ThrowIfNull(retrievedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);
        RequireK(k);

        var relevant = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in relevantIds)
        {
            relevant.Add(RequireId(id, nameof(relevantIds)));
        }

        if (relevant.Count == 0)
        {
            throw new ArgumentException("At least one expected memory id is required.", nameof(relevantIds));
        }

        var found = new HashSet<string>(StringComparer.Ordinal);
        var scored = Math.Min(k, retrievedIds.Count);
        for (var index = 0; index < scored; index++)
        {
            var id = RequireId(retrievedIds[index], nameof(retrievedIds));
            if (relevant.Contains(id))
            {
                found.Add(id);
            }
        }

        return found.Count / (double)relevant.Count;
    }

    private static void RequireK(int k)
    {
        if (k < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "K must be at least 1.");
        }
    }

    private static string RequireId(string id, string paramName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Memory ids cannot contain null or blank values.", paramName);
        }

        return id;
    }
}
