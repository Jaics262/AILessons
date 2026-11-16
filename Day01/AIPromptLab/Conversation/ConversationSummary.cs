public sealed class ConversationSummary
{
    public const string Heading = "Conversation summary";
    public const string DecisionsSection = "Decisions";
    public const string ConstraintsSection = "Constraints";
    public const string CompletedSection = "Completed actions";
    public const string UnresolvedSection = "Unresolved tasks";
    public const string DecisionLabel = "Decision";
    public const string ConstraintLabel = "Constraint";
    public const string CompletedLabel = "Completed";
    public const string UnresolvedLabel = "Unresolved";

    public ConversationSummary(
        IReadOnlyList<string> decisions,
        IReadOnlyList<string> constraints,
        IReadOnlyList<string> completedActions,
        IReadOnlyList<string> unresolvedTasks)
    {
        Decisions = CopyFacts(decisions, nameof(decisions), "Decisions cannot contain null or blank values.");
        Constraints = CopyFacts(constraints, nameof(constraints), "Constraints cannot contain null or blank values.");
        CompletedActions = CopyFacts(
            completedActions,
            nameof(completedActions),
            "Completed actions cannot contain null or blank values.");
        UnresolvedTasks = CopyFacts(
            unresolvedTasks,
            nameof(unresolvedTasks),
            "Unresolved tasks cannot contain null or blank values.");
    }

    public List<string> Decisions { get; }

    public List<string> Constraints { get; }

    public List<string> CompletedActions { get; }

    public List<string> UnresolvedTasks { get; }

    public bool HasFacts =>
        Decisions.Count > 0 ||
        Constraints.Count > 0 ||
        CompletedActions.Count > 0 ||
        UnresolvedTasks.Count > 0;

    public string Format()
    {
        if (!HasFacts)
        {
            return string.Empty;
        }

        var lines = new List<string> { Heading };
        AppendSection(lines, DecisionsSection, Decisions);
        AppendSection(lines, ConstraintsSection, Constraints);
        AppendSection(lines, CompletedSection, CompletedActions);
        AppendSection(lines, UnresolvedSection, UnresolvedTasks);
        return string.Join('\n', lines);
    }

    private static List<string> CopyFacts(IReadOnlyList<string> facts, string paramName, string blankMessage)
    {
        ArgumentNullException.ThrowIfNull(facts, paramName);

        if (facts.Any(fact => string.IsNullOrWhiteSpace(fact)))
        {
            throw new ArgumentException(blankMessage, paramName);
        }

        return facts.ToList();
    }

    private static void AppendSection(List<string> lines, string section, List<string> facts)
    {
        if (facts.Count == 0)
        {
            return;
        }

        lines.Add(section + ":");
        foreach (var fact in facts)
        {
            lines.Add("- " + fact);
        }
    }
}
