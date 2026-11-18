using System.Text;
using System.Text.Json;

// Saved form of one EvaluationQualityReport.
// Property names are camelCase and fixed. Every property is written.
// A missing count or rate is JSON null. The file has no timestamp.
// Indentation uses \n, so the same report has the same bytes on every machine.
// Write creates the report directory when it does not already exist.
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
