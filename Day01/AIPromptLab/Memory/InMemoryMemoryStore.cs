public sealed class InMemoryMemoryStore
{
    private readonly List<MemoryEntry> _entries = [];

    public void Add(MemoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (_entries.Exists(existing => string.Equals(existing.Id, entry.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"A memory entry with id '{entry.Id}' already exists.");
        }

        _entries.Add(entry);
    }

    public IReadOnlyList<MemoryEntry> Retrieve()
    {
        return _entries.ToList();
    }

    public MemoryEntry? Find(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _entries.Find(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
    }
}
