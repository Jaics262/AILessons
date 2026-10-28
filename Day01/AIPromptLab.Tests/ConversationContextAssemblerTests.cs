using System.Text.Json;

namespace AIPromptLab.Tests;

public sealed class ConversationContextAssemblerTests
{
    [Fact]
    public void Assemble_returns_the_prompt_when_the_history_is_empty()
    {
        var history = new List<object>();

        var context = CreateAssembler().Assemble(history, "New question.");

        Assert.Null(context.Summary);
        Assert.Equal(0, context.PrefixCount);
        Assert.Single(context.Contents);
        Assert.Contains("New question.", JsonSerializer.Serialize(context.Contents), StringComparison.Ordinal);
        Assert.Empty(history);
    }

    [Fact]
    public void Assemble_keeps_turns_inside_the_window_verbatim()
    {
        var history = new List<object>();
        AddPlainTurn(history, "Decision: use the stored price");
        AddPlainTurn(history, "Recent question.");
        var stored = history.ToArray();

        var context = CreateAssembler().Assemble(history, "New question.");
        var json = JsonSerializer.Serialize(context.Contents);

        Assert.Null(context.Summary);
        Assert.Equal(stored.Length, context.PrefixCount);
        Assert.DoesNotContain(ConversationSummary.Heading, json, StringComparison.Ordinal);
        Assert.Contains("Decision: use the stored price", json, StringComparison.Ordinal);
        AssertChronological(json, "Decision: use the stored price", "Recent question.", "New question.");
        Assert.Same(stored[0], context.Contents[0]);
        Assert.Same(stored[3], context.Contents[3]);
        AssertSameSequence(stored, history);
    }

    [Fact]
    public void Assemble_summarizes_turns_outside_the_history_budget_and_keeps_the_newest_turn()
    {
        var history = new List<object>();
        AddPlainTurn(history, "Decision: use the stored price\nThe notebook colour is blue.");
        AddPlainTurn(history, "Middle question.");
        AddPlainTurn(history, "Recent question.");
        var stored = history.ToArray();
        var newestTokens = Estimate(stored, 4, 6);

        var context = CreateAssembler(maxContextTurns: 6, maxHistoryTokens: newestTokens)
            .Assemble(history, "New question.");
        var json = JsonSerializer.Serialize(context.Contents);

        Assert.NotNull(context.Summary);
        Assert.Equal(["use the stored price"], context.Summary.Decisions);
        Assert.Equal(3, context.PrefixCount);
        Assert.Same(stored[4], context.Contents[1]);
        Assert.Same(stored[5], context.Contents[2]);
        Assert.DoesNotContain("The notebook colour is blue.", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Middle question.", json, StringComparison.Ordinal);
        AssertChronological(
            json,
            ConversationSummary.Heading,
            "use the stored price",
            "Recent question.",
            "New question.");
        AssertSameSequence(stored, history);
    }

    [Fact]
    public void Assemble_keeps_a_recent_tool_turn_whole_and_leaves_the_history_unchanged()
    {
        var history = new List<object>();
        AddToolTurn(history, "Decision: use the stored price\nThe notebook colour is blue.", "c-old", "sig-old");
        AddToolTurn(history, "Calculate the notebook total.", "c-kept", "sig-kept");
        var stored = history.ToArray();

        var context = CreateAssembler(maxContextTurns: 1).Assemble(history, "New question.");
        var json = JsonSerializer.Serialize(context.Contents);
        var callIndex = json.IndexOf("\"thoughtSignature\":\"sig-kept\"", StringComparison.Ordinal);
        var responseIndex = json.IndexOf("\"functionResponse\"", StringComparison.Ordinal);
        var promptIndex = json.IndexOf("New question.", StringComparison.Ordinal);

        Assert.NotNull(context.Summary);
        Assert.Equal(["use the stored price"], context.Summary.Decisions);
        Assert.Equal(5, context.PrefixCount);
        Assert.Same(stored[4], context.Contents[1]);
        Assert.Same(stored[5], context.Contents[2]);
        Assert.Same(stored[6], context.Contents[3]);
        Assert.Same(stored[7], context.Contents[4]);
        Assert.True(callIndex >= 0 && callIndex < responseIndex && responseIndex < promptIndex);
        Assert.Contains("\"id\":\"c-kept\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("c-old", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sig-old", json, StringComparison.Ordinal);
        Assert.DoesNotContain("The notebook colour is blue.", json, StringComparison.Ordinal);
        AssertChronological(json, ConversationSummary.Heading, "use the stored price", "Calculate the notebook total.", "New question.");
        AssertSameSequence(stored, history);
    }

    [Fact]
    public void Assemble_drops_lower_priority_facts_until_the_summary_fits_the_token_budget()
    {
        var kept = new ConversationSummary(["keep this decision"], [], [], []);
        var budget = ConversationContextAssembler.EstimateSummaryTokens(kept.Format());
        var history = new List<object>();
        AddPlainTurn(
            history,
            string.Join(
                '\n',
                "Decision: keep this decision",
                "Decision: drop this later decision",
                "Constraint: drop this constraint",
                "Completed: drop this completed action",
                "Unresolved: drop this unresolved task"));
        AddPlainTurn(history, "Recent question.");
        var stored = history.ToArray();

        var context = CreateAssembler(maxContextTurns: 1, maxSummaryTokens: budget)
            .Assemble(history, "New question.");
        var json = JsonSerializer.Serialize(context.Contents);

        Assert.NotNull(context.Summary);
        Assert.Equal(["keep this decision"], context.Summary.Decisions);
        Assert.Empty(context.Summary.Constraints);
        Assert.Empty(context.Summary.CompletedActions);
        Assert.Empty(context.Summary.UnresolvedTasks);
        Assert.True(ConversationContextAssembler.EstimateSummaryTokens(context.Summary.Format()) <= budget);
        Assert.True(RecentTurnContextSelector.EstimateTokens(context.Contents[0]) <= budget);
        Assert.DoesNotContain("drop this later decision", json, StringComparison.Ordinal);
        Assert.DoesNotContain("drop this constraint", json, StringComparison.Ordinal);
        Assert.DoesNotContain("drop this completed action", json, StringComparison.Ordinal);
        Assert.DoesNotContain("drop this unresolved task", json, StringComparison.Ordinal);
        AssertChronological(json, ConversationSummary.Heading, "keep this decision", "Recent question.", "New question.");
        Assert.Same(stored[2], context.Contents[1]);
        AssertSameSequence(stored, history);
    }

    [Fact]
    public void Assemble_omits_the_summary_when_every_fact_exceeds_the_token_budget()
    {
        var history = new List<object>();
        AddPlainTurn(history, "Decision: use the stored price");
        AddPlainTurn(history, "Recent question.");
        var stored = history.ToArray();

        var context = CreateAssembler(maxContextTurns: 1, maxSummaryTokens: 1).Assemble(history, "New question.");
        var json = JsonSerializer.Serialize(context.Contents);

        Assert.Null(context.Summary);
        Assert.Equal(2, context.PrefixCount);
        Assert.DoesNotContain(ConversationSummary.Heading, json, StringComparison.Ordinal);
        Assert.DoesNotContain("use the stored price", json, StringComparison.Ordinal);
        AssertChronological(json, "Recent question.", "New question.");
        Assert.Same(stored[2], context.Contents[0]);
        AssertSameSequence(stored, history);
    }

    [Fact]
    public void Assemble_rejects_a_null_history()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CreateAssembler().Assemble(null!, "New question."));

        Assert.Equal("messages", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Assemble_rejects_a_blank_prompt(string? prompt)
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateAssembler().Assemble([], prompt!));

        Assert.Equal("prompt", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_a_summary_budget_below_one(int maxSummaryTokens)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConversationContextAssembler(
                new RecentTurnContextSelector(1),
                new DeterministicConversationSummarizer(),
                maxSummaryTokens));

        Assert.Equal("maxSummaryTokens", exception.ParamName);
    }

    private static ConversationContextAssembler CreateAssembler(
        int maxContextTurns = 6,
        int maxHistoryTokens = PromptLabSettings.DefaultMaxHistoryTokens,
        int maxSummaryTokens = PromptLabSettings.DefaultMaxSummaryTokens)
    {
        return new ConversationContextAssembler(
            new RecentTurnContextSelector(maxContextTurns, maxHistoryTokens),
            new DeterministicConversationSummarizer(),
            maxSummaryTokens);
    }

    private static void AddPlainTurn(List<object> history, string prompt)
    {
        history.Add(User(prompt));
        history.Add(Model("answer " + prompt));
    }

    private static void AddToolTurn(List<object> history, string prompt, string id, string signature)
    {
        history.Add(User(prompt));
        history.Add(FunctionCall(id, signature));
        history.Add(FunctionResponse(id));
        history.Add(Model("answer " + prompt));
    }

    private static int Estimate(IReadOnlyList<object> messages, int start, int end)
    {
        var tokens = 0;
        for (var index = start; index < end; index++)
        {
            tokens += RecentTurnContextSelector.EstimateTokens(messages[index]);
        }

        return tokens;
    }

    private static void AssertSameSequence(object[] expected, IReadOnlyList<object> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Same(expected[index], actual[index]);
        }
    }

    private static void AssertChronological(string json, params string[] tokens)
    {
        var previous = -1;
        foreach (var token in tokens)
        {
            var index = json.IndexOf(token, StringComparison.Ordinal);
            Assert.True(index > previous);
            previous = index;
        }
    }

    private static object User(string text)
    {
        return new { role = "user", parts = new object[] { new { text } } };
    }

    private static object Model(string text)
    {
        return new { role = "model", parts = new object[] { new { text } } };
    }

    private static object FunctionCall(string id, string signature)
    {
        var functionCall = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = "calculate_total",
            ["args"] = JsonSerializer.SerializeToElement(new { quantity = 2, unitPrice = 9.5 })
        };

        return new
        {
            role = "model",
            parts = new object[] { new { functionCall, thoughtSignature = signature } }
        };
    }

    private static object FunctionResponse(string id)
    {
        var functionResponse = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = "calculate_total",
            ["response"] = new Dictionary<string, decimal> { ["total"] = 19.0m }
        };

        return new { role = "user", parts = new object[] { new { functionResponse } } };
    }
}
