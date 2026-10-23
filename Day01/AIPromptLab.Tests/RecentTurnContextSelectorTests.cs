using System.Text.Json;

namespace AIPromptLab.Tests;

public sealed class RecentTurnContextSelectorTests
{
    [Fact]
    public void Select_returns_an_empty_context_when_the_history_is_empty()
    {
        var history = new List<object>();

        var selected = new RecentTurnContextSelector(5).Select(history);

        Assert.Empty(selected);
        Assert.Empty(history);
        Assert.NotSame(history, selected);
    }

    [Fact]
    public void Select_includes_both_turns_when_two_turns_are_within_a_limit_of_five()
    {
        var history = new List<object>();
        AddPlainTurn(history, "turn-1");
        AddPlainTurn(history, "turn-2");
        var stored = history.ToArray();

        var selected = new RecentTurnContextSelector(5).Select(history);

        Assert.Equal(stored, selected.ToArray());
        AssertSameSequence(stored, history);
    }

    [Fact]
    public void Select_includes_only_the_latest_five_of_eight_turns_without_changing_history()
    {
        var history = new List<object>();
        AddPlainTurn(history, "turn-1");
        AddPlainTurn(history, "turn-2");
        AddToolTurn(history, "turn-3", "c-dropped", "sig-dropped");
        AddToolTurn(history, "turn-4", "c-kept", "sig-kept");
        AddPlainTurn(history, "turn-5");
        AddPlainTurn(history, "turn-6");
        AddPlainTurn(history, "turn-7");
        AddPlainTurn(history, "turn-8");
        var stored = history.ToArray();

        var selected = new RecentTurnContextSelector(5).Select(history);

        AssertSameSequence(stored, history);
        Assert.Equal(stored.Skip(8).ToArray(), selected.ToArray());

        var selectedJson = JsonSerializer.Serialize(selected);
        var storedJson = JsonSerializer.Serialize(history);
        Assert.DoesNotContain("turn-1", selectedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("turn-2", selectedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("turn-3", selectedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("c-dropped", selectedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("sig-dropped", selectedJson, StringComparison.Ordinal);
        Assert.Contains("c-kept", storedJson, StringComparison.Ordinal);
        Assert.Contains("c-dropped", storedJson, StringComparison.Ordinal);

        var callIndex = selectedJson.IndexOf("\"thoughtSignature\":\"sig-kept\"", StringComparison.Ordinal);
        var responseIndex = selectedJson.IndexOf("\"functionResponse\"", StringComparison.Ordinal);
        var nextTurnIndex = selectedJson.IndexOf("turn-5", StringComparison.Ordinal);
        Assert.True(callIndex >= 0 && callIndex < responseIndex && responseIndex < nextTurnIndex);
        AssertChronological(selectedJson, "turn-4", "turn-5", "turn-6", "turn-7", "turn-8");
    }

    [Fact]
    public void Select_returns_the_most_recent_complete_turns_in_chronological_order()
    {
        var first = User("one");
        var firstAnswer = Model("answer one");
        var second = User("two");
        var secondAnswer = Model("answer two");
        var third = User("three");
        var thirdAnswer = Model("answer three");
        var history = new List<object> { first, firstAnswer, second, secondAnswer, third, thirdAnswer };

        var selected = new RecentTurnContextSelector(2).Select(history);

        Assert.Equal(new[] { second, secondAnswer, third, thirdAnswer }, selected);
        Assert.Equal(6, history.Count);
        Assert.Same(first, history[0]);
    }

    [Fact]
    public void Select_keeps_a_tool_turn_together_with_its_format_follow_up()
    {
        var olderUser = User("older");
        var olderAnswer = Model("older answer");
        var prompt = User("Calculate the notebook total.");
        var call = FunctionCall("c1", "sig-abc");
        var response = FunctionResponse("c1");
        var draft = Model("draft answer");
        var format = User(RecentTurnContextSelector.SummaryFormatInstruction);
        var finalAnswer = Model("final summary");
        var history = new List<object>
        {
            olderUser,
            olderAnswer,
            prompt,
            call,
            response,
            draft,
            format,
            finalAnswer
        };

        var selected = new RecentTurnContextSelector(1).Select(history);

        Assert.Equal(new[] { prompt, call, response, draft, format, finalAnswer }, selected);
        Assert.Same(call, selected[1]);
        Assert.Same(response, selected[2]);
        Assert.Contains("\"id\":\"c1\"", JsonSerializer.Serialize(selected[1]), StringComparison.Ordinal);
        Assert.Contains("\"thoughtSignature\":\"sig-abc\"", JsonSerializer.Serialize(selected[1]), StringComparison.Ordinal);
        Assert.Contains("\"functionResponse\"", JsonSerializer.Serialize(selected[2]), StringComparison.Ordinal);
        Assert.Equal(8, history.Count);
        Assert.Same(olderUser, history[0]);
    }

    [Fact]
    public void Select_copies_the_window_and_leaves_the_source_unchanged()
    {
        var history = new List<object> { User("one"), Model("answer one") };

        var selected = new RecentTurnContextSelector(6).Select(history);
        var selectedCount = selected.Count;
        history.Add(User("two"));

        Assert.NotSame(history, selected);
        Assert.Equal(selectedCount, selected.Count);
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void Select_returns_every_message_when_the_history_is_shorter_than_the_limit()
    {
        var prompt = User("only");
        var call = FunctionCall("c9", "sig-keep");
        var response = FunctionResponse("c9");
        var answer = Model("done");
        var history = new List<object> { prompt, call, response, answer };

        var selected = new RecentTurnContextSelector(4).Select(history);

        Assert.Equal(history, selected);
        Assert.NotSame(history, selected);
    }

    [Fact]
    public void Select_rejects_a_null_history()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new RecentTurnContextSelector(1).Select(null!));

        Assert.Equal("messages", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_a_limit_below_one(int maxContextTurns)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new RecentTurnContextSelector(maxContextTurns));

        Assert.Equal("maxContextTurns", exception.ParamName);
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
