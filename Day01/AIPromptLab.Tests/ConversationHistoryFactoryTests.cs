namespace AIPromptLab.Tests;

public sealed class ConversationHistoryFactoryTests
{
    [Theory]
    [InlineData("InMemory")]
    [InlineData("inmemory")]
    public void Create_returns_the_in_memory_history_for_the_configured_provider(string provider)
    {
        var history = ConversationHistoryFactory.Create(provider);

        Assert.IsType<InMemoryConversationHistory>(history);
        Assert.Empty(history.Messages);
        Assert.Null(history.Summary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Database")]
    public void Create_rejects_an_unknown_provider(string? provider)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ConversationHistoryFactory.Create(provider));

        Assert.Contains("InMemory", exception.Message, StringComparison.Ordinal);
    }
}
