using System.Globalization;
using Microsoft.Extensions.Configuration;

public sealed class PromptLabSettings
{
    public const int DefaultMaxContextTurns = 6;

    // Heuristic. Actual token use varies with the content and the model.
    public const int DefaultMaxHistoryTokens = 4000;

    public const int DefaultMaxFunctionRounds = 4;
    public const string DefaultModel = "gemini-3.8-flash";
    public const int DefaultRequestTimeoutMinutes = 5;

    public PromptLabSettings(
        int maxContextTurns,
        int maxHistoryTokens,
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
        MaxFunctionRounds = maxFunctionRounds;
        Model = model;
        RequestTimeout = requestTimeout;
    }

    public int MaxContextTurns { get; }

    public int MaxHistoryTokens { get; }

    public int MaxFunctionRounds { get; }

    public string Model { get; }

    public TimeSpan RequestTimeout { get; }

    public static PromptLabSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new PromptLabSettings(
            ReadPositiveInt(configuration, "MaxContextTurns", DefaultMaxContextTurns),
            ReadPositiveInt(configuration, "MaxHistoryTokens", DefaultMaxHistoryTokens),
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
