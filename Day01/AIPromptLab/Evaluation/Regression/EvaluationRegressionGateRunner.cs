using System.Text.Json;

// Runs EvaluationRegressionGate from two saved snapshot files.
// The gate owns the comparison and the policy. This runner loads the files, prints result.Report,
// and turns that result into a process exit code a pipeline can use.
// 0 means the gate passed. 1 means the gate failed. 2 means a snapshot file is missing or invalid.
// A missing or unreadable file stays 2. The gate does not see a snapshot that failed to load,
// so a broken file is not reported as a quality regression.
// File errors go to the error output. The gate report goes to the standard output.
// The regression-gate command uses EvaluationRegressionPolicy.Default.
// --report writes EvaluationQualityReport as JSON. The report copies the gate result.
// It does not apply the policy again, and it has no timestamp.
// A missing or invalid snapshot still has an outcome: status Failed, exit code 2, and the file error.
// The report includes that outcome when a report path was given. A count or rate that was not loaded
// is null, and the failure-rate change stays null because the gate did not run. The error output
// still names the file. Exit code 2 does not change.
// Write creates the report directory when it is missing.
// A relative --report path is resolved from the project directory, the folder that contains
// AIPromptLab.csproj. An absolute report path is used as given.
// If the report file cannot be written, the error output says so and the exit code stays the same.
// A relative path is shown relative to the project.
public static class EvaluationRegressionGateRunner
{
    public const string CommandName = "regression-gate";

    public const int PassedExitCode = 0;

    public const int FailedExitCode = 1;

    public const int InvalidSnapshotExitCode = 2;

    public const string Usage =
        $"Usage: {CommandName} --baseline <snapshot.json> --candidate <snapshot.json> [--report <report.json>]";

    private const string BaselineOption = "--baseline";

    private const string CandidateOption = "--candidate";

    private const string ReportOption = "--report";

    public static bool IsInvocation(IReadOnlyList<string>? args) =>
        args is { Count: > 0 } &&
        string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase);

    public static int Run(IReadOnlyList<string>? args, TextWriter? output = null, TextWriter? error = null)
    {
        error ??= Console.Error;
        if (!TryParse(args, out var baselinePath, out var candidatePath, out var reportPath, out var parseError))
        {
            error.WriteLine(parseError);
            return InvalidSnapshotExitCode;
        }

        return Run(baselinePath, candidatePath, output, error, reportPath: reportPath);
    }

    public static string ProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (IsProject(directory.FullName))
            {
                return directory.FullName;
            }

            var child = Path.Combine(directory.FullName, "AIPromptLab");
            if (IsProject(child))
            {
                return child;
            }

            var dayChild = Path.Combine(directory.FullName, "Day01", "AIPromptLab");
            if (IsProject(dayChild))
            {
                return dayChild;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    public static int Run(
        string? baselinePath,
        string? candidatePath,
        TextWriter? output = null,
        TextWriter? error = null,
        EvaluationRegressionPolicy? policy = null,
        string? reportPath = null,
        string? reportBaseDirectory = null)
    {
        output ??= Console.Out;
        error ??= Console.Error;
        policy ??= EvaluationRegressionPolicy.Default;
        if (reportPath is not null && string.IsNullOrWhiteSpace(reportPath))
        {
            error.WriteLine($"The {ReportOption} file was not provided. {Usage}");
            return InvalidSnapshotExitCode;
        }

        var reportDisplayPath = reportPath;
        if (reportPath is not null)
        {
            reportPath = ResolveReportPath(reportPath, reportBaseDirectory);
        }

        var baselineReady = TryRead(baselinePath, "baseline", out var baseline, out var baselineError);
        var candidateReady = TryRead(candidatePath, "candidate", out var candidate, out var candidateError);
        if (!baselineReady || !candidateReady || baseline is null || candidate is null)
        {
            var reasons = new List<string>();
            if (!baselineReady)
            {
                error.WriteLine(baselineError);
                reasons.Add(baselineError);
            }

            if (!candidateReady)
            {
                error.WriteLine(candidateError);
                reasons.Add(candidateError);
            }

            if (reportPath is not null)
            {
                TryWriteReport(
                    reportPath,
                    reportDisplayPath ?? reportPath,
                    EvaluationQualityReport.FromSnapshotLoadFailure(baseline, candidate, reasons),
                    error);
            }

            return InvalidSnapshotExitCode;
        }

        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, policy);
        output.WriteLine(result.Report);
        var exitCode = result.Passed ? PassedExitCode : FailedExitCode;
        if (reportPath is not null)
        {
            TryWriteReport(
                reportPath,
                reportDisplayPath ?? reportPath,
                EvaluationQualityReport.From(result),
                error);
        }

        return exitCode;
    }

    private static bool TryParse(
        IReadOnlyList<string>? args,
        out string baselinePath,
        out string candidatePath,
        out string? reportPath,
        out string error)
    {
        baselinePath = "";
        candidatePath = "";
        reportPath = null;
        if (!IsInvocation(args))
        {
            error = Usage;
            return false;
        }

        var arguments = args ?? [];
        string? baseline = null;
        string? candidate = null;
        string? report = null;
        for (var index = 1; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (IsOption(argument, BaselineOption))
            {
                if (!TryTakeOption(arguments, ref index, argument, BaselineOption, ref baseline, out error))
                {
                    return false;
                }

                continue;
            }

            if (IsOption(argument, CandidateOption))
            {
                if (!TryTakeOption(arguments, ref index, argument, CandidateOption, ref candidate, out error))
                {
                    return false;
                }

                continue;
            }

            if (IsOption(argument, ReportOption))
            {
                if (!TryTakeOption(arguments, ref index, argument, ReportOption, ref report, out error))
                {
                    return false;
                }

                continue;
            }

            error = $"Unknown argument '{argument}'. {Usage}";
            return false;
        }

        if (baseline is null || candidate is null)
        {
            error = Usage;
            return false;
        }

        baselinePath = baseline;
        candidatePath = candidate;
        reportPath = report;
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
        IsOption(argument, BaselineOption) ||
        IsOption(argument, CandidateOption) ||
        IsOption(argument, ReportOption);

    private static string ResolveReportPath(string path, string? baseDirectory)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }

        var root = Path.GetFullPath(baseDirectory ?? ProjectDirectory());
        return Path.GetFullPath(path, root);
    }

    private static bool IsProject(string directory) =>
        File.Exists(Path.Combine(directory, "AIPromptLab.csproj"));

    private static void TryWriteReport(
        string path,
        string displayPath,
        EvaluationQualityReport report,
        TextWriter error)
    {
        try
        {
            EvaluationQualityReportJson.Write(path, report);
        }
        catch (IOException exception)
        {
            var message = exception.Message;
            if (!string.Equals(path, displayPath, StringComparison.OrdinalIgnoreCase))
            {
                message = message.Replace(path, displayPath, StringComparison.OrdinalIgnoreCase);
            }

            error.WriteLine(message);
        }
    }

    private static bool TryRead(string? path, string role, out EvaluationSnapshot? snapshot, out string error)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = $"The {role} snapshot file was not provided.";
            return false;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            error = $"The {role} snapshot file '{path}' was not found.";
            return false;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            error = $"The {role} snapshot file '{path}' could not be read. {exception.Message}";
            return false;
        }

        try
        {
            snapshot = EvaluationSnapshotJson.Deserialize(json);
        }
        catch (JsonException exception)
        {
            error = $"The {role} snapshot file '{path}' is not valid. {exception.Message}";
            return false;
        }

        error = "";
        return true;
    }
}
