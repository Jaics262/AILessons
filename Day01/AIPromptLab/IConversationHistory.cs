public interface IConversationHistory
{
    IReadOnlyList<object> Messages { get; }

    ConversationSummary? Summary { get; }

    void Replace(IEnumerable<object> messages);

    void ReplaceSummary(ConversationSummary? summary);

    void Reset();
}
