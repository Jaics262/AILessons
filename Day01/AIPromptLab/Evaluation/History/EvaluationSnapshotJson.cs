using System.Text;
using System.Text.Json;

// Saved form of one EvaluationSnapshot.
// The regression gate does not parse JSON. It receives the snapshot this reader builds.
// Property names are camelCase. Failure category names are the enum names, in any letter case.
// Omitted case ids, or a null caseIds array, mean the run did not record its cases.
public static class EvaluationSnapshotJson
{
    private const string RunIdProperty = "runId";
    private const string EvaluatedAtProperty = "evaluatedAt";
    private const string TotalCasesProperty = "totalCases";
    private const string FailedCasesProperty = "failedCases";
    private const string FailureRateProperty = "failureRate";
    private const string CategoryCountsProperty = "categoryCounts";
    private const string CaseIdsProperty = "caseIds";

    private static readonly HashSet<string> PropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        RunIdProperty,
        EvaluatedAtProperty,
        TotalCasesProperty,
        FailedCasesProperty,
        FailureRateProperty,
        CategoryCountsProperty,
        CaseIdsProperty
    };

    public static string Serialize(EvaluationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteSnapshot(writer, snapshot);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static EvaluationSnapshot Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("Evaluation snapshot JSON is empty.");
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("An evaluation snapshot must be a JSON object.");
        }

        return ReadSnapshot(document.RootElement);
    }

    private static void WriteSnapshot(Utf8JsonWriter writer, EvaluationSnapshot snapshot)
    {
        writer.WriteStartObject();
        writer.WriteString(RunIdProperty, snapshot.RunId);
        writer.WriteString(EvaluatedAtProperty, snapshot.EvaluatedAt);
        writer.WriteNumber(TotalCasesProperty, snapshot.TotalCases);
        writer.WriteNumber(FailedCasesProperty, snapshot.FailedCases);
        writer.WriteNumber(FailureRateProperty, snapshot.FailureRate);
        writer.WritePropertyName(CategoryCountsProperty);
        writer.WriteStartObject();
        foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
        {
            writer.WriteNumber(category.ToString(), snapshot.CategoryCounts[category]);
        }

        writer.WriteEndObject();
        if (snapshot.CaseIds is not null)
        {
            writer.WritePropertyName(CaseIdsProperty);
            writer.WriteStartArray();
            foreach (var caseId in snapshot.CaseIds)
            {
                writer.WriteStringValue(caseId);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static EvaluationSnapshot ReadSnapshot(JsonElement root)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
            {
                throw new JsonException(
                    $"The evaluation snapshot property '{property.Name}' is listed more than once.");
            }
        }

        foreach (var name in properties.Keys)
        {
            if (!PropertyNames.Contains(name))
            {
                throw new JsonException(
                    $"The evaluation snapshot property '{name}' is not recognized.");
            }
        }

        try
        {
            return new EvaluationSnapshot(
                ReadString(properties, RunIdProperty),
                ReadTimestamp(properties, EvaluatedAtProperty),
                ReadInt(properties, TotalCasesProperty),
                ReadInt(properties, FailedCasesProperty),
                ReadDouble(properties, FailureRateProperty),
                ReadCounts(Require(properties, CategoryCountsProperty)),
                ReadOptionalCaseIds(properties));
        }
        catch (ArgumentException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    private static IReadOnlyList<string>? ReadOptionalCaseIds(
        IReadOnlyDictionary<string, JsonElement> properties)
    {
        if (!properties.TryGetValue(CaseIdsProperty, out var element) ||
            element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Case ids must be a JSON array.");
        }

        var caseIds = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not string caseId)
            {
                throw new JsonException("A case id must be a string.");
            }

            caseIds.Add(caseId);
        }

        return caseIds;
    }

    private static Dictionary<EvaluationFailureCategory, int> ReadCounts(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Category counts must be a JSON object.");
        }

        var counts = new Dictionary<EvaluationFailureCategory, int>();
        foreach (var property in element.EnumerateObject())
        {
            if (!TryReadCategory(property.Name, out var category))
            {
                throw new JsonException($"'{property.Name}' is not a failure category.");
            }

            if (!property.Value.TryGetInt32(out var count))
            {
                throw new JsonException($"The count for '{property.Name}' must be an integer.");
            }

            if (!counts.TryAdd(category, count))
            {
                throw new JsonException(
                    $"The failure category '{category}' is listed more than once.");
            }
        }

        return counts;
    }

    private static bool TryReadCategory(string name, out EvaluationFailureCategory category)
    {
        category = default;
        if (name.Length == 0 || !char.IsLetter(name[0]))
        {
            return false;
        }

        return Enum.TryParse(name, ignoreCase: true, out category) && Enum.IsDefined(category);
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

    private static DateTimeOffset ReadTimestamp(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var element = Require(properties, name);
        if (element.ValueKind != JsonValueKind.String || !element.TryGetDateTimeOffset(out var timestamp))
        {
            throw new JsonException($"'{name}' must be a date and time with an offset.");
        }

        return timestamp;
    }

    private static int ReadInt(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var element = Require(properties, name);
        if (!element.TryGetInt32(out var value))
        {
            throw new JsonException($"'{name}' must be an integer.");
        }

        return value;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var element = Require(properties, name);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var value))
        {
            throw new JsonException($"'{name}' must be a number.");
        }

        return value;
    }

    private static JsonElement Require(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        if (!properties.TryGetValue(name, out var element))
        {
            throw new JsonException($"The evaluation snapshot is missing '{name}'.");
        }

        return element;
    }
}
