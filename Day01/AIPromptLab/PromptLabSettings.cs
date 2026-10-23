using System.Text.Json;

public sealed class PromptLabSettings
{
    public const int DefaultMaxContextTurns = 6;
    public const int DefaultMaxFunctionRounds = 4;
    public const string DefaultModel = "gemini-3.8-flash";
    public const int DefaultRequestTimeoutMinutes = 5;

    public PromptLabSettings(int maxContextTurns, int maxFunctionRounds, string model, TimeSpan requestTimeout)
    {
        if (maxContextTurns < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxContextTurns),
                "The maximum number of context turns must be greater than zero.");
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
        MaxFunctionRounds = maxFunctionRounds;
        Model = model;
        RequestTimeout = requestTimeout;
    }

    public int MaxContextTurns { get; }

    public int MaxFunctionRounds { get; }

    public string Model { get; }

    public TimeSpan RequestTimeout { get; }

    public static PromptLabSettings Read(JsonElement settings)
    {
        var timeoutMinutes = ReadPositiveInt(settings, "RequestTimeoutMinutes", DefaultRequestTimeoutMinutes);
        return new PromptLabSettings(
            ReadPositiveInt(settings, "MaxContextTurns", DefaultMaxContextTurns),
            ReadPositiveInt(settings, "MaxFunctionRounds", DefaultMaxFunctionRounds),
            ReadText(settings, "Model", DefaultModel),
            TimeSpan.FromMinutes(timeoutMinutes));
    }

    private static int ReadPositiveInt(JsonElement settings, string name, int fallback)
    {
        if (!settings.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number) ||
            number < 1)
        {
            throw new InvalidOperationException($"{name} must be a whole number greater than zero.");
        }

        return number;
    }

    private static string ReadText(JsonElement settings, string name, string fallback)
    {
        if (!settings.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"{name} must be a non-blank string.");
        }

        return value.GetString()!;
    }
}
