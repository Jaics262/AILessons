public sealed class InMemoryConversationHistory : IConversationHistory
{
    private readonly List<object> _messages = [];

    public IReadOnlyList<object> Messages => _messages;

    public void Replace(IEnumerable<object> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var copy = messages.ToList();
        _messages.Clear();
        _messages.AddRange(copy);
    }

    public void Reset()
    {
        _messages.Clear();
    }
}
