using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class PromptLabServices
{
    public const string ApiKeyName = "GEMINI_API_KEY";

    public static IServiceCollection AddPromptLab(this IServiceCollection services)
    {
        services.AddSingleton(static provider => PromptLabSettings.Read(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<IConversationHistory>(static provider =>
        {
            var historyProvider = provider.GetRequiredService<IConfiguration>()["ConversationHistory"];
            if (string.IsNullOrWhiteSpace(historyProvider))
            {
                throw new InvalidOperationException(
                    "ConversationHistory is missing. Set it in appsettings.json, user secrets, or the environment.");
            }

            return ConversationHistoryFactory.Create(historyProvider);
        });
        services.AddSingleton<IConversationSummarizer, DeterministicConversationSummarizer>();
        services.AddSingleton<HttpRetryPolicy>();
        services.AddSingleton(static provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            return new GeminiLlmClient(
                provider.GetRequiredService<IConversationHistory>(),
                provider.GetRequiredService<PromptLabSettings>(),
                configuration[ApiKeyName],
                provider.GetRequiredService<HttpRetryPolicy>(),
                provider.GetRequiredService<IConversationSummarizer>());
        });
        services.AddHostedService<PromptLabWorker>();
        return services;
    }
}
