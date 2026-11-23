using System.Text.Json;

namespace AIPromptLab.Tests;

public sealed class EvaluationQualityReportTests
{
    private static DateTimeOffset EvaluatedAt { get; } =
        new(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);

    private static readonly string[] PropertyNames =
    [
        "status",
        "baselineCaseCount",
        "candidateCaseCount",
        "baselineFailureRate",
        "candidateFailureRate",
        "failureRateChangePercentagePoints",
        "failureReasons",
        "exitCode"
    ];

    [Fact]
    public void Report_copies_a_passing_gate_result_as_percentages()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);

        var report = EvaluationQualityReport.From(result);
        var json = EvaluationQualityReportJson.Serialize(report);

        Assert.True(result.Passed);
        Assert.Equal(EvaluationQualityReport.PassedStatus, report.Status);
        Assert.Equal(100, report.BaselineCaseCount);
        Assert.Equal(100, report.CandidateCaseCount);
        Assert.Equal(baseline.FailureRate, report.BaselineFailureRate);
        Assert.Equal(candidate.FailureRate, report.CandidateFailureRate);
        Assert.Equal(10d, report.BaselineFailureRate);
        Assert.Equal(15d, report.CandidateFailureRate);
        Assert.Equal(result.Comparison!.FailureRateChange, report.FailureRateChangePercentagePoints);
        Assert.Equal(5d, report.FailureRateChangePercentagePoints);
        Assert.Empty(report.FailureReasons);
        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, report.ExitCode);
        Assert.Equal(PassedJson, json);
        Assert.Equal(json, EvaluationQualityReportJson.Serialize(report));
        AssertPropertyNames(json);
    }

    [Fact]
    public void Report_copies_every_gate_failure_reason_in_order()
    {
        var baseline = Snapshot("baseline", 100, 10, ungrounded: 1, missing: 1, other: 8);
        var candidate = Snapshot("candidate", 100, 20, ungrounded: 3, missing: 4, other: 13);
        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);
        var report = EvaluationQualityReport.From(result);

        Assert.False(result.Passed);
        Assert.Equal(EvaluationQualityReport.FailedStatus, report.Status);
        Assert.Equal(result.FailureReasons, report.FailureReasons);
        Assert.Equal(3, report.FailureReasons.Count);
        Assert.Equal(10d, report.FailureRateChangePercentagePoints);
        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, report.ExitCode);

        var reasons = (string[])result.FailureReasons;
        var original = reasons[0];
        reasons[0] = "changed";

        Assert.Equal(original, report.FailureReasons[0]);
    }

    [Fact]
    public void Report_keeps_a_negative_change_when_the_gate_still_fails()
    {
        var cases = new[] { "alpha", "beta", "gamma", "delta" };
        var result = EvaluationRegressionGate.Evaluate(
            Snapshot("baseline", 4, 2, ungrounded: 0, missing: 1, caseIds: cases),
            Snapshot("candidate", 4, 1, ungrounded: 1, missing: 1, caseIds: cases),
            EvaluationRegressionPolicy.Default);
        var report = EvaluationQualityReport.From(result);

        Assert.Equal(-25d, result.Comparison!.FailureRateChange);
        Assert.False(result.Passed);
        Assert.Equal(EvaluationQualityReport.FailedStatus, report.Status);
        Assert.Equal(-25d, report.FailureRateChangePercentagePoints);
        Assert.Equal(result.FailureReasons, report.FailureReasons);
        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, report.ExitCode);
        Assert.Contains(
            "\"failureRateChangePercentagePoints\": -25",
            EvaluationQualityReportJson.Serialize(report),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Report_needs_a_gate_comparison()
    {
        var result = EvaluationRegressionGate.Evaluate(null, Snapshot("candidate", 1, 0), EvaluationRegressionPolicy.Default);

        var error = Assert.Throws<ArgumentException>(() => EvaluationQualityReport.From(result));

        Assert.Contains("regression-gate comparison", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_load_failure_records_only_the_loaded_rates()
    {
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        var reason = "The baseline snapshot file 'missing.json' was not found.";

        var report = EvaluationQualityReport.FromSnapshotLoadFailure(null, candidate, [reason]);

        Assert.Equal(EvaluationQualityReport.FailedStatus, report.Status);
        Assert.Null(report.BaselineCaseCount);
        Assert.Null(report.BaselineFailureRate);
        Assert.Equal(100, report.CandidateCaseCount);
        Assert.Equal(15d, report.CandidateFailureRate);
        Assert.Null(report.FailureRateChangePercentagePoints);
        Assert.Equal([reason], report.FailureReasons);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, report.ExitCode);
        Assert.DoesNotContain("snapshot is missing", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_input_records_loaded_snapshots_without_a_gate_comparison()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        var reason = "The policy file 'policy.json' is not valid. 'maxFailureRateRegressionPercentagePoints' must be a non-negative number.";

        var report = EvaluationQualityReport.FromInvalidInput(baseline, candidate, [reason]);

        Assert.Equal(EvaluationQualityReport.FailedStatus, report.Status);
        Assert.Equal(100, report.BaselineCaseCount);
        Assert.Equal(10d, report.BaselineFailureRate);
        Assert.Equal(100, report.CandidateCaseCount);
        Assert.Equal(15d, report.CandidateFailureRate);
        Assert.Null(report.FailureRateChangePercentagePoints);
        Assert.Equal([reason], report.FailureReasons);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, report.ExitCode);
    }

    [Fact]
    public void Snapshot_load_failure_does_not_replace_the_gate()
    {
        var baseline = Snapshot("baseline", 2, 0, caseIds: ["alpha", "beta"]);
        var candidate = Snapshot("candidate", 2, 0, caseIds: ["alpha", "beta"]);

        var error = Assert.Throws<ArgumentException>(() =>
            EvaluationQualityReport.FromSnapshotLoadFailure(baseline, candidate, ["unused"]));

        Assert.Contains("regression-gate result", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_writes_the_passing_report_and_keeps_exit_code_zero()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);
        using var files = new TempFiles();
        var reportPath = files.PathOf("quality.json");

        var (code, output, error) = Execute(
            files.Write("baseline.json", baseline),
            files.Write("candidate.json", candidate),
            reportPath: reportPath);
        var report = Read(reportPath);

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Equal(result.Report + Environment.NewLine, output);
        Assert.Equal(string.Empty, error);
        Assert.Equal(JsonValueKind.Object, report.ValueKind);
        Assert.Equal("Passed", report.GetProperty("status").GetString());
        Assert.Equal(0, report.GetProperty("exitCode").GetInt32());
        Assert.Equal(PassedJson, File.ReadAllText(reportPath));
        Assert.Equal((byte)'{', File.ReadAllBytes(reportPath)[0]);
    }

    [Fact]
    public void Run_writes_zero_rates_as_numbers()
    {
        var cases = new[] { "alpha", "beta" };
        var baseline = Snapshot("baseline", 2, 0, caseIds: cases);
        var candidate = Snapshot("candidate", 2, 0, caseIds: cases);
        using var files = new TempFiles();
        var reportPath = files.PathOf("quality.json");

        var (code, _, error) = Execute(
            files.Write("baseline.json", baseline),
            files.Write("candidate.json", candidate),
            reportPath: reportPath);
        var report = Read(reportPath);

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal(0, report.GetProperty("baselineFailureRate").GetDouble());
        Assert.Equal(0, report.GetProperty("candidateFailureRate").GetDouble());
        Assert.Equal(0, report.GetProperty("failureRateChangePercentagePoints").GetDouble());
        Assert.Equal(JsonValueKind.Number, report.GetProperty("baselineFailureRate").ValueKind);
    }

    [Fact]
    public void Run_writes_the_failing_report_and_keeps_exit_code_one()
    {
        var baseline = Snapshot("baseline", 100, 10, ungrounded: 1, missing: 1, other: 8);
        var candidate = Snapshot("candidate", 100, 20, ungrounded: 3, missing: 4, other: 13);
        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);
        using var files = new TempFiles();
        var reportPath = files.PathOf("quality.json");

        var (code, output, error) = Execute(
            files.Write("baseline.json", baseline),
            files.Write("candidate.json", candidate),
            reportPath: reportPath);
        var report = Read(reportPath);

        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, code);
        Assert.Equal(result.Report + Environment.NewLine, output);
        Assert.Equal(string.Empty, error);
        Assert.Equal(JsonValueKind.Object, report.ValueKind);
        Assert.Equal("Failed", report.GetProperty("status").GetString());
        Assert.Equal(1, report.GetProperty("exitCode").GetInt32());
        Assert.Equal(result.FailureReasons, ReadReasons(report));
        var json = File.ReadAllText(reportPath);
        Assert.NotEmpty(result.FailureReasons);
        foreach (var reason in result.FailureReasons)
        {
            Assert.Contains(reason, json, StringComparison.Ordinal);
        }

        Assert.Equal(10d, report.GetProperty("baselineFailureRate").GetDouble());
        Assert.Equal(20d, report.GetProperty("candidateFailureRate").GetDouble());
        Assert.Equal(10d, report.GetProperty("failureRateChangePercentagePoints").GetDouble());
    }

    [Fact]
    public void Run_records_the_failure_rate_change_in_percentage_points()
    {
        using var files = new TempFiles();
        var worsened = WritePair(
            files,
            "worsened-baseline.json",
            "worsened-candidate.json",
            failedBaseline: 15,
            failedCandidate: 22,
            totalCases: 100);
        var improved = WritePair(
            files,
            "improved-baseline.json",
            "improved-candidate.json",
            failedBaseline: 6,
            failedCandidate: 3,
            totalCases: 20);

        var worsenedReport = Read(worsened);
        var improvedReport = Read(improved);

        Assert.Equal(15d, worsenedReport.GetProperty("baselineFailureRate").GetDouble());
        Assert.Equal(22d, worsenedReport.GetProperty("candidateFailureRate").GetDouble());
        Assert.Equal(7d, worsenedReport.GetProperty("failureRateChangePercentagePoints").GetDouble());
        Assert.Equal(
            worsenedReport.GetProperty("candidateFailureRate").GetDouble() -
            worsenedReport.GetProperty("baselineFailureRate").GetDouble(),
            worsenedReport.GetProperty("failureRateChangePercentagePoints").GetDouble());
        Assert.Equal(JsonValueKind.Number, worsenedReport.GetProperty("failureRateChangePercentagePoints").ValueKind);

        Assert.Equal(30d, improvedReport.GetProperty("baselineFailureRate").GetDouble());
        Assert.Equal(15d, improvedReport.GetProperty("candidateFailureRate").GetDouble());
        Assert.Equal(-15d, improvedReport.GetProperty("failureRateChangePercentagePoints").GetDouble());
        Assert.Equal(
            improvedReport.GetProperty("candidateFailureRate").GetDouble() -
            improvedReport.GetProperty("baselineFailureRate").GetDouble(),
            improvedReport.GetProperty("failureRateChangePercentagePoints").GetDouble());
    }

    [Fact]
    public void Run_writes_the_report_into_a_directory_that_does_not_exist()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        using var files = new TempFiles();
        var reportPath = files.PathOf("new-reports", "day22", "quality.json");
        var reportDirectory = Path.GetDirectoryName(reportPath);

        Assert.False(Directory.Exists(reportDirectory));

        var (code, _, error) = Execute(
            files.Write("baseline.json", baseline),
            files.Write("candidate.json", candidate),
            reportPath: reportPath);
        var report = Read(reportPath);

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.True(Directory.Exists(reportDirectory));
        Assert.True(File.Exists(reportPath));
        Assert.Equal(JsonValueKind.Object, report.ValueKind);
        Assert.Equal("Passed", report.GetProperty("status").GetString());
    }

    [Fact]
    public void Run_uses_the_given_policy_instead_of_a_second_rule()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 16, other: 16);
        var policy = EvaluationRegressionPolicy.Default with { MaximumFailureRateRegression = 6d };
        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, policy);
        using var files = new TempFiles();
        var reportPath = files.PathOf("quality.json");

        var (code, _, error) = Execute(
            files.Write("baseline.json", baseline),
            files.Write("candidate.json", candidate),
            policy,
            reportPath);
        var report = Read(reportPath);

        Assert.True(result.Passed);
        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.Equal("Passed", report.GetProperty("status").GetString());
        Assert.Equal(0, report.GetProperty("exitCode").GetInt32());
        Assert.Equal(6d, report.GetProperty("failureRateChangePercentagePoints").GetDouble());
        Assert.Empty(ReadReasons(report));
    }

    [Fact]
    public void Run_writes_a_report_when_a_snapshot_file_is_missing()
    {
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        using var files = new TempFiles();
        var missing = files.PathOf("missing-baseline.json");
        var reportPath = files.PathOf("out", "quality.json");

        var (code, output, error) = Execute(missing, files.Write("candidate.json", candidate), reportPath: reportPath);
        var report = Read(reportPath);
        var reason = $"The baseline snapshot file '{missing}' was not found.";

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal(reason + Environment.NewLine, error);
        Assert.Equal("Failed", report.GetProperty("status").GetString());
        Assert.Equal(2, report.GetProperty("exitCode").GetInt32());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("baselineCaseCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("baselineFailureRate").ValueKind);
        Assert.Equal(100, report.GetProperty("candidateCaseCount").GetInt32());
        Assert.Equal(15d, report.GetProperty("candidateFailureRate").GetDouble());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("failureRateChangePercentagePoints").ValueKind);
        Assert.Equal([reason], ReadReasons(report));
        Assert.DoesNotContain("The baseline snapshot is missing.", File.ReadAllText(reportPath), StringComparison.Ordinal);
        Assert.DoesNotContain("Regression gate:", output, StringComparison.Ordinal);
        AssertPropertyNames(File.ReadAllText(reportPath));
    }

    [Fact]
    public void Run_writes_a_report_when_the_candidate_json_is_malformed()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        using var files = new TempFiles();
        var candidatePath = files.WriteRaw("candidate.json", "{");
        var reportPath = files.PathOf("quality.json");

        var (code, output, error) = Execute(files.Write("baseline.json", baseline), candidatePath, reportPath: reportPath);
        var report = Read(reportPath);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains($"The candidate snapshot file '{candidatePath}' is not valid.", error, StringComparison.Ordinal);
        Assert.Equal(100, report.GetProperty("baselineCaseCount").GetInt32());
        Assert.Equal(10d, report.GetProperty("baselineFailureRate").GetDouble());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("candidateCaseCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("candidateFailureRate").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("failureRateChangePercentagePoints").ValueKind);
        Assert.Equal(2, report.GetProperty("exitCode").GetInt32());
        Assert.Contains("is not valid", Assert.Single(ReadReasons(report)), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_writes_a_report_when_neither_snapshot_can_be_read()
    {
        using var files = new TempFiles();
        var baselinePath = files.PathOf("missing-baseline.json");
        var candidatePath = files.PathOf("missing-candidate.json");
        var reportPath = files.PathOf("quality.json");

        var (code, output, error) = Execute(baselinePath, candidatePath, reportPath: reportPath);
        var report = Read(reportPath);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("baseline snapshot file", error, StringComparison.Ordinal);
        Assert.Contains("candidate snapshot file", error, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("baselineCaseCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("candidateCaseCount").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("baselineFailureRate").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("candidateFailureRate").ValueKind);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("failureRateChangePercentagePoints").ValueKind);
        Assert.Equal(2, ReadReasons(report).Count);
        Assert.Equal(2, report.GetProperty("exitCode").GetInt32());
    }

    [Fact]
    public void Run_leaves_the_existing_output_unchanged_when_no_report_is_requested()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 16, other: 16);
        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);
        using var files = new TempFiles();
        var baselinePath = files.Write("baseline.json", baseline);
        var candidatePath = files.Write("candidate.json", candidate);
        var before = Directory.GetFiles(files.Directory, "*", SearchOption.AllDirectories);

        var (code, output, error) = Execute(baselinePath, candidatePath);

        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, code);
        Assert.Equal(result.Report + Environment.NewLine, output);
        Assert.Equal(string.Empty, error);
        Assert.Equal(before, Directory.GetFiles(files.Directory, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Run_repeats_the_same_json_for_identical_inputs()
    {
        var baseline = Snapshot("baseline", 100, 10, ungrounded: 1, missing: 1, other: 8);
        var candidate = Snapshot("candidate", 100, 20, ungrounded: 3, missing: 4, other: 13);
        using var files = new TempFiles();
        var baselinePath = files.Write("baseline.json", baseline);
        var candidatePath = files.Write("candidate.json", candidate);
        var reportPath = files.PathOf("quality.json");

        var first = Execute(baselinePath, candidatePath, reportPath: reportPath);
        var firstBytes = File.ReadAllBytes(reportPath);
        var second = Execute(baselinePath, candidatePath, reportPath: reportPath);
        var secondBytes = File.ReadAllBytes(reportPath);

        Assert.Equal(first.ExitCode, second.ExitCode);
        Assert.Equal(first.Output, second.Output);
        Assert.Equal(first.Error, second.Error);
        Assert.Equal(firstBytes, secondBytes);
        var json = System.Text.Encoding.UTF8.GetString(firstBytes);
        Assert.DoesNotContain("timestamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evaluatedAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(JsonValueKind.Object, Read(reportPath).ValueKind);
    }

    [Fact]
    public void Run_explains_a_report_write_failure_and_keeps_the_gate_exit_code()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var passing = Snapshot("passing", 100, 15, other: 15);
        var failing = Snapshot("failing", 100, 16, other: 16);
        using var files = new TempFiles();
        var baselinePath = files.Write("baseline.json", baseline);
        var passingPath = files.Write("passing.json", passing);
        var failingPath = files.Write("failing.json", failing);
        var blocked = files.PathOf("report-directory");
        Directory.CreateDirectory(blocked);

        var passed = Execute(baselinePath, passingPath, reportPath: blocked);
        var failed = Execute(baselinePath, failingPath, reportPath: blocked);

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, passed.ExitCode);
        Assert.Contains("Regression gate: PASSED", passed.Output, StringComparison.Ordinal);
        Assert.Contains($"The quality report '{blocked}' could not be written.", passed.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", passed.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, failed.ExitCode);
        Assert.Contains("Regression gate: FAILED", failed.Output, StringComparison.Ordinal);
        Assert.Contains($"The quality report '{blocked}' could not be written.", failed.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(blocked, "quality.json")));
    }

    [Fact]
    public void Run_explains_a_report_write_failure_and_keeps_exit_code_two()
    {
        using var files = new TempFiles();
        var missing = files.PathOf("missing.json");
        var blocked = files.PathOf("report-directory");
        Directory.CreateDirectory(blocked);

        var (code, output, error) = Execute(missing, files.WriteRaw("candidate.json", "{"), reportPath: blocked);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains($"The baseline snapshot file '{missing}' was not found.", error, StringComparison.Ordinal);
        Assert.Contains("is not valid", error, StringComparison.Ordinal);
        Assert.Contains($"The quality report '{blocked}' could not be written.", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Command_line_writes_the_report_for_a_passing_gate()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        var result = EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default);
        using var files = new TempFiles();
        var baselinePath = files.Write("baseline.json", baseline);
        var candidatePath = files.Write("candidate.json", candidate);
        var reportPath = files.PathOf("quality.json");

        var (code, output, error) = ExecuteCommand(
            "regression-gate",
            "--candidate",
            candidatePath,
            "--report=" + reportPath,
            "--baseline",
            baselinePath);

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Equal(result.Report + Environment.NewLine, output);
        Assert.Equal(string.Empty, error);
        Assert.Equal(PassedJson, File.ReadAllText(reportPath));
    }

    [Fact]
    public void Command_line_returns_two_for_a_missing_or_repeated_report_path()
    {
        using var files = new TempFiles();
        var baselinePath = files.WriteRaw("baseline.json", "{}");
        var candidatePath = files.WriteRaw("candidate.json", "{}");
        var omitted = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--report");
        var repeated = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--report",
            "one.json",
            "--report",
            "two.json");
        var blank = ExecuteCommand(
            "regression-gate",
            "--baseline",
            files.PathOf("missing-baseline.json"),
            "--candidate",
            files.PathOf("missing-candidate.json"),
            "--report=");
        var unknown = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--report",
            files.PathOf("quality.json"),
            "--extra");

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, omitted.ExitCode);
        Assert.Contains("The --report file was not provided.", omitted.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, repeated.ExitCode);
        Assert.Contains("The --report option was provided more than once.", repeated.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, blank.ExitCode);
        Assert.Contains("The --report file was not provided.", blank.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("was not found", blank.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, unknown.ExitCode);
        Assert.Contains("Unknown argument '--extra'.", unknown.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(files.PathOf("quality.json")));
        Assert.Equal(string.Empty, omitted.Output);
        Assert.Equal(string.Empty, repeated.Output);
        Assert.Equal(string.Empty, blank.Output);
        Assert.Equal(string.Empty, unknown.Output);
    }

    private static string PassedJson => string.Join('\n',
        "{",
        "  \"status\": \"Passed\",",
        "  \"baselineCaseCount\": 100,",
        "  \"candidateCaseCount\": 100,",
        "  \"baselineFailureRate\": 10,",
        "  \"candidateFailureRate\": 15,",
        "  \"failureRateChangePercentagePoints\": 5,",
        "  \"failureReasons\": [],",
        "  \"exitCode\": 0",
        "}");

    private static void AssertPropertyNames(string json)
    {
        using var document = JsonDocument.Parse(json);
        var names = new List<string>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            names.Add(property.Name);
        }

        Assert.Equal(PropertyNames, names);
    }

    private static JsonElement Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static List<string> ReadReasons(JsonElement report)
    {
        var reasons = new List<string>();
        foreach (var reason in report.GetProperty("failureReasons").EnumerateArray())
        {
            reasons.Add(reason.GetString()!);
        }

        return reasons;
    }

    [Fact]
    public void Run_writes_a_relative_report_path_under_the_project_directory()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        using var files = new TempFiles();
        var project = files.PathOf("project");
        Directory.CreateDirectory(project);
        var relative = Path.Combine("reports", "quality.json");

        var (code, _, error) = Execute(
            files.Write("baseline.json", baseline),
            files.Write("candidate.json", candidate),
            reportPath: relative,
            reportBaseDirectory: project);
        var written = Path.Combine(project, relative);

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.True(File.Exists(written));
        Assert.Equal("Passed", Read(written).GetProperty("status").GetString());
    }

    [Fact]
    public void Run_keeps_an_absolute_report_path()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        using var files = new TempFiles();
        var absolute = files.PathOf("chosen", "quality.json");
        var otherProject = files.PathOf("other-project");
        Directory.CreateDirectory(otherProject);

        var (code, _, error) = Execute(
            files.Write("baseline.json", baseline),
            files.Write("candidate.json", candidate),
            reportPath: absolute,
            reportBaseDirectory: otherProject);

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.True(File.Exists(absolute));
        Assert.False(Directory.Exists(Path.Combine(otherProject, "chosen")));
    }

    [Fact]
    public void Relative_report_write_failure_names_the_project_relative_path()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("candidate", 100, 15, other: 15);
        using var files = new TempFiles();
        var project = files.PathOf("project");
        var blocked = Path.Combine(project, "reports");
        Directory.CreateDirectory(blocked);

        var (code, output, error) = Execute(
            files.Write("baseline.json", baseline),
            files.Write("candidate.json", candidate),
            reportPath: Path.Combine("reports"),
            reportBaseDirectory: project);

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Contains("Regression gate: PASSED", output, StringComparison.Ordinal);
        Assert.Contains(
            $"The quality report '{Path.Combine("reports")}' could not be written.",
            error,
            StringComparison.Ordinal);
        Assert.DoesNotContain(project, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_directory_contains_the_prompt_lab_project()
    {
        var project = EvaluationRegressionGateRunner.ProjectDirectory();

        Assert.True(File.Exists(Path.Combine(project, "AIPromptLab.csproj")));
    }

    private static (int ExitCode, string Output, string Error) Execute(
        string? baselinePath,
        string? candidatePath,
        EvaluationRegressionPolicy? policy = null,
        string? reportPath = null,
        string? reportBaseDirectory = null)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = EvaluationRegressionGateRunner.Run(
            baselinePath,
            candidatePath,
            output,
            error,
            policy,
            reportPath,
            reportBaseDirectory);
        return (code, output.ToString(), error.ToString());
    }

    private static (int ExitCode, string Output, string Error) ExecuteCommand(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = EvaluationRegressionGateRunner.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static string WritePair(
        TempFiles files,
        string baselineName,
        string candidateName,
        int failedBaseline,
        int failedCandidate,
        int totalCases)
    {
        var baselinePath = files.WriteRaw(baselineName, SnapshotJson("baseline", totalCases, failedBaseline));
        var candidatePath = files.WriteRaw(candidateName, SnapshotJson("candidate", totalCases, failedCandidate));
        var reportPath = files.PathOf(Path.GetFileNameWithoutExtension(baselineName) + "-report.json");
        var (code, _, error) = Execute(baselinePath, candidatePath, reportPath: reportPath);

        Assert.Equal(string.Empty, error);
        Assert.Equal(
            failedCandidate > failedBaseline
                ? EvaluationRegressionGateRunner.FailedExitCode
                : EvaluationRegressionGateRunner.PassedExitCode,
            code);
        return reportPath;
    }

    private static string SnapshotJson(string runId, int totalCases, int failedCases)
    {
        var rate = Rate(failedCases, totalCases).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return string.Join('\n',
            "{",
            $"  \"runId\": \"{runId}\",",
            "  \"evaluatedAt\": \"2026-11-12T18:00:00+00:00\",",
            $"  \"totalCases\": {totalCases.ToString(System.Globalization.CultureInfo.InvariantCulture)},",
            $"  \"failedCases\": {failedCases.ToString(System.Globalization.CultureInfo.InvariantCulture)},",
            $"  \"failureRate\": {rate},",
            "  \"categoryCounts\": {",
            "    \"UngroundedClaim\": 0,",
            "    \"MissingExpectedFact\": 0,",
            "    \"EmptyAnswer\": 0,",
            $"    \"Other\": {failedCases.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "  }",
            "}");
    }

    private static EvaluationSnapshot Snapshot(
        string runId,
        int totalCases,
        int failedCases,
        int ungrounded = 0,
        int missing = 0,
        int empty = 0,
        int other = 0,
        IReadOnlyList<string>? caseIds = null)
    {
        return new EvaluationSnapshot(
            runId,
            EvaluatedAt,
            totalCases,
            failedCases,
            Rate(failedCases, totalCases),
            new Dictionary<EvaluationFailureCategory, int>
            {
                [EvaluationFailureCategory.UngroundedClaim] = ungrounded,
                [EvaluationFailureCategory.MissingExpectedFact] = missing,
                [EvaluationFailureCategory.EmptyAnswer] = empty,
                [EvaluationFailureCategory.Other] = other
            },
            caseIds);
    }

    private static double Rate(int failedCases, int totalCases)
    {
        if (totalCases == 0)
        {
            return 0d;
        }

        return (double)failedCases / totalCases * 100d;
    }

    private sealed class TempFiles : IDisposable
    {
        public TempFiles()
        {
            System.IO.Directory.CreateDirectory(Directory);
        }

        public string Directory { get; } = Path.Combine(
            Path.GetTempPath(),
            "evaluation-quality-report-" + Guid.NewGuid().ToString("N"));

        public string Write(string name, EvaluationSnapshot snapshot) =>
            WriteRaw(name, EvaluationSnapshotJson.Serialize(snapshot));

        public string WriteRaw(string name, string json)
        {
            var path = PathOf(name);
            File.WriteAllText(path, json);
            return path;
        }

        public string PathOf(params string[] parts) => Path.Combine([Directory, .. parts]);

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }
}
