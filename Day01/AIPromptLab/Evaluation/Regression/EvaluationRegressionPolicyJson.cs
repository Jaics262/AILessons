using System.Text.Json;

// Saved form of one EvaluationRegressionPolicy.
// The regression gate does not parse JSON. The runner builds the policy and passes it to the gate.
// Property names are camelCase. Every listed field is required. An omitted field is not filled
// from the default policy, because a missing limit is a different file, not a quieter one.
// A loaded policy still fails when the baseline or candidate snapshot is missing.
// That choice is not a field in this file. It stays the default.
// Limits must be non-negative. Zero means that increase is not allowed.
public static class EvaluationRegressionPolicyJson
{
    public const string MaximumFailureRateRegressionProperty = "maxFailureRateRegressionPercentagePoints";

    public const string MaximumUngroundedClaimIncreaseProperty = "maxAdditionalUngroundedClaimFailures";

    public const string MaximumMissingExpectedFactIncreaseProperty = "maxAdditionalMissingExpectedFactFailures";

    private static readonly HashSet<string> PropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        MaximumFailureRateRegressionProperty,
        MaximumUngroundedClaimIncreaseProperty,
        MaximumMissingExpectedFactIncreaseProperty
    };

    public static EvaluationRegressionPolicy Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("Quality gate policy JSON is empty.");
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A quality gate policy must be a JSON object.");
        }

        return ReadPolicy(document.RootElement);
    }

    private static EvaluationRegressionPolicy ReadPolicy(JsonElement root)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
            {
                throw new JsonException(
                    $"The quality gate policy property '{property.Name}' is listed more than once.");
            }
        }

        foreach (var name in properties.Keys)
        {
            if (!PropertyNames.Contains(name))
            {
                throw new JsonException(
                    $"The quality gate policy property '{name}' is not recognized.");
            }
        }

        var maximumFailureRateRegression = ReadNonNegativeNumber(
            properties,
            MaximumFailureRateRegressionProperty);
        var maximumUngroundedClaimIncrease = ReadNonNegativeInt(
            properties,
            MaximumUngroundedClaimIncreaseProperty);
        var maximumMissingExpectedFactIncrease = ReadNonNegativeInt(
            properties,
            MaximumMissingExpectedFactIncreaseProperty);

        try
        {
            return new EvaluationRegressionPolicy(
                maximumFailureRateRegression,
                maximumUngroundedClaimIncrease,
                maximumMissingExpectedFactIncrease);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    private static double ReadNonNegativeNumber(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name)
    {
        var element = Require(properties, name);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var value))
        {
            throw new JsonException($"'{name}' must be a number.");
        }

        if (!double.IsFinite(value) || value < 0d)
        {
            throw new JsonException($"'{name}' must be a non-negative number.");
        }

        return value;
    }

    // Bytes of the policy limits the gate applied when no policy file was read.
    // Property order and names match the policy file. The same policy has the same bytes.
    public static byte[] CanonicalBytes(EvaluationRegressionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber(MaximumFailureRateRegressionProperty, policy.MaximumFailureRateRegression);
            writer.WriteNumber(MaximumUngroundedClaimIncreaseProperty, policy.MaximumUngroundedClaimIncrease);
            writer.WriteNumber(
                MaximumMissingExpectedFactIncreaseProperty,
                policy.MaximumMissingExpectedFactIncrease);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static int ReadNonNegativeInt(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name)
    {
        var element = Require(properties, name);
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException($"'{name}' must be an integer.");
        }

        if (!element.TryGetInt32(out var value))
        {
            if (element.TryGetDouble(out var number) && number < 0d)
            {
                throw new JsonException($"'{name}' must be a non-negative integer.");
            }

            throw new JsonException($"'{name}' must be an integer.");
        }

        if (value < 0)
        {
            throw new JsonException($"'{name}' must be a non-negative integer.");
        }

        return value;
    }

    private static JsonElement Require(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        if (!properties.TryGetValue(name, out var element))
        {
            throw new JsonException($"The quality gate policy is missing '{name}'.");
        }

        return element;
    }
}
