using System.Globalization;
using System.Text;

// Markdown form of one saved EvaluationQualityReport.
// The text copies the JSON fields. It does not run the regression gate or apply its policy.
// A missing count or rate is "not recorded". 0 is a real rate, not a stand-in for a missing value.
// There is no timestamp. The same report produces the same markdown.
// Write creates the summary directory when it does not already exist.
public static class EvaluationQualitySummary
{
    public const string NotRecorded = "not recorded";

    public static string Render(EvaluationQualityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var lines = new List<string>
        {
            "# Quality summary",
            "",
            $"Gate status: {report.Status}",
            "",
            "| | Baseline | Candidate |",
            "| --- | --- | --- |",
            $"| Cases | {FormatCount(report.BaselineCaseCount)} | {FormatCount(report.CandidateCaseCount)} |",
            $"| Failure rate | {FormatRate(report.BaselineFailureRate)} | {FormatRate(report.CandidateFailureRate)} |",
            "",
            $"Failure rate change: {FormatChange(report.FailureRateChangePercentagePoints)}",
            "",
            $"Exit code: {report.ExitCode.ToString(CultureInfo.InvariantCulture)}",
            "",
            "## Failure reasons",
            ""
        };

        if (report.FailureReasons.Count == 0)
        {
            lines.Add("None.");
        }
        else
        {
            foreach (var reason in report.FailureReasons)
            {
                lines.Add($"- {reason}");
            }
        }

        return string.Join('\n', lines) + "\n";
    }

    public static void Write(string path, EvaluationQualityReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var markdown = Render(report);

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            throw new IOException(
                $"The quality summary '{path}' could not be written. {exception.Message}",
                exception);
        }
    }

    private static string FormatCount(int? count) =>
        count is int value
            ? value.ToString(CultureInfo.InvariantCulture)
            : NotRecorded;

    private static string FormatRate(double? rate) =>
        rate is double value
            ? $"{FormatNumber(value)}%"
            : NotRecorded;

    private static string FormatChange(double? change) =>
        change is double value
            ? $"{FormatNumber(value)} percentage points"
            : NotRecorded;

    private static string FormatNumber(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
