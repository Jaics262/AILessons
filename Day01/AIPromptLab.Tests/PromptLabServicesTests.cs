using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AIPromptLab.Tests;

public sealed class PromptLabServicesTests
{
    [Fact]
    public void AddPromptLab_resolves_one_client_from_configuration()
    {
        using var provider = BuildProvider(
            ("ConversationHistory", "InMemory"),
            ("MaxContextTurns", "5"),
            ("MaxHistoryTokens", "1200"),
            ("MaxSummaryTokens", "80"),
            ("MaxFunctionRounds", "2"),
            ("Model", "gemini-test"),
            ("RequestTimeoutMinutes", "3"),
            (PromptLabServices.ApiKeyName, "unit-test-api-key"));

        var settings = provider.GetRequiredService<PromptLabSettings>();
        var history = provider.GetRequiredService<IConversationHistory>();
        var summarizer = provider.GetRequiredService<IConversationSummarizer>();
        var client = provider.GetRequiredService<GeminiLlmClient>();

        Assert.Equal(5, settings.MaxContextTurns);
        Assert.Equal(1200, settings.MaxHistoryTokens);
        Assert.Equal(80, settings.MaxSummaryTokens);
        Assert.Equal(2, settings.MaxFunctionRounds);
        Assert.Equal("gemini-test", settings.Model);
        Assert.IsType<InMemoryConversationHistory>(history);
        Assert.IsType<DeterministicConversationSummarizer>(summarizer);
        Assert.Same(client, provider.GetRequiredService<GeminiLlmClient>());
        Assert.Same(history, provider.GetRequiredService<IConversationHistory>());
        Assert.Same(summarizer, provider.GetRequiredService<IConversationSummarizer>());
    }

    [Fact]
    public void AddPromptLab_requires_an_api_key()
    {
        using var provider = BuildProvider(("ConversationHistory", "InMemory"));

        var exception = Assert.Throws<GeminiClientException>(() => provider.GetRequiredService<GeminiLlmClient>());

        Assert.Equal(GeminiClientError.MissingApiKey, exception.Error);
    }

    [Fact]
    public void AddPromptLab_requires_a_conversation_history_provider()
    {
        using var provider = BuildProvider((PromptLabServices.ApiKeyName, "unit-test-api-key"));

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<GeminiLlmClient>());

        Assert.Contains("ConversationHistory", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildProvider(params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => pair.Value))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddPromptLab();
        return services.BuildServiceProvider();
    }
}
