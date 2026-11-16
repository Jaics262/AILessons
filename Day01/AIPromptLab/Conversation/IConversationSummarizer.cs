public interface IConversationSummarizer
{
    ConversationSummary Summarize(IReadOnlyList<object> messages);
}
