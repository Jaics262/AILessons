// Fixed queries and the memory texts they are scored against.
public static class RetrievalEvaluationSet
{
    public const string RetryAfter = "retry-after";
    public const string DotNet = "dotnet";
    public const string SessionHistory = "session-history";
    public const string ConversationSummary = "conversation-summary";
    public const string VectorSimilarity = "vector-similarity";
    public const string LocalTools = "local-tools";
    public const string LearningSummary = "learning-summary";
    public const string VerbatimTurns = "verbatim-turns";
    public const string FunctionRounds = "function-rounds";
    public const string HistoryOnFailure = "history-on-failure";

    public static IReadOnlyList<RetrievalTestCase> Cases { get; } =
    [
        new("How should HTTP retries work?", [RetryAfter]),
        new("Which framework does the project use?", [DotNet]),
        new("How is conversation history managed?", [SessionHistory]),
        new("How are older conversations compressed?", [ConversationSummary]),
        new("How are similar memories found?", [VectorSimilarity]),
        new("Which tools can the model call?", [LocalTools]),
        new("What must a learning summary contain?", [LearningSummary]),
        new("Do recent turns remain verbatim?", [VerbatimTurns]),
        new("How many tool rounds can one prompt use?", [FunctionRounds]),
        new("What happens to stored history when a request fails?", [HistoryOnFailure])
    ];

    public static IReadOnlyList<MemoryEntry> Memories { get; } = CreateMemories();

    private static MemoryEntry[] CreateMemories()
    {
        var createdAt = DateTimeOffset.Parse("2026-10-30T09:00:00Z");
        return
        [
            new(RetryAfter, "HTTP retries should respect Retry-After.", createdAt),
            new(DotNet, "The project uses .NET 10 and xUnit.", createdAt),
            new(SessionHistory, "The in-memory conversation history appends the prompt and model reply after a successful turn.", createdAt),
            new(ConversationSummary, "Turns outside the recent window are sent as decisions, constraints, completed actions, and unresolved tasks.", createdAt),
            new(VectorSimilarity, "EmbeddingMemoryRetriever orders memories by the cosine of their stored vectors.", createdAt),
            new(LocalTools, "The generateContent request can include calculate_total and calculate_vat.", createdAt),
            new(LearningSummary, "A valid model answer has a title, a summary, exactly three key points, and a difficulty.", createdAt),
            new(VerbatimTurns, "Recent conversation turns must remain verbatim.", createdAt),
            new(FunctionRounds, "At most four tool rounds run before the client asks for the learning-summary schema.", createdAt),
            new(HistoryOnFailure, "A GeminiClientException leaves the stored transcript and summary as they were.", createdAt)
        ];
    }
}
