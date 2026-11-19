using System.Text;
using System.Text.Json;

// Saved form of one EvaluationQualityReport.
// Property names are camelCase and fixed. Every property is written.
// A missing count or rate is JSON null. The file has no timestamp.
// Indentation uses \n, so the same report has the same bytes on every machine.
// Write creates the report directory when it does not already exist.
// Deserialize reads that saved report. It does not run the regression gate or apply its policy.
public static class EvaluationQualityReportJson
{
    public const string StatusProperty = "status";

    public const string BaselineCaseCountProperty = "baselineCaseCount";

    public const string CandidateCaseCountProperty = "candidateCaseCount";

    public const string BaselineFailureRateProperty = "baselineFailureRate";

    public const string CandidateFailureRateProperty = "candidateFailureRate";

    public const string FailureRateChangePercentagePointsProperty = "failureRateChangePercentagePoints";

    public const string FailureReasonsProperty = "failureReasons";

    public const string ExitCodeProperty = "exitCode";

    private static readonly HashSet<string> PropertyNames = new(StringComparer.Ordinal)
    {
        StatusProperty,
        BaselineCaseCountProperty,
        CandidateCaseCountProperty,
        BaselineFailureRateProperty,
        CandidateFailureRateProperty,
        FailureRateChangePercentagePointsProperty,
        FailureReasonsProperty,
        ExitCodeProperty
    };

    public static string Serialize(EvaluationQualityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = true,
                NewLine = "\n"
            }))
        {
            writer.WriteStartObject();
            writer.WriteString(StatusProperty, report.Status);
            WriteInt32(writer, BaselineCaseCountProperty, report.BaselineCaseCount);
            WriteInt32(writer, CandidateCaseCountProperty, report.CandidateCaseCount);
            WriteDouble(writer, BaselineFailureRateProperty, report.BaselineFailureRate);
            WriteDouble(writer, CandidateFailureRateProperty, report.CandidateFailureRate);
            WriteDouble(writer, FailureRateChangePercentagePointsProperty, report.FailureRateChangePercentagePoints);
            writer.WritePropertyName(FailureReasonsProperty);
            writer.WriteStartArray();
            foreach (var reason in report.FailureReasons)
            {
                writer.WriteStringValue(reason);
            }

            writer.WriteEndArray();
            writer.WriteNumber(ExitCodeProperty, report.ExitCode);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void Write(string path, EvaluationQualityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = Serialize(report);

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new IOException(
                $"The quality report '{path}' could not be written. {exception.Message}",
                exception);
        }
    }

    public static EvaluationQualityReport Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("Quality report JSON is empty.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new JsonException(exception.Message, exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("A quality report must be a JSON object.");
            }

            return ReadReport(document.RootElement);
        }
    }

    private static EvaluationQualityReport ReadReport(JsonElement root)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
            {
                throw new JsonException(
                    $"The quality report property '{property.Name}' is listed more than once.");
            }
        }

        foreach (var name in properties.Keys)
        {
            if (!PropertyNames.Contains(name))
            {
                throw new JsonException(
                    $"The quality report property '{name}' is not recognized.");
            }
        }

        try
        {
            return EvaluationQualityReport.FromStored(
                ReadString(properties, StatusProperty),
                ReadNullableInt32(properties, BaselineCaseCountProperty),
                ReadNullableInt32(properties, CandidateCaseCountProperty),
                ReadNullableDouble(properties, BaselineFailureRateProperty),
                ReadNullableDouble(properties, CandidateFailureRateProperty),
                ReadNullableDouble(properties, FailureRateChangePercentagePointsProperty),
                ReadReasons(properties),
                ReadInt32(properties, ExitCodeProperty));
        }
        catch (ArgumentException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    private static IReadOnlyList<string> ReadReasons(IReadOnlyDictionary<string, JsonElement> properties)
    {
        var element = Require(properties, FailureReasonsProperty);
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"'{FailureReasonsProperty}' must be an array of strings.");
        }

        var reasons = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not string reason)
            {
                throw new JsonException("A failure reason must be a string.");
            }

            reasons.Add(reason);
        }

        return reasons;
    }

    private static string ReadString(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var element = Require(properties, name);
        if (element.ValueKind != JsonValueKind.String || element.GetString() is not string value)
        {
            throw new JsonException($"'{name}' must be a string.");
        }

        return value;
    }

    private static int ReadInt32(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var element = Require(properties, name);
        if (!element.TryGetInt32(out var value))
        {
            throw new JsonException($"'{name}' must be an integer.");
        }

        return value;
    }

    private static int? ReadNullableInt32(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var element = Require(properties, name);
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!element.TryGetInt32(out var value))
        {
            throw new JsonException($"'{name}' must be an integer or null.");
        }

        return value;
    }

    private static double? ReadNullableDouble(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var element = Require(properties, name);
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var value))
        {
            throw new JsonException($"'{name}' must be a number or null.");
        }

        return value;
    }

    private static JsonElement Require(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        if (!properties.TryGetValue(name, out var element))
        {
            throw new JsonException($"The quality report is missing '{name}'.");
        }

        return element;
    }

    private static void WriteInt32(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is int number)
        {
            writer.WriteNumber(name, number);
            return;
        }

        writer.WriteNull(name);
    }

    private static void WriteDouble(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is double number)
        {
            writer.WriteNumber(name, number);
            return;
        }

        writer.WriteNull(name);
    }
}
