using System.Text.Json;

// Reads a saved quality report and writes the markdown summary.
// The JSON file is the source of truth. This command does not run the regression gate
// and does not apply its policy. A stored Failed status still exits 0 once the summary is written.
// A missing or malformed report is named on the error output and exits 2. No summary file is written.
// The summary has no timestamp. The same JSON produces the same markdown.
// A relative path is resolved from the project directory, the folder that contains
// AIPromptLab.csproj. An absolute path is used as given.
// When the path was relative, an error names that relative path.
public static class EvaluationQualitySummaryRunner
{
    public const string CommandName = "quality-summary";

    public const int WrittenExitCode = 0;

    public const int InvalidReportExitCode = 2;

    public const string DefaultReportPath = "reports/quality.json";

    public const string DefaultSummaryPath = "reports/quality-summary.md";

    public const string Usage =
        $"Usage: {CommandName} [--report <quality.json>] [--output <quality-summary.md>]";

    private const string ReportOption = "--report";

    private const string OutputOption = "--output";

    public static bool IsInvocation(IReadOnlyList<string>? args) =>
        args is { Count: > 0 } &&
        string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase);

    public static int Run(IReadOnlyList<string>? args, TextWriter? output = null, TextWriter? error = null)
    {
        error ??= Console.Error;
        if (!TryParse(args, out var reportPath, out var summaryPath, out var parseError))
        {
            error.WriteLine(parseError);
            return InvalidReportExitCode;
        }

        return Run(reportPath, summaryPath, output, error);
    }

    public static int Run(
        string? reportPath,
        string? summaryPath,
        TextWriter? output = null,
        TextWriter? error = null,
        string? baseDirectory = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;
        if (reportPath is not null && string.IsNullOrWhiteSpace(reportPath))
        {
            error.WriteLine($"The {ReportOption} file was not provided. {Usage}");
            return InvalidReportExitCode;
        }

        if (summaryPath is not null && string.IsNullOrWhiteSpace(summaryPath))
        {
            error.WriteLine($"The {OutputOption} file was not provided. {Usage}");
            return InvalidReportExitCode;
        }

        var reportDisplayPath = reportPath ?? DefaultReportPath;
        var summaryDisplayPath = summaryPath ?? DefaultSummaryPath;
        var resolvedReport = ResolvePath(reportDisplayPath, baseDirectory);
        var resolvedSummary = ResolvePath(summaryDisplayPath, baseDirectory);
        if (!TryRead(resolvedReport, reportDisplayPath, out var report, out var readError) || report is null)
        {
            error.WriteLine(readError);
            return InvalidReportExitCode;
        }

        var markdown = EvaluationQualitySummary.Render(report);
        if (!TryWrite(resolvedSummary, summaryDisplayPath, report, out var writeError))
        {
            error.WriteLine(writeError);
            return InvalidReportExitCode;
        }

        output.Write(markdown);
        return WrittenExitCode;
    }

    private static bool TryParse(
        IReadOnlyList<string>? args,
        out string? reportPath,
        out string? summaryPath,
        out string error)
    {
        reportPath = null;
        summaryPath = null;
        if (!IsInvocation(args))
        {
            error = Usage;
            return false;
        }

        var arguments = args ?? [];
        string? report = null;
        string? summary = null;
        for (var index = 1; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (IsOption(argument, ReportOption))
            {
                if (!TryTakeOption(arguments, ref index, argument, ReportOption, ref report, out error))
                {
                    return false;
                }

                continue;
            }

            if (IsOption(argument, OutputOption))
            {
                if (!TryTakeOption(arguments, ref index, argument, OutputOption, ref summary, out error))
                {
                    return false;
                }

                continue;
            }

            error = $"Unknown argument '{argument}'. {Usage}";
            return false;
        }

        reportPath = report;
        summaryPath = summary;
        error = "";
        return true;
    }

    private static bool TryTakeOption(
        IReadOnlyList<string> arguments,
        ref int index,
        string argument,
        string name,
        ref string? value,
        out string error)
    {
        if (value is not null)
        {
            error = $"The {name} option was provided more than once. {Usage}";
            return false;
        }

        var prefix = name + "=";
        if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[prefix.Length..];
            error = "";
            return true;
        }

        if (index + 1 >= arguments.Count || IsKnownOption(arguments[index + 1]))
        {
            error = $"The {name} file was not provided. {Usage}";
            return false;
        }

        index++;
        value = arguments[index];
        error = "";
        return true;
    }

    private static bool IsOption(string argument, string name) =>
        string.Equals(argument, name, StringComparison.OrdinalIgnoreCase) ||
        argument.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownOption(string argument) =>
        IsOption(argument, ReportOption) ||
        IsOption(argument, OutputOption);

    private static string ResolvePath(string path, string? baseDirectory)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }

        var root = Path.GetFullPath(baseDirectory ?? EvaluationRegressionGateRunner.ProjectDirectory());
        return Path.GetFullPath(path, root);
    }

    private static bool TryRead(
        string path,
        string displayPath,
        out EvaluationQualityReport? report,
        out string error)
    {
        report = null;
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            error = $"The quality report '{displayPath}' was not found.";
            return false;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            error = $"The quality report '{displayPath}' could not be read. {exception.Message}";
            return false;
        }

        try
        {
            report = EvaluationQualityReportJson.Deserialize(json);
        }
        catch (JsonException exception)
        {
            error = $"The quality report '{displayPath}' is not valid. {exception.Message}";
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryWrite(
        string path,
        string displayPath,
        EvaluationQualityReport report,
        out string error)
    {
        try
        {
            EvaluationQualitySummary.Write(path, report);
        }
        catch (IOException exception)
        {
            var message = exception.Message;
            if (!string.Equals(path, displayPath, StringComparison.OrdinalIgnoreCase))
            {
                message = message.Replace(path, displayPath, StringComparison.OrdinalIgnoreCase);
            }

            error = message;
            return false;
        }

        error = "";
        return true;
    }
}
