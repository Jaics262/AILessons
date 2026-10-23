using System.Text.Json;

public sealed class RecentTurnContextSelector
{
    public const string SummaryFormatInstruction = "Format that answer as the learning summary JSON.";

    public RecentTurnContextSelector(int maxContextTurns)
    {
        if (maxContextTurns < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxContextTurns),
                "The maximum number of context turns must be greater than zero.");
        }

        MaxContextTurns = maxContextTurns;
    }

    public int MaxContextTurns { get; }

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

        if (turnStarts.Count <= MaxContextTurns)
        {
            return messages.ToList();
        }

        var firstKept = turnStarts[turnStarts.Count - MaxContextTurns];
        return messages.Skip(firstKept).ToList();
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
