using System.Text.Json;

namespace AIPromptLab.Tests;

public sealed class InMemoryConversationHistoryTests
{
    [Fact]
    public void Replace_keeps_the_summary_separate_from_the_messages()
    {
        var history = new InMemoryConversationHistory();
        var summary = new ConversationSummary(
            ["use the stored price"],
            ["do not invent missing information"],
            ["calculated the notebook total"],
            ["confirm the VAT rate"]);
        var message = new { role = "user", parts = new object[] { new { text = "Recent question." } } };

        history.ReplaceSummary(summary);
        history.Replace([message]);

        Assert.Same(summary, history.Summary);
        Assert.Same(message, Assert.Single(history.Messages));
        Assert.DoesNotContain(ConversationSummary.Heading, JsonSerializer.Serialize(history.Messages), StringComparison.Ordinal);
        Assert.Contains("use the stored price", summary.Format(), StringComparison.Ordinal);

        history.ReplaceSummary(null);

        Assert.Null(history.Summary);
        Assert.Same(message, Assert.Single(history.Messages));
    }

    [Fact]
    public void Reset_clears_the_summary_and_the_messages()
    {
        var history = new InMemoryConversationHistory();
        history.Replace([new { role = "user", parts = new object[] { new { text = "Recent question." } } }]);
        history.ReplaceSummary(new ConversationSummary(["use the stored price"], [], [], []));

        history.Reset();

        Assert.Empty(history.Messages);
        Assert.Null(history.Summary);
    }
}
