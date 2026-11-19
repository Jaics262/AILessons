using System.Globalization;
using System.Text.Json;

namespace AIPromptLab.Tests;

public sealed class EvaluationQualitySummaryTests
{
    private const string RegressionReason =
        "The failure rate regressed by 6 percentage points, from 10% to 16%. The limit is 5 percentage points.";

    private const string UngroundedReason =
        "Ungrounded claims increased by 2. The limit is 0.";

    private const string MissingFactReason =
        "Missing expected facts increased by 3. The limit is 1.";

    [Fact]
    public void Summary_copies_the_saved_failed_report()
    {
        var report = EvaluationQualityReportJson.Deserialize(FailedJson);

        var markdown = EvaluationQualitySummary.Render(report);

        Assert.Equal(EvaluationQualityReport.FailedStatus, report.Status);
        Assert.Equal(100, report.BaselineCaseCount);
        Assert.Equal(100, report.CandidateCaseCount);
        Assert.Equal(10d, report.BaselineFailureRate);
        Assert.Equal(16d, report.CandidateFailureRate);
        Assert.Equal(6d, report.FailureRateChangePercentagePoints);
        Assert.Equal([RegressionReason], report.FailureReasons);
        Assert.Equal(FailedMarkdown, markdown);
        Assert.Equal(markdown, EvaluationQualitySummary.Render(report));
        Assert.DoesNotContain('\r', markdown);
        Assert.DoesNotContain("timestamp", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evaluatedAt", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Summary_copies_every_failure_reason_in_order()
    {
        var report = EvaluationQualityReportJson.Deserialize(ManyReasonsJson);

        var markdown = EvaluationQualitySummary.Render(report);

        Assert.Equal(
            [RegressionReason, UngroundedReason, MissingFactReason],
            report.FailureReasons);
        var regressionAt = markdown.IndexOf(RegressionReason, StringComparison.Ordinal);
        var ungroundedAt = markdown.IndexOf(UngroundedReason, StringComparison.Ordinal);
        var missingAt = markdown.IndexOf(MissingFactReason, StringComparison.Ordinal);
        Assert.True(regressionAt >= 0 && regressionAt < ungroundedAt && ungroundedAt < missingAt);
        var bullets = markdown.Split('\n').Where(line => line.StartsWith("- ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(
            ["- " + RegressionReason, "- " + UngroundedReason, "- " + MissingFactReason],
            bullets);
    }

    [Fact]
    public void Summary_keeps_a_negative_change_and_zero_rates()
    {
        var markdown = EvaluationQualitySummary.Render(
            EvaluationQualityReportJson.Deserialize(ImprovedJson));

        Assert.Contains("Gate status: Failed", markdown, StringComparison.Ordinal);
        Assert.Contains("| Cases | 4 | 4 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Failure rate | 50% | 25% |", markdown, StringComparison.Ordinal);
        Assert.Contains("Failure rate change: -25 percentage points", markdown, StringComparison.Ordinal);
        Assert.Contains("Exit code: 1", markdown, StringComparison.Ordinal);
        Assert.Contains("- The dataset changed.", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_says_when_a_count_or_rate_was_not_recorded()
    {
        var markdown = EvaluationQualitySummary.Render(
            EvaluationQualityReportJson.Deserialize(MissingBaselineJson));

        Assert.Contains("| Cases | not recorded | 100 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Failure rate | not recorded | 15% |", markdown, StringComparison.Ordinal);
        Assert.Contains("Failure rate change: not recorded", markdown, StringComparison.Ordinal);
        Assert.Contains("Exit code: 2", markdown, StringComparison.Ordinal);
        Assert.Contains(
            "- The baseline snapshot file 'missing.json' was not found.",
            markdown,
            StringComparison.Ordinal);
        Assert.DoesNotContain("| Cases | 0 |", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("0%", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_does_not_apply_the_regression_gate_policy()
    {
        var report = EvaluationQualityReportJson.Deserialize(PassedDespiteRegressionJson);
        var gate = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 100, 10),
            Snapshot("candidate", 100, 16),
            EvaluationRegressionPolicy.Default);

        var markdown = EvaluationQualitySummary.Render(report);

        Assert.False(gate.Passed);
        Assert.Equal(EvaluationQualityReport.PassedStatus, report.Status);
        Assert.Empty(report.FailureReasons);
        Assert.Contains("Gate status: Passed", markdown, StringComparison.Ordinal);
        Assert.Contains("Failure rate change: 6 percentage points", markdown, StringComparison.Ordinal);
        Assert.Contains("None.", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("regressed", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(gate.FailureReasons[0], markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_uses_invariant_number_formatting()
    {
        var report = EvaluationQualityReport.FromStored(
            EvaluationQualityReport.FailedStatus,
            100,
            100,
            10.5d,
            16.25d,
            5.75d,
            ["The failure rate changed."],
            EvaluationRegressionGateRunner.FailedExitCode);
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            var markdown = EvaluationQualitySummary.Render(report);

            Assert.Contains("| Failure rate | 10.5% | 16.25% |", markdown, StringComparison.Ordinal);
            Assert.Contains("Failure rate change: 5.75 percentage points", markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("10,5%", markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("16,25%", markdown, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Deserialize_reads_a_saved_report_without_running_the_gate()
    {
        var report = EvaluationQualityReport.FromSnapshotLoadFailure(
            null,
            Snapshot("candidate", 100, 15),
            ["The baseline snapshot file 'missing.json' was not found."]);

        var again = EvaluationQualityReportJson.Deserialize(EvaluationQualityReportJson.Serialize(report));

        Assert.Equal(report.Status, again.Status);
        Assert.Equal(report.BaselineCaseCount, again.BaselineCaseCount);
        Assert.Equal(report.CandidateCaseCount, again.CandidateCaseCount);
        Assert.Equal(report.BaselineFailureRate, again.BaselineFailureRate);
        Assert.Equal(report.CandidateFailureRate, again.CandidateFailureRate);
        Assert.Equal(report.FailureRateChangePercentagePoints, again.FailureRateChangePercentagePoints);
        Assert.Equal(report.FailureReasons, again.FailureReasons);
        Assert.Equal(report.ExitCode, again.ExitCode);
        Assert.Equal(EvaluationQualitySummary.Render(report), EvaluationQualitySummary.Render(again));
    }

    [Theory]
    [InlineData("", "Quality report JSON is empty.")]
    [InlineData("   ", "Quality report JSON is empty.")]
    [InlineData("{", "open JSON object")]
    [InlineData("[]", "A quality report must be a JSON object.")]
    [InlineData("null", "A quality report must be a JSON object.")]
    public void Deserialize_rejects_malformed_json(string json, string message)
    {
        var error = Assert.Throws<JsonException>(() => EvaluationQualityReportJson.Deserialize(json));

        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_rejects_a_report_that_does_not_match_the_saved_shape()
    {
        AssertMessage(FailedJson.Replace("\"status\": \"Failed\"", "\"state\": \"Failed\"", StringComparison.Ordinal), "is not recognized");
        AssertMessage(FailedJson.Replace("\"exitCode\": 1,\n", "", StringComparison.Ordinal).Replace(",\n  \"exitCode\": 1", "", StringComparison.Ordinal), "missing 'exitCode'");
        AssertMessage(FailedJson.Replace("\"Failed\"", "\"Unknown\"", StringComparison.Ordinal), "Status must be Passed or Failed.");
        AssertMessage(FailedJson.Replace("\"baselineCaseCount\": 100", "\"baselineCaseCount\": -1", StringComparison.Ordinal), "cannot be negative");
        AssertMessage(FailedJson.Replace("\"failureReasons\": [\n    \"" + RegressionReason + "\"\n  ]", "\"failureReasons\": \"nope\"", StringComparison.Ordinal), "must be an array of strings");
        AssertMessage(PassedDespiteRegressionJson.Replace("\"failureReasons\": []", "\"failureReasons\": [\"extra\"]", StringComparison.Ordinal), "cannot include failure reasons");
    }

    [Fact]
    public void Run_writes_the_summary_from_the_saved_report()
    {
        using var files = new TempFiles();
        var reportPath = files.Write("quality.json", FailedJson);
        var summaryPath = files.PathOf("quality-summary.md");

        var (code, output, error) = Execute(reportPath, summaryPath);

        Assert.Equal(EvaluationQualitySummaryRunner.WrittenExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(FailedMarkdown, output);
        Assert.Equal(FailedMarkdown, File.ReadAllText(summaryPath));
        Assert.Equal((byte)'#', File.ReadAllBytes(summaryPath)[0]);
    }

    [Fact]
    public void Run_explains_a_missing_report_and_writes_no_summary()
    {
        using var files = new TempFiles();
        var missing = files.PathOf("missing-quality.json");
        var summaryPath = files.PathOf("quality-summary.md");

        var (code, output, error) = Execute(missing, summaryPath);

        Assert.Equal(EvaluationQualitySummaryRunner.InvalidReportExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal($"The quality report '{missing}' was not found." + Environment.NewLine, error);
        Assert.False(File.Exists(summaryPath));
    }

    [Fact]
    public void Run_explains_malformed_json_and_writes_no_summary()
    {
        using var files = new TempFiles();
        var reportPath = files.Write("quality.json", "{");
        var summaryPath = files.PathOf("quality-summary.md");

        var (code, output, error) = Execute(reportPath, summaryPath);

        Assert.Equal(EvaluationQualitySummaryRunner.InvalidReportExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains($"The quality report '{reportPath}' is not valid.", error, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", error, StringComparison.Ordinal);
        Assert.False(File.Exists(summaryPath));
    }

    [Fact]
    public void Run_repeats_the_same_summary_for_identical_json()
    {
        using var files = new TempFiles();
        var reportPath = files.Write("quality.json", FailedJson);
        var summaryPath = files.PathOf("quality-summary.md");

        var first = Execute(reportPath, summaryPath);
        var firstBytes = File.ReadAllBytes(summaryPath);
        var second = Execute(reportPath, summaryPath);
        var secondBytes = File.ReadAllBytes(summaryPath);

        Assert.Equal(first.ExitCode, second.ExitCode);
        Assert.Equal(first.Output, second.Output);
        Assert.Equal(first.Error, second.Error);
        Assert.Equal(firstBytes, secondBytes);
        var markdown = System.Text.Encoding.UTF8.GetString(firstBytes);
        Assert.DoesNotContain("timestamp", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evaluatedAt", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\r', markdown);
    }

    [Fact]
    public void Run_writes_the_summary_into_a_directory_that_does_not_exist()
    {
        using var files = new TempFiles();
        var summaryPath = files.PathOf("new-reports", "day24", "quality-summary.md");

        Assert.False(Directory.Exists(Path.GetDirectoryName(summaryPath)));

        var (code, _, error) = Execute(files.Write("quality.json", FailedJson), summaryPath);

        Assert.Equal(EvaluationQualitySummaryRunner.WrittenExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(FailedMarkdown, File.ReadAllText(summaryPath));
    }

    [Fact]
    public void Run_explains_a_summary_write_failure()
    {
        using var files = new TempFiles();
        var blocked = files.PathOf("summary-directory");
        Directory.CreateDirectory(blocked);

        var (code, output, error) = Execute(files.Write("quality.json", FailedJson), blocked);

        Assert.Equal(EvaluationQualitySummaryRunner.InvalidReportExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains($"The quality summary '{blocked}' could not be written.", error, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", error, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(blocked, "quality-summary.md")));
    }

    [Fact]
    public void Run_uses_the_default_report_and_summary_names()
    {
        using var files = new TempFiles();
        var root = files.PathOf("project");
        Directory.CreateDirectory(Path.Combine(root, "reports"));
        File.WriteAllText(Path.Combine(root, EvaluationQualitySummaryRunner.DefaultReportPath), FailedJson);

        var (code, output, error) = Execute(reportPath: null, summaryPath: null, baseDirectory: root);
        var summaryPath = Path.Combine(root, EvaluationQualitySummaryRunner.DefaultSummaryPath);

        Assert.Equal(EvaluationQualitySummaryRunner.WrittenExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(FailedMarkdown, output);
        Assert.Equal(FailedMarkdown, File.ReadAllText(summaryPath));
    }

    [Fact]
    public void Relative_summary_write_failure_names_the_relative_path()
    {
        using var files = new TempFiles();
        var root = files.PathOf("project");
        var blocked = Path.Combine(root, "reports");
        Directory.CreateDirectory(blocked);
        var reportPath = Path.Combine("incoming", "quality.json");
        Directory.CreateDirectory(Path.Combine(root, "incoming"));
        File.WriteAllText(Path.Combine(root, reportPath), FailedJson);

        var (code, output, error) = Execute(reportPath, "reports", baseDirectory: root);

        Assert.Equal(EvaluationQualitySummaryRunner.InvalidReportExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("The quality summary 'reports' could not be written.", error, StringComparison.Ordinal);
        Assert.DoesNotContain(root, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_keeps_an_absolute_summary_path()
    {
        using var files = new TempFiles();
        var root = files.PathOf("project");
        Directory.CreateDirectory(root);
        var absolute = files.PathOf("chosen", "quality-summary.md");

        var (code, _, error) = Execute(files.Write("quality.json", FailedJson), absolute, baseDirectory: root);

        Assert.Equal(EvaluationQualitySummaryRunner.WrittenExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(FailedMarkdown, File.ReadAllText(absolute));
        Assert.False(Directory.Exists(Path.Combine(root, "chosen")));
    }

    [Fact]
    public void Command_line_writes_the_summary()
    {
        using var files = new TempFiles();
        var reportPath = files.Write("quality.json", FailedJson);
        var summaryPath = files.PathOf("out", "quality-summary.md");

        var (code, output, error) = ExecuteCommand(
            "quality-summary",
            "--output",
            summaryPath,
            "--report=" + reportPath);

        Assert.Equal(EvaluationQualitySummaryRunner.WrittenExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(FailedMarkdown, output);
        Assert.Equal(FailedMarkdown, File.ReadAllText(summaryPath));
    }

    [Fact]
    public void Command_line_returns_two_for_a_missing_or_repeated_path()
    {
        using var files = new TempFiles();
        var reportPath = files.Write("quality.json", FailedJson);
        var omitted = ExecuteCommand("quality-summary", "--report");
        var repeated = ExecuteCommand(
            "quality-summary",
            "--report",
            reportPath,
            "--output",
            "one.md",
            "--output",
            "two.md");
        var blank = ExecuteCommand("quality-summary", "--report", reportPath, "--output=");
        var unknown = ExecuteCommand(
            "quality-summary",
            "--report",
            reportPath,
            "--output",
            files.PathOf("quality-summary.md"),
            "--extra");

        Assert.Equal(EvaluationQualitySummaryRunner.InvalidReportExitCode, omitted.ExitCode);
        Assert.Contains("The --report file was not provided.", omitted.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationQualitySummaryRunner.InvalidReportExitCode, repeated.ExitCode);
        Assert.Contains("The --output option was provided more than once.", repeated.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationQualitySummaryRunner.InvalidReportExitCode, blank.ExitCode);
        Assert.Contains("The --output file was not provided.", blank.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationQualitySummaryRunner.InvalidReportExitCode, unknown.ExitCode);
        Assert.Contains("Unknown argument '--extra'.", unknown.Error, StringComparison.Ordinal);
        Assert.Equal(string.Empty, omitted.Output);
        Assert.Equal(string.Empty, repeated.Output);
        Assert.Equal(string.Empty, blank.Output);
        Assert.Equal(string.Empty, unknown.Output);
        Assert.False(File.Exists(files.PathOf("quality-summary.md")));
        Assert.False(File.Exists(files.PathOf("one.md")));
    }

    [Fact]
    public void Command_line_explains_a_missing_default_report_from_the_project()
    {
        var (code, output, error) = ExecuteCommand("quality-summary", "--report", "reports/does-not-exist-quality.json");

        Assert.Equal(EvaluationQualitySummaryRunner.InvalidReportExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains(
            "The quality report 'reports/does-not-exist-quality.json' was not found.",
            error,
            StringComparison.Ordinal);
    }

    private static string FailedJson => string.Join('\n',
        "{",
        "  \"status\": \"Failed\",",
        "  \"baselineCaseCount\": 100,",
        "  \"candidateCaseCount\": 100,",
        "  \"baselineFailureRate\": 10,",
        "  \"candidateFailureRate\": 16,",
        "  \"failureRateChangePercentagePoints\": 6,",
        "  \"failureReasons\": [",
        $"    \"{RegressionReason}\"",
        "  ],",
        "  \"exitCode\": 1",
        "}");

    private static string FailedMarkdown => string.Join('\n',
        "# Quality summary",
        "",
        "Gate status: Failed",
        "",
        "| | Baseline | Candidate |",
        "| --- | --- | --- |",
        "| Cases | 100 | 100 |",
        "| Failure rate | 10% | 16% |",
        "",
        "Failure rate change: 6 percentage points",
        "",
        "Exit code: 1",
        "",
        "## Failure reasons",
        "",
        $"- {RegressionReason}",
        "");

    private static string ManyReasonsJson => string.Join('\n',
        "{",
        "  \"status\": \"Failed\",",
        "  \"baselineCaseCount\": 100,",
        "  \"candidateCaseCount\": 100,",
        "  \"baselineFailureRate\": 10,",
        "  \"candidateFailureRate\": 20,",
        "  \"failureRateChangePercentagePoints\": 10,",
        "  \"failureReasons\": [",
        $"    \"{RegressionReason}\",",
        $"    \"{UngroundedReason}\",",
        $"    \"{MissingFactReason}\"",
        "  ],",
        "  \"exitCode\": 1",
        "}");

    private static string ImprovedJson => string.Join('\n',
        "{",
        "  \"status\": \"Failed\",",
        "  \"baselineCaseCount\": 4,",
        "  \"candidateCaseCount\": 4,",
        "  \"baselineFailureRate\": 50,",
        "  \"candidateFailureRate\": 25,",
        "  \"failureRateChangePercentagePoints\": -25,",
        "  \"failureReasons\": [",
        "    \"The dataset changed.\"",
        "  ],",
        "  \"exitCode\": 1",
        "}");

    private static string MissingBaselineJson => string.Join('\n',
        "{",
        "  \"status\": \"Failed\",",
        "  \"baselineCaseCount\": null,",
        "  \"candidateCaseCount\": 100,",
        "  \"baselineFailureRate\": null,",
        "  \"candidateFailureRate\": 15,",
        "  \"failureRateChangePercentagePoints\": null,",
        "  \"failureReasons\": [",
        "    \"The baseline snapshot file 'missing.json' was not found.\"",
        "  ],",
        "  \"exitCode\": 2",
        "}");

    private static string PassedDespiteRegressionJson => string.Join('\n',
        "{",
        "  \"status\": \"Passed\",",
        "  \"baselineCaseCount\": 100,",
        "  \"candidateCaseCount\": 100,",
        "  \"baselineFailureRate\": 10,",
        "  \"candidateFailureRate\": 16,",
        "  \"failureRateChangePercentagePoints\": 6,",
        "  \"failureReasons\": [],",
        "  \"exitCode\": 0",
        "}");

    private static void AssertMessage(string json, string message)
    {
        var error = Assert.Throws<JsonException>(() => EvaluationQualityReportJson.Deserialize(json));
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output, string Error) Execute(
        string? reportPath,
        string? summaryPath,
        string? baseDirectory = null)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = EvaluationQualitySummaryRunner.Run(reportPath, summaryPath, output, error, baseDirectory);
        return (code, output.ToString(), error.ToString());
    }

    private static (int ExitCode, string Output, string Error) ExecuteCommand(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = EvaluationQualitySummaryRunner.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static EvaluationSnapshot Snapshot(string runId, int totalCases, int failedCases)
    {
        return new EvaluationSnapshot(
            runId,
            new DateTimeOffset(2026, 11, 12, 18, 0, 0, TimeSpan.Zero),
            totalCases,
            failedCases,
            totalCases == 0 ? 0d : (double)failedCases / totalCases * 100d,
            new Dictionary<EvaluationFailureCategory, int>
            {
                [EvaluationFailureCategory.UngroundedClaim] = 0,
                [EvaluationFailureCategory.MissingExpectedFact] = 0,
                [EvaluationFailureCategory.EmptyAnswer] = 0,
                [EvaluationFailureCategory.Other] = failedCases
            });
    }

    private sealed class TempFiles : IDisposable
    {
        public TempFiles()
        {
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "evaluation-quality-summary-" + Guid.NewGuid().ToString("N"));

        public string Write(string name, string json)
        {
            var path = PathOf(name);
            File.WriteAllText(path, json);
            return path;
        }

        public string PathOf(params string[] parts) => Path.Combine([DirectoryPath, .. parts]);

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
