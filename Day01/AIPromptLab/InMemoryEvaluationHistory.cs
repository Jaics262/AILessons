// In-memory history of completed evaluation runs. Nothing here is written to a file or a database.
// Runs come back from the earliest EvaluatedAt to the latest. Equal times stay in the order they were added.
// Category counts are a dictionary behind IReadOnlyDictionary. Add stores a new snapshot, and each read returns
// another copy, so the stored counts stay at the values from Add.
public sealed class InMemoryEvaluationHistory
{
    private readonly List<EvaluationSnapshot> _runs = [];
    private readonly Dictionary<string, EvaluationSnapshot> _byRunId = new(StringComparer.Ordinal);

    public void Add(EvaluationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_byRunId.ContainsKey(snapshot.RunId))
        {
            throw new InvalidOperationException(
                $"An evaluation snapshot with run id '{snapshot.RunId}' already exists.");
        }

        var stored = Copy(snapshot);
        _runs.Add(stored);
        _byRunId.Add(stored.RunId, stored);
    }

    public IReadOnlyList<EvaluationSnapshot> Retrieve()
    {
        if (_runs.Count == 0)
        {
            return [];
        }

        var order = new int[_runs.Count];
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        Array.Sort(order, CompareRuns);
        var ordered = new EvaluationSnapshot[order.Length];
        for (var index = 0; index < order.Length; index++)
        {
            ordered[index] = Copy(_runs[order[index]]);
        }

        return ordered;
    }

    public EvaluationSnapshot? Find(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return _byRunId.TryGetValue(runId.Trim(), out var snapshot) ? Copy(snapshot) : null;
    }

    private int CompareRuns(int left, int right)
    {
        var byTime = _runs[left].EvaluatedAt.CompareTo(_runs[right].EvaluatedAt);
        return byTime != 0 ? byTime : left.CompareTo(right);
    }

    private static EvaluationSnapshot Copy(EvaluationSnapshot snapshot)
    {
        return new EvaluationSnapshot(
            snapshot.RunId,
            snapshot.EvaluatedAt,
            snapshot.TotalCases,
            snapshot.FailedCases,
            snapshot.FailureRate,
            snapshot.CategoryCounts);
    }
}
