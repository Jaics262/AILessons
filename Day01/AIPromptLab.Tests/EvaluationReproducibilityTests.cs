using System.Text.Json;

namespace AIPromptLab.Tests;

public sealed class EvaluationReproducibilityTests
{
    private static readonly DateTimeOffset RunAt = new(2026, 11, 26, 10, 20, 0, TimeSpan.Zero);

    [Fact]
    public void Identical_bytes_produce_the_same_fingerprint()
    {
        var content = "same evaluation input"u8.ToArray();

        var first = ContentFingerprint.FromBytes(content);
        var second = ContentFingerprint.FromBytes(content.ToArray());

        Assert.Equal(first, second);
        Assert.StartsWith("sha256:", first, StringComparison.Ordinal);
        Assert.Equal(7 + 64, first.Length);
        Assert.Equal(first, ContentFingerprint.FromBytes(content));
    }

    [Fact]
    public void Changed_bytes_produce_a_different_fingerprint()
    {
        var original = ContentFingerprint.FromBytes("dataset"u8);
        var changed = ContentFingerprint.FromBytes("dataset!"u8);
        var empty = ContentFingerprint.FromBytes([]);

        Assert.NotEqual(original, changed);
        Assert.NotEqual(original, empty);
        Assert.Equal(empty, ContentFingerprint.FromBytes([]));
    }

    [Fact]
    public void The_same_file_content_at_different_paths_has_one_fingerprint()
    {
        var content = "baseline-and-candidate"u8.ToArray();
        using var files = new TempFiles();
        var first = files.WriteBytes("one/input.json", content);
        var second = files.WriteBytes("two/copy.json", content);

        Assert.NotEqual(first, second);
        Assert.Equal(ContentFingerprint.FromFile(first), ContentFingerprint.FromFile(second));
        Assert.Equal(ContentFingerprint.FromBytes(content), ContentFingerprint.FromFile(second));
    }

    [Fact]
    public void Dataset_fingerprint_uses_both_snapshot_files_and_ignores_their_paths()
    {
        var baseline = "baseline-bytes"u8.ToArray();
        var candidate = "candidate-bytes"u8.ToArray();

        var fingerprint = EvaluationInputFingerprints.Dataset(baseline, candidate);

        Assert.Equal(fingerprint, EvaluationInputFingerprints.Dataset(baseline.ToArray(), candidate.ToArray()));
        Assert.NotEqual(fingerprint, EvaluationInputFingerprints.Dataset(candidate, baseline));
        Assert.NotEqual(fingerprint, EvaluationInputFingerprints.Dataset(baseline, "candidate-bytes!"u8));
    }

    [Fact]
    public void Report_records_the_files_the_run_read_and_a_utc_timestamp()
    {
        using var files = new TempFiles();
        var baseline = files.Write("baseline.json", SnapshotJson("baseline", 100, 10));
        var candidate = files.Write("candidate.json", SnapshotJson("candidate", 100, 15));
        var policy = files.Write("policy.json", PolicyJson(5, 0, 1));
        var reportPath = files.PathOf("quality.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = EvaluationRegressionGateRunner.Run(
            [
                "regression-gate",
                "--baseline",
                baseline,
                "--candidate",
                candidate,
                "--policy",
                policy,
                "--report",
                reportPath
            ],
            output,
            error,
            RunAt);
        var report = EvaluationQualityReportJson.Deserialize(File.ReadAllText(reportPath));
        var metadata = ReadMetadata(reportPath);

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(100, report.BaselineCaseCount);
        Assert.Equal(100, report.CandidateCaseCount);
        Assert.Equal(10d, report.BaselineFailureRate);
        Assert.Equal(15d, report.CandidateFailureRate);
        Assert.Equal(
            EvaluationInputFingerprints.Dataset(File.ReadAllBytes(baseline), File.ReadAllBytes(candidate)),
            report.Metadata!.DatasetFingerprint);
        Assert.Equal(ContentFingerprint.FromFile(policy), report.Metadata.PolicyFingerprint);
        Assert.Equal(RunAt, report.Metadata.RunTimestampUtc);
        Assert.Equal("2026-11-26T10:20:00Z", metadata.GetProperty("runTimestampUtc").GetString());
        Assert.Equal("sha256:", report.Metadata.DatasetFingerprint![..7]);
        Assert.DoesNotContain(baseline, report.Metadata.DatasetFingerprint, StringComparison.Ordinal);
        Assert.DoesNotContain(policy, report.Metadata.PolicyFingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void A_changed_input_file_changes_only_its_fingerprint()
    {
        using var files = new TempFiles();
        var baseline = files.Write("baseline.json", SnapshotJson("baseline", 100, 10));
        var candidate = files.Write("candidate.json", SnapshotJson("candidate", 100, 15));
        var policy = files.Write("policy.json", PolicyJson(5, 0, 1));
        var firstPath = files.PathOf("first.json");
        var secondPath = files.PathOf("second.json");
        Run(baseline, candidate, policy, firstPath);

        File.AppendAllText(candidate, " ");
        Run(baseline, candidate, policy, secondPath);
        var first = EvaluationQualityReportJson.Deserialize(File.ReadAllText(firstPath));
        var second = EvaluationQualityReportJson.Deserialize(File.ReadAllText(secondPath));

        Assert.NotEqual(first.Metadata!.DatasetFingerprint, second.Metadata!.DatasetFingerprint);
        Assert.Equal(first.Metadata.PolicyFingerprint, second.Metadata.PolicyFingerprint);
        Assert.Equal(
            EvaluationInputFingerprints.Dataset(File.ReadAllBytes(baseline), File.ReadAllBytes(candidate)),
            second.Metadata.DatasetFingerprint);
    }

    [Fact]
    public void The_same_snapshot_bytes_at_another_path_keep_the_dataset_fingerprint()
    {
        using var files = new TempFiles();
        var baselineBytes = SnapshotJson("baseline", 100, 10);
        var candidateBytes = SnapshotJson("candidate", 100, 15);
        var policy = files.Write("policy.json", PolicyJson(5, 0, 1));
        var firstReport = files.PathOf("first.json");
        var secondReport = files.PathOf("second.json");
        Run(files.Write("a/baseline.json", baselineBytes), files.Write("a/candidate.json", candidateBytes), policy, firstReport);
        Run(files.Write("b/baseline.json", baselineBytes), files.Write("b/candidate.json", candidateBytes), policy, secondReport);
        var first = EvaluationQualityReportJson.Deserialize(File.ReadAllText(firstReport));
        var second = EvaluationQualityReportJson.Deserialize(File.ReadAllText(secondReport));

        Assert.Equal(first.Metadata!.DatasetFingerprint, second.Metadata!.DatasetFingerprint);
        Assert.Equal(first.Metadata.PolicyFingerprint, second.Metadata.PolicyFingerprint);
    }

    [Fact]
    public void A_changed_policy_file_changes_the_policy_fingerprint()
    {
        using var files = new TempFiles();
        var baseline = files.Write("baseline.json", SnapshotJson("baseline", 100, 10));
        var candidate = files.Write("candidate.json", SnapshotJson("candidate", 100, 15));
        var policy = files.Write("policy.json", PolicyJson(5, 0, 1));
        var firstPath = files.PathOf("first.json");
        var secondPath = files.PathOf("second.json");
        Run(baseline, candidate, policy, firstPath);
        files.Write("policy.json", PolicyJson(6, 0, 1));
        Run(baseline, candidate, policy, secondPath);
        var first = EvaluationQualityReportJson.Deserialize(File.ReadAllText(firstPath));
        var second = EvaluationQualityReportJson.Deserialize(File.ReadAllText(secondPath));

        Assert.Equal(first.Metadata!.DatasetFingerprint, second.Metadata!.DatasetFingerprint);
        Assert.NotEqual(first.Metadata.PolicyFingerprint, second.Metadata.PolicyFingerprint);
        Assert.Equal(ContentFingerprint.FromFile(policy), second.Metadata.PolicyFingerprint);
        Assert.NotEqual(
            ContentFingerprint.FromBytes(EvaluationRegressionPolicyJson.CanonicalBytes(new EvaluationRegressionPolicy(6d, 0, 1))),
            second.Metadata.PolicyFingerprint);
    }

    [Fact]
    public void An_omitted_policy_file_fingerprints_the_default_policy_content()
    {
        using var files = new TempFiles();
        var reportPath = files.PathOf("quality.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = EvaluationRegressionGateRunner.Run(
            [
                "regression-gate",
                "--baseline",
                files.Write("baseline.json", SnapshotJson("baseline", 100, 10)),
                "--candidate",
                files.Write("candidate.json", SnapshotJson("candidate", 100, 15)),
                "--report",
                reportPath
            ],
            output,
            error,
            RunAt);
        var report = EvaluationQualityReportJson.Deserialize(File.ReadAllText(reportPath));

        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, code);
        Assert.Equal(
            ContentFingerprint.FromBytes(EvaluationRegressionPolicyJson.CanonicalBytes(EvaluationRegressionPolicy.Default)),
            report.Metadata!.PolicyFingerprint);
        Assert.Equal(
            ContentFingerprint.FromBytes(EvaluationRegressionPolicyJson.CanonicalBytes(EvaluationRegressionPolicy.Default)),
            ContentFingerprint.FromBytes(EvaluationRegressionPolicyJson.CanonicalBytes(EvaluationRegressionPolicy.Default)));
    }

    [Fact]
    public void A_non_utc_run_time_is_stored_as_utc()
    {
        using var files = new TempFiles();
        var reportPath = files.PathOf("quality.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        EvaluationRegressionGateRunner.Run(
            [
                "regression-gate",
                "--baseline",
                files.Write("baseline.json", SnapshotJson("baseline", 2, 0)),
                "--candidate",
                files.Write("candidate.json", SnapshotJson("candidate", 2, 0)),
                "--report",
                reportPath
            ],
            output,
            error,
            new DateTimeOffset(2026, 11, 26, 11, 20, 0, TimeSpan.FromHours(1)));

        Assert.Equal("2026-11-26T10:20:00Z", ReadMetadata(reportPath).GetProperty("runTimestampUtc").GetString());
        Assert.Equal(TimeSpan.Zero, EvaluationQualityReportJson.Deserialize(File.ReadAllText(reportPath)).Metadata!.RunTimestampUtc.Offset);
    }

    [Fact]
    public void Metadata_does_not_change_the_markdown_summary_or_exit_code()
    {
        using var files = new TempFiles();
        var reportPath = files.PathOf("quality.json");
        var summaryPath = files.PathOf("quality-summary.md");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = EvaluationRegressionGateRunner.Run(
            [
                "regression-gate",
                "--baseline",
                files.Write("baseline.json", SnapshotJson("baseline", 100, 10)),
                "--candidate",
                files.Write("candidate.json", SnapshotJson("candidate", 100, 20)),
                "--report",
                reportPath
            ],
            output,
            error,
            RunAt);
        var report = EvaluationQualityReportJson.Deserialize(File.ReadAllText(reportPath));
        using var summaryOutput = new StringWriter();
        using var summaryError = new StringWriter();

        var summaryCode = EvaluationQualitySummaryRunner.Run(reportPath, summaryPath, summaryOutput, summaryError);

        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, code);
        Assert.Equal(EvaluationQualitySummaryRunner.WrittenExitCode, summaryCode);
        Assert.Equal(string.Empty, summaryError.ToString());
        Assert.Equal(EvaluationQualitySummary.Render(report.WithoutMetadata()), File.ReadAllText(summaryPath));
        Assert.Equal(EvaluationQualitySummary.Render(report), File.ReadAllText(summaryPath));
        Assert.DoesNotContain("sha256:", File.ReadAllText(summaryPath), StringComparison.Ordinal);
        Assert.DoesNotContain("2026-11-26", File.ReadAllText(summaryPath), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_snapshot_leaves_the_dataset_fingerprint_empty()
    {
        using var files = new TempFiles();
        var reportPath = files.PathOf("quality.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = EvaluationRegressionGateRunner.Run(
            [
                "regression-gate",
                "--baseline",
                files.PathOf("missing.json"),
                "--candidate",
                files.Write("candidate.json", SnapshotJson("candidate", 100, 15)),
                "--report",
                reportPath
            ],
            output,
            error,
            RunAt);
        var report = EvaluationQualityReportJson.Deserialize(File.ReadAllText(reportPath));

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Null(report.BaselineCaseCount);
        Assert.Equal(100, report.CandidateCaseCount);
        Assert.Null(report.Metadata!.DatasetFingerprint);
        Assert.Equal(
            ContentFingerprint.FromBytes(EvaluationRegressionPolicyJson.CanonicalBytes(EvaluationRegressionPolicy.Default)),
            report.Metadata.PolicyFingerprint);
        Assert.Equal("2026-11-26T10:20:00Z", report.Metadata.RunTimestampUtcText);
    }

    private static int Run(string baseline, string candidate, string policy, string report)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        return EvaluationRegressionGateRunner.Run(
            ["regression-gate", "--baseline", baseline, "--candidate", candidate, "--policy", policy, "--report", report],
            output,
            error,
            RunAt);
    }

    private static JsonElement ReadMetadata(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("metadata").Clone();
    }

    private static string SnapshotJson(string runId, int totalCases, int failedCases)
    {
        var rate = totalCases == 0 ? 0d : failedCases * 100d / totalCases;
        return string.Join('\n',
            "{",
            $"  \"runId\": \"{runId}\",",
            "  \"evaluatedAt\": \"2026-11-12T18:00:00+00:00\",",
            $"  \"totalCases\": {totalCases.ToString(System.Globalization.CultureInfo.InvariantCulture)},",
            $"  \"failedCases\": {failedCases.ToString(System.Globalization.CultureInfo.InvariantCulture)},",
            $"  \"failureRate\": {rate.ToString(System.Globalization.CultureInfo.InvariantCulture)},",
            "  \"categoryCounts\": {",
            "    \"UngroundedClaim\": 0,",
            "    \"MissingExpectedFact\": 0,",
            "    \"EmptyAnswer\": 0,",
            $"    \"Other\": {failedCases.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "  }",
            "}");
    }

    private static string PolicyJson(double failureRate, int ungrounded, int missing) =>
        string.Join('\n',
            "{",
            $"  \"maxFailureRateRegressionPercentagePoints\": {failureRate.ToString(System.Globalization.CultureInfo.InvariantCulture)},",
            $"  \"maxAdditionalUngroundedClaimFailures\": {ungrounded.ToString(System.Globalization.CultureInfo.InvariantCulture)},",
            $"  \"maxAdditionalMissingExpectedFactFailures\": {missing.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "}");

    private sealed class TempFiles : IDisposable
    {
        public TempFiles()
        {
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "evaluation-reproducibility-" + Guid.NewGuid().ToString("N"));

        public string Write(string name, string text) => WriteBytes(name, System.Text.Encoding.UTF8.GetBytes(text));

        public string WriteBytes(string name, byte[] content)
        {
            var path = PathOf(name);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, content);
            return path;
        }

        public string PathOf(string name) => Path.Combine(DirectoryPath, name);

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
