public sealed class InMemoryConversationHistory : IConversationHistory
{
    private readonly List<object> _messages = [];
    private ConversationSummary? _summary;

    public IReadOnlyList<object> Messages => _messages;

    public ConversationSummary? Summary => _summary;

    public void Replace(IEnumerable<object> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var copy = messages.ToList();
        _messages.Clear();
        _messages.AddRange(copy);
    }

    public void ReplaceSummary(ConversationSummary? summary)
    {
        _summary = summary;
    }

    public void Reset()
    {
        _messages.Clear();
        _summary = null;
    }
}
