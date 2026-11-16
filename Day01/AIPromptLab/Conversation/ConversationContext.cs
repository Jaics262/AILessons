public sealed class ConversationContext
{
    public ConversationContext(List<object> contents, int prefixCount, ConversationSummary? summary)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (prefixCount < 0 || prefixCount >= contents.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prefixCount),
                "The prefix must leave the current prompt in the context.");
        }

        Contents = contents;
        PrefixCount = prefixCount;
        Summary = summary;
    }

    public List<object> Contents { get; }

    public int PrefixCount { get; }

    public ConversationSummary? Summary { get; }
}
