using System.Globalization;
using Microsoft.Extensions.Configuration;

public sealed class PromptLabSettings
{
    public const int DefaultMaxContextTurns = 6;

    // Heuristic. Actual token use varies with the content and the model.
    public const int DefaultMaxHistoryTokens = 4000;

    // Heuristic budget for the compact summary of turns outside the recent window.
    public const int DefaultMaxSummaryTokens = 200;

    // Heuristic budget for retrieved memories added to a request.
    public const int DefaultMaxMemoryTokens = 200;

    public const int DefaultMaxFunctionRounds = 4;
    public const string DefaultModel = "gemini-3.8-flash";
    public const int DefaultRequestTimeoutMinutes = 5;

    public PromptLabSettings(
        int maxContextTurns,
        int maxHistoryTokens,
        int maxSummaryTokens,
        int maxMemoryTokens,
        int maxFunctionRounds,
        string model,
        TimeSpan requestTimeout)
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

        if (maxSummaryTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSummaryTokens),
                "The maximum summary token budget must be greater than zero.");
        }

        if (maxMemoryTokens < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMemoryTokens),
                "The maximum memory token budget must be greater than zero.");
        }

        if (maxFunctionRounds < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFunctionRounds),
                "The maximum number of function rounds must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("The model name cannot be empty.", nameof(model));
        }

        if (requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                "The request timeout must be greater than zero.");
        }

        MaxContextTurns = maxContextTurns;
        MaxHistoryTokens = maxHistoryTokens;
        MaxSummaryTokens = maxSummaryTokens;
        MaxMemoryTokens = maxMemoryTokens;
        MaxFunctionRounds = maxFunctionRounds;
        Model = model;
        RequestTimeout = requestTimeout;
    }

    public int MaxContextTurns { get; }

    public int MaxHistoryTokens { get; }

    public int MaxSummaryTokens { get; }

    public int MaxMemoryTokens { get; }

    public int MaxFunctionRounds { get; }

    public string Model { get; }

    public TimeSpan RequestTimeout { get; }

    public static PromptLabSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new PromptLabSettings(
            ReadPositiveInt(configuration, "MaxContextTurns", DefaultMaxContextTurns),
            ReadPositiveInt(configuration, "MaxHistoryTokens", DefaultMaxHistoryTokens),
            ReadPositiveInt(configuration, "MaxSummaryTokens", DefaultMaxSummaryTokens),
            ReadPositiveInt(configuration, "MaxMemoryTokens", DefaultMaxMemoryTokens),
            ReadPositiveInt(configuration, "MaxFunctionRounds", DefaultMaxFunctionRounds),
            ReadText(configuration, "Model", DefaultModel),
            TimeSpan.FromMinutes(ReadPositiveInt(configuration, "RequestTimeoutMinutes", DefaultRequestTimeoutMinutes)));
    }

    private static int ReadPositiveInt(IConfiguration configuration, string name, int fallback)
    {
        var value = configuration[name];
        if (value is null)
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            throw new InvalidOperationException($"{name} must be a whole number greater than zero.");
        }

        return number;
    }

    private static string ReadText(IConfiguration configuration, string name, string fallback)
    {
        var value = configuration[name];
        if (value is null)
        {
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} must be a non-blank string.");
        }

        return value;
    }
}
