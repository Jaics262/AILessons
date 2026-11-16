using System.Text.Json;

public sealed class DeterministicConversationSummarizer : IConversationSummarizer
{
    public ConversationSummary Summarize(IReadOnlyList<object> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var decisions = new List<string>();
        var constraints = new List<string>();
        var completedActions = new List<string>();
        var unresolvedTasks = new List<string>();
        foreach (var message in messages)
        {
            foreach (var line in ReadLines(message))
            {
                if (TryReadFact(line, ConversationSummary.DecisionLabel, out var decision))
                {
                    decisions.Add(decision);
                }
                else if (TryReadFact(line, ConversationSummary.ConstraintLabel, out var constraint))
                {
                    constraints.Add(constraint);
                }
                else if (TryReadFact(line, ConversationSummary.CompletedLabel, out var completed))
                {
                    completedActions.Add(completed);
                }
                else if (TryReadFact(line, ConversationSummary.UnresolvedLabel, out var unresolved))
                {
                    unresolvedTasks.Add(unresolved);
                }
            }
        }

        return new ConversationSummary(decisions, constraints, completedActions, unresolvedTasks);
    }

    private static IEnumerable<string> ReadLines(object message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var json = JsonSerializer.SerializeToElement(message);
        if (!json.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var part in parts.EnumerateArray())
        {
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

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }
    }

    private static bool TryReadFact(string line, string label, out string value)
    {
        var prefix = label + ":";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = line[prefix.Length..].Trim();
        return value.Length > 0;
    }
}
