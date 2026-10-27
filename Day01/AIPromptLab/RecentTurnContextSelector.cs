using System.Text.Json;

public sealed class RecentTurnContextSelector
{
    public const string SummaryFormatInstruction = "Format that answer as the learning summary JSON.";

    public RecentTurnContextSelector(int maxContextTurns, int maxHistoryTokens = PromptLabSettings.DefaultMaxHistoryTokens)
    {
        if (maxContextTurns < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxContextTurns),
                "The maximum number of context turns must be greater than zero.");
        }

        if (maxHistoryTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxHistoryTokens),
                "The maximum history token budget must be greater than zero.");
        }

        MaxContextTurns = maxContextTurns;
        MaxHistoryTokens = maxHistoryTokens;
    }

    public int MaxContextTurns { get; }

    public int MaxHistoryTokens { get; }

    public IReadOnlyList<object> Select(IReadOnlyList<object> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var turnStarts = new List<int>();
        for (var index = 0; index < messages.Count; index++)
        {
            if (StartsUserTurn(messages, index))
            {
                turnStarts.Add(index);
            }
        }

        if (turnStarts.Count == 0)
        {
            return [];
        }

        var selectedTurns = 0;
        var selectedTokens = 0;
        var keptStart = messages.Count;
        for (var turn = turnStarts.Count - 1; turn >= 0 && selectedTurns < MaxContextTurns; turn--)
        {
            var start = turnStarts[turn];
            var end = turn + 1 < turnStarts.Count ? turnStarts[turn + 1] : messages.Count;
            var turnTokens = 0;
            for (var index = start; index < end; index++)
            {
                turnTokens += EstimateTokens(messages[index]);
            }

            if (selectedTurns > 0 && selectedTokens + turnTokens > MaxHistoryTokens)
            {
                break;
            }

            selectedTokens += turnTokens;
            selectedTurns++;
            keptStart = start;
        }

        return messages.Skip(keptStart).ToList();
    }

    // About four characters per token. This is not the model's tokenizer.
    public static int EstimateTokens(object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var length = JsonSerializer.Serialize(message).Length;
        return Math.Max(1, (length + 3) / 4);
    }

    private static bool StartsUserTurn(IReadOnlyList<object> messages, int index)
    {
        var json = JsonSerializer.SerializeToElement(messages[index]);
        if (!IsRole(json, "user") ||
            !json.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var hasPrompt = false;
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("functionResponse", out var functionResponse) &&
                functionResponse.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                return false;
            }

            if (!part.TryGetProperty("text", out var textElement) ||
                textElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = textElement.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (text == SummaryFormatInstruction &&
                index > 0 &&
                IsRole(JsonSerializer.SerializeToElement(messages[index - 1]), "model"))
            {
                continue;
            }

            hasPrompt = true;
        }

        return hasPrompt;
    }

    private static bool IsRole(JsonElement message, string role)
    {
        return message.TryGetProperty("role", out var roleElement) &&
               roleElement.ValueKind == JsonValueKind.String &&
               roleElement.GetString() == role;
    }
}
