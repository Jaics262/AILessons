public sealed class ConversationContextAssembler
{
    public const string RetrievedMemoriesHeading = "Retrieved memories";

    private readonly RecentTurnContextSelector _selector;
    private readonly IConversationSummarizer _summarizer;

    public ConversationContextAssembler(
        RecentTurnContextSelector selector,
        IConversationSummarizer summarizer,
        int maxSummaryTokens = PromptLabSettings.DefaultMaxSummaryTokens,
        int maxMemoryTokens = PromptLabSettings.DefaultMaxMemoryTokens)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(summarizer);

        if (maxSummaryTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSummaryTokens),
                "The maximum summary token budget must be greater than zero.");
        }

        if (maxMemoryTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMemoryTokens),
                "The maximum memory token budget must be greater than zero.");
        }

        _selector = selector;
        _summarizer = summarizer;
        MaxSummaryTokens = maxSummaryTokens;
        MaxMemoryTokens = maxMemoryTokens;
    }

    public int MaxSummaryTokens { get; }

    public int MaxMemoryTokens { get; }

    public ConversationContext Assemble(
        IReadOnlyList<object> messages,
        string prompt,
        Func<string, string>? redact = null,
        IReadOnlyList<MemoryEntry>? memories = null)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("The prompt cannot be empty.", nameof(prompt));
        }

        var recentTurns = _selector.Select(messages);
        var omittedCount = messages.Count - recentTurns.Count;
        ConversationSummary? summary = null;
        if (omittedCount > 0)
        {
            summary = FitSummary(_summarizer.Summarize(messages.Take(omittedCount).ToList()), redact);
        }

        var contents = new List<object>();
        if (summary is not null)
        {
            var text = summary.Format();
            if (redact is not null)
            {
                text = redact(text);
            }

            contents.Add(CreateTextMessage(text));
        }

        var retrievedMemories = FitMemories(memories, redact);
        if (retrievedMemories.Count > 0)
        {
            var text = FormatMemories(retrievedMemories);
            if (redact is not null)
            {
                text = redact(text);
            }

            contents.Add(CreateTextMessage(text));
        }

        contents.AddRange(recentTurns);
        var prefixCount = contents.Count;
        contents.Add(CreatePromptMessage(prompt));
        return new ConversationContext(contents, prefixCount, summary);
    }

    public static int EstimateSummaryTokens(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return RecentTurnContextSelector.EstimateTokens(CreateTextMessage(text));
    }

    public static int EstimateMemoryTokens(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return RecentTurnContextSelector.EstimateTokens(CreateTextMessage(text));
    }

    private ConversationSummary? FitSummary(ConversationSummary summary, Func<string, string>? redact)
    {
        var fitted = summary;
        while (fitted.HasFacts)
        {
            var text = fitted.Format();
            if (redact is not null)
            {
                text = redact(text);
            }

            if (!string.IsNullOrWhiteSpace(text) && EstimateSummaryTokens(text) <= MaxSummaryTokens)
            {
                return fitted;
            }

            fitted = DropLastFact(fitted);
        }

        return null;
    }

    private static ConversationSummary DropLastFact(ConversationSummary summary)
    {
        var decisions = summary.Decisions.ToList();
        var constraints = summary.Constraints.ToList();
        var completedActions = summary.CompletedActions.ToList();
        var unresolvedTasks = summary.UnresolvedTasks.ToList();
        if (unresolvedTasks.Count > 0)
        {
            unresolvedTasks.RemoveAt(unresolvedTasks.Count - 1);
        }
        else if (completedActions.Count > 0)
        {
            completedActions.RemoveAt(completedActions.Count - 1);
        }
        else if (constraints.Count > 0)
        {
            constraints.RemoveAt(constraints.Count - 1);
        }
        else
        {
            decisions.RemoveAt(decisions.Count - 1);
        }

        return new ConversationSummary(decisions, constraints, completedActions, unresolvedTasks);
    }

    private List<MemoryEntry> FitMemories(IReadOnlyList<MemoryEntry>? memories, Func<string, string>? redact)
    {
        var fitted = new List<MemoryEntry>();
        if (memories is null)
        {
            return fitted;
        }

        foreach (var memory in memories)
        {
            ArgumentNullException.ThrowIfNull(memory);
            fitted.Add(memory);
        }

        while (fitted.Count > 0)
        {
            var text = FormatMemories(fitted);
            if (redact is not null)
            {
                text = redact(text);
            }

            if (!string.IsNullOrWhiteSpace(text) && EstimateMemoryTokens(text) <= MaxMemoryTokens)
            {
                return fitted;
            }

            fitted.RemoveAt(fitted.Count - 1);
        }

        return fitted;
    }

    private static string FormatMemories(IReadOnlyList<MemoryEntry> memories)
    {
        var lines = new List<string> { RetrievedMemoriesHeading };
        foreach (var memory in memories)
        {
            lines.Add("- " + memory.Text);
        }

        return string.Join('\n', lines);
    }

    private static object CreateTextMessage(string text)
    {
        return new
        {
            role = "user",
            parts = new object[] { new { text } }
        };
    }

    private static object CreatePromptMessage(string prompt)
    {
        return new
        {
            role = "user",
            parts = new object[] { new { text = prompt } }
        };
    }
}
