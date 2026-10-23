public interface IConversationHistory
{
    IReadOnlyList<object> Messages { get; }

    void Replace(IEnumerable<object> messages);

    void Reset();
}
