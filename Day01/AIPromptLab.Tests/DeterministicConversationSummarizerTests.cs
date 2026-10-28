namespace AIPromptLab.Tests;

public sealed class DeterministicConversationSummarizerTests
{
    private readonly DeterministicConversationSummarizer _summarizer = new();

    [Fact]
    public void Summarize_extracts_selected_key_facts_in_the_order_they_appear()
    {
        var messages = new List<object>
        {
            User("Decision: use the stored price\nIgnore the colour.\nConstraint: do not invent missing information"),
            FunctionResponse(),
            Model("Completed: calculated the notebook total\nUnresolved: confirm the VAT rate\nDecision: keep the receipt")
        };
        var stored = messages.ToArray();

        var summary = _summarizer.Summarize(messages);

        Assert.Equal(["use the stored price", "keep the receipt"], summary.Decisions);
        Assert.Equal(["do not invent missing information"], summary.Constraints);
        Assert.Equal(["calculated the notebook total"], summary.CompletedActions);
        Assert.Equal(["confirm the VAT rate"], summary.UnresolvedTasks);
        Assert.Same(stored[0], messages[0]);
        Assert.Equal(stored.Length, messages.Count);
        Assert.DoesNotContain("Ignore the colour.", summary.Format(), StringComparison.Ordinal);
    }

    [Fact]
    public void Summarize_trims_fact_values_and_ignores_blank_or_unlabelled_lines()
    {
        var messages = new List<object>
        {
            User("decision: lower case\nDecision:\nConstraint:   stay under the budget  \nCompleted: \nJust a note.")
        };

        var summary = _summarizer.Summarize(messages);

        Assert.Empty(summary.Decisions);
        Assert.Equal(["stay under the budget"], summary.Constraints);
        Assert.Empty(summary.CompletedActions);
        Assert.Empty(summary.UnresolvedTasks);
    }

    [Fact]
    public void Summarize_returns_an_empty_summary_when_the_messages_have_no_key_facts()
    {
        var summary = _summarizer.Summarize([User("The notebook colour is blue."), Model("A stored answer.")]);

        Assert.False(summary.HasFacts);
        Assert.Equal(string.Empty, summary.Format());
    }

    [Fact]
    public void Summarize_rejects_a_null_message_list()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _summarizer.Summarize(null!));

        Assert.Equal("messages", exception.ParamName);
    }

    private static object User(string text)
    {
        return new { role = "user", parts = new object[] { new { text } } };
    }

    private static object Model(string text)
    {
        return new { role = "model", parts = new object[] { new { text } } };
    }

    private static object FunctionResponse()
    {
        var functionResponse = new Dictionary<string, object?>
        {
            ["name"] = "calculate_total",
            ["response"] = new Dictionary<string, decimal> { ["total"] = 19.0m }
        };

        return new { role = "user", parts = new object[] { new { functionResponse } } };
    }
}
