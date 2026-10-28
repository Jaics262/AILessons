public sealed class ConversationContextAssembler
{
    private readonly RecentTurnContextSelector _selector;
    private readonly IConversationSummarizer _summarizer;

    public ConversationContextAssembler(
        RecentTurnContextSelector selector,
        IConversationSummarizer summarizer,
        int maxSummaryTokens = PromptLabSettings.DefaultMaxSummaryTokens)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(summarizer);

        if (maxSummaryTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSummaryTokens),
                "The maximum summary token budget must be greater than zero.");
        }

        _selector = selector;
        _summarizer = summarizer;
        MaxSummaryTokens = maxSummaryTokens;
    }

    public int MaxSummaryTokens { get; }

    public ConversationContext Assemble(
        IReadOnlyList<object> messages,
        string prompt,
        Func<string, string>? redact = null)
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

            contents.Add(CreateSummaryMessage(text));
        }

        contents.AddRange(recentTurns);
        var prefixCount = contents.Count;
        contents.Add(CreatePromptMessage(prompt));
        return new ConversationContext(contents, prefixCount, summary);
    }

    public static int EstimateSummaryTokens(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return RecentTurnContextSelector.EstimateTokens(CreateSummaryMessage(text));
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

    private static object CreateSummaryMessage(string text)
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
