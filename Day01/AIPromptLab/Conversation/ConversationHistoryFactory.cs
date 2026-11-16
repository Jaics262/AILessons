public static class ConversationHistoryFactory
{
    public const string InMemoryProvider = "InMemory";

    public static IConversationHistory Create(string? provider)
    {
        if (string.Equals(provider, InMemoryProvider, StringComparison.OrdinalIgnoreCase))
        {
            return new InMemoryConversationHistory();
        }

        throw new InvalidOperationException(
            $"ConversationHistory '{provider}' is not supported. Set ConversationHistory to InMemory in appsettings.json.");
    }
}
