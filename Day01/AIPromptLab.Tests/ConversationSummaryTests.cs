namespace AIPromptLab.Tests;

public sealed class ConversationSummaryTests
{
    [Fact]
    public void Constructor_copies_the_supplied_facts()
    {
        var decisions = new List<string> { "use the stored price" };
        var constraints = new List<string> { "do not invent missing information" };
        var completedActions = new List<string> { "calculated the notebook total" };
        var unresolvedTasks = new List<string> { "confirm the VAT rate" };

        var summary = new ConversationSummary(decisions, constraints, completedActions, unresolvedTasks);
        decisions[0] = "changed";
        constraints.Add("another constraint");
        completedActions.Clear();
        unresolvedTasks[0] = "changed";

        Assert.Equal(["use the stored price"], summary.Decisions);
        Assert.Equal(["do not invent missing information"], summary.Constraints);
        Assert.Equal(["calculated the notebook total"], summary.CompletedActions);
        Assert.Equal(["confirm the VAT rate"], summary.UnresolvedTasks);
        Assert.True(summary.HasFacts);
    }

    [Fact]
    public void Format_lists_only_the_sections_that_have_facts()
    {
        var summary = new ConversationSummary(
            ["use the stored price", "keep the receipt"],
            [],
            ["calculated the notebook total"],
            ["confirm the VAT rate"]);

        var text = summary.Format();
        var decisions = text.IndexOf(ConversationSummary.DecisionsSection, StringComparison.Ordinal);
        var completed = text.IndexOf(ConversationSummary.CompletedSection, StringComparison.Ordinal);
        var unresolved = text.IndexOf(ConversationSummary.UnresolvedSection, StringComparison.Ordinal);

        Assert.StartsWith(ConversationSummary.Heading, text, StringComparison.Ordinal);
        Assert.DoesNotContain(ConversationSummary.ConstraintsSection, text, StringComparison.Ordinal);
        Assert.True(decisions >= 0 && decisions < completed && completed < unresolved);
        Assert.Contains("- use the stored price", text, StringComparison.Ordinal);
        Assert.Contains("- keep the receipt", text, StringComparison.Ordinal);
        Assert.Contains("- calculated the notebook total", text, StringComparison.Ordinal);
        Assert.Contains("- confirm the VAT rate", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_returns_an_empty_string_when_there_are_no_facts()
    {
        var summary = new ConversationSummary([], [], [], []);

        Assert.False(summary.HasFacts);
        Assert.Equal(string.Empty, summary.Format());
    }

    [Theory]
    [InlineData(0, "decisions")]
    [InlineData(1, "constraints")]
    [InlineData(2, "completedActions")]
    [InlineData(3, "unresolvedTasks")]
    public void Constructor_rejects_a_null_fact_list(int index, string paramName)
    {
        var lists = new IReadOnlyList<string>?[] { ["kept"], ["kept"], ["kept"], ["kept"] };
        lists[index] = null;

        var exception = Assert.Throws<ArgumentNullException>(
            () => new ConversationSummary(lists[0]!, lists[1]!, lists[2]!, lists[3]!));

        Assert.Equal(paramName, exception.ParamName);
    }

    [Theory]
    [InlineData(0, "decisions")]
    [InlineData(1, "constraints")]
    [InlineData(2, "completedActions")]
    [InlineData(3, "unresolvedTasks")]
    public void Constructor_rejects_a_blank_fact(int index, string paramName)
    {
        var lists = new List<string>[]
        {
            ["use the stored price"],
            ["do not invent missing information"],
            ["calculated the notebook total"],
            ["confirm the VAT rate"]
        };
        lists[index] = [" "];

        var exception = Assert.Throws<ArgumentException>(
            () => new ConversationSummary(lists[0], lists[1], lists[2], lists[3]));

        Assert.Equal(paramName, exception.ParamName);
    }
}
