using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class LearningSummary
{
    public const string Beginner = "Beginner";
    public const string Intermediate = "Intermediate";
    public const string Advanced = "Advanced";

    public static readonly IReadOnlyList<string> AllowedDifficulties = [Beginner, Intermediate, Advanced];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    [JsonConstructor]
    public LearningSummary(string title, string summary, List<string> keyPoints, string difficulty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(keyPoints);

        if (!IsAllowedDifficulty(difficulty))
        {
            throw new ArgumentException(
                $"Difficulty must be one of: {string.Join(", ", AllowedDifficulties)}.",
                nameof(difficulty));
        }

        if (keyPoints.Exists(point => string.IsNullOrWhiteSpace(point)))
        {
            throw new ArgumentException("Key points cannot contain null or blank values.", nameof(keyPoints));
        }

        Title = title;
        Summary = summary;
        KeyPoints = keyPoints;
        Difficulty = difficulty;
    }

    public string Title { get; }

    public string Summary { get; }

    public List<string> KeyPoints { get; }

    public string Difficulty { get; }

    public string Serialize() => JsonSerializer.Serialize(this, SerializerOptions);

    public static LearningSummary Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize<LearningSummary>(json, SerializerOptions)
            ?? throw new JsonException("Learning summary JSON deserialized to null.");
    }

    public static LearningSummary? DeserializeOrDefault(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return Deserialize(json);
    }

    private static bool IsAllowedDifficulty(string? difficulty)
    {
        return difficulty is not null && AllowedDifficulties.Contains(difficulty, StringComparer.Ordinal);
    }
}
