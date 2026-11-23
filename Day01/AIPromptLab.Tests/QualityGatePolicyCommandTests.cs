using System.Globalization;

namespace AIPromptLab.Tests;

public sealed class QualityGatePolicyCommandTests
{
    private static DateTimeOffset EvaluatedAt { get; } =
        new(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);

    private const string BaselineJson = """
        {
          "runId": "baseline",
          "evaluatedAt": "2026-11-12T18:00:00+00:00",
          "totalCases": 100,
          "failedCases": 10,
          "failureRate": 10,
          "categoryCounts": {
            "UngroundedClaim": 0,
            "MissingExpectedFact": 0,
            "EmptyAnswer": 0,
            "Other": 10
          }
        }
        """;

    private const string CandidateJson = """
        {
          "runId": "candidate",
          "evaluatedAt": "2026-11-12T18:00:00+00:00",
          "totalCases": 100,
          "failedCases": 16,
          "failureRate": 16,
          "categoryCounts": {
            "UngroundedClaim": 0,
            "MissingExpectedFact": 0,
            "EmptyAnswer": 0,
            "Other": 16
          }
        }
        """;

    private const string MalformedPolicyMessage =
        "Expected depth to be zero at the end of the JSON payload. There is an open JSON object or array that should be closed. LineNumber: 0 | BytePositionInLine: 1.";

    public static IEnumerable<object[]> MissingRequiredFields { get; } =
    [
        [
            """
            {
              "maxAdditionalUngroundedClaimFailures": 0,
              "maxAdditionalMissingExpectedFactFailures": 1
            }
            """,
            "maxFailureRateRegressionPercentagePoints"
        ],
        [
            """
            {
              "maxFailureRateRegressionPercentagePoints": 6,
              "maxAdditionalMissingExpectedFactFailures": 1
            }
            """,
            "maxAdditionalUngroundedClaimFailures"
        ],
        [
            """
            {
              "maxFailureRateRegressionPercentagePoints": 6,
              "maxAdditionalUngroundedClaimFailures": 0
            }
            """,
            "maxAdditionalMissingExpectedFactFailures"
        ]
    ];

    public static IEnumerable<object[]> NegativeThresholds { get; } =
    [
        [
            PolicyJson(-1, 0, 1),
            "'maxFailureRateRegressionPercentagePoints' must be a non-negative number."
        ],
        [
            PolicyJson(6, -1, 1),
            "'maxAdditionalUngroundedClaimFailures' must be a non-negative integer."
        ],
        [
            PolicyJson(6, 0, -1),
            "'maxAdditionalMissingExpectedFactFailures' must be a non-negative integer."
        ]
    ];

    [Fact]
    public void Stricter_policy_fails_the_snapshots_a_custom_policy_passes()
    {
        var baseline = Baseline();
        var candidate = Candidate();
        var passingPolicy = new EvaluationRegressionPolicy(6d, 0, 1);
        var stricterPolicy = new EvaluationRegressionPolicy(5d, 0, 1);
        using var files = new TempFiles();
        var baselinePath = files.Write("baseline.json", BaselineJson);
        var candidatePath = files.Write("candidate.json", CandidateJson);
        var passingPath = files.Write("passing-policy.json", PolicyJson(6, 0, 1));
        var stricterPath = files.Write("stricter-policy.json", PolicyJson(5, 0, 1));

        var passing = Run(Gate(baselinePath, candidatePath, passingPath));
        var stricter = Run(Gate(baselinePath, candidatePath, stricterPath));
        var expectedPass = EvaluationRegressionGate.Evaluate(baseline, candidate, passingPolicy);
        var expectedFail = EvaluationRegressionGate.Evaluate(baseline, candidate, stricterPolicy);

        Assert.True(expectedPass.Passed);
        Assert.False(expectedFail.Passed);
        Assert.Equal(6d, expectedPass.Comparison!.FailureRateChange);
        Assert.Equal(expectedPass.Comparison.FailureRateChange, expectedFail.Comparison!.FailureRateChange);
        AssertReport(expectedPass, passing);
        AssertReport(expectedFail, stricter);
        Assert.NotEqual(passing.Output, stricter.Output);
    }

    [Fact]
    public void Missing_policy_file_exits_two_and_does_not_run_the_gate()
    {
        using var files = new TempFiles();
        var missing = files.PathOf("missing-policy.json");

        var (code, output, error) = Run(Gate(
            files.Write("baseline.json", BaselineJson),
            files.Write("candidate.json", CandidateJson),
            missing));

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal($"The policy file '{missing}' was not found.{Environment.NewLine}", error);
    }

    [Fact]
    public void Malformed_policy_json_exits_two_and_does_not_run_the_gate()
    {
        using var files = new TempFiles();
        var policyPath = files.Write("policy.json", "{");

        var (code, output, error) = Run(Gate(
            files.Write("baseline.json", BaselineJson),
            files.Write("candidate.json", CandidateJson),
            policyPath));

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal(
            $"The policy file '{policyPath}' is not valid. {MalformedPolicyMessage}{Environment.NewLine}",
            error);
    }

    [Theory]
    [MemberData(nameof(MissingRequiredFields))]
    public void Missing_required_policy_field_exits_two(string json, string fieldName)
    {
        using var files = new TempFiles();
        var policyPath = files.Write("policy.json", json);

        var (code, output, error) = Run(Gate(
            files.Write("baseline.json", BaselineJson),
            files.Write("candidate.json", CandidateJson),
            policyPath));

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal(
            $"The policy file '{policyPath}' is not valid. The quality gate policy is missing '{fieldName}'.{Environment.NewLine}",
            error);
    }

    [Theory]
    [MemberData(nameof(NegativeThresholds))]
    public void Negative_policy_threshold_exits_two(string json, string message)
    {
        using var files = new TempFiles();
        var policyPath = files.Write("policy.json", json);

        var (code, output, error) = Run(Gate(
            files.Write("baseline.json", BaselineJson),
            files.Write("candidate.json", CandidateJson),
            policyPath));

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal(
            $"The policy file '{policyPath}' is not valid. {message}{Environment.NewLine}",
            error);
    }

    [Fact]
    public void Command_without_policy_uses_the_default_policy()
    {
        var baseline = Baseline();
        var candidate = Candidate();
        using var files = new TempFiles();
        var baselinePath = files.Write("baseline.json", BaselineJson);
        var candidatePath = files.Write("candidate.json", CandidateJson);
        var unusedLoosePolicy = files.Write("loose-policy.json", PolicyJson(6, 0, 1));
        var reportPath = files.PathOf("quality.json");

        var omitted = Run(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--report",
            reportPath);
        var expected = EvaluationRegressionGate.Evaluate(
            baseline,
            candidate,
            EvaluationRegressionPolicy.Default);
        var report = EvaluationQualityReportJson.Deserialize(File.ReadAllText(reportPath));
        var loose = Run(Gate(baselinePath, candidatePath, unusedLoosePolicy));

        Assert.False(expected.Passed);
        Assert.Equal(5d, EvaluationRegressionPolicy.Default.MaximumFailureRateRegression);
        AssertReport(expected, omitted);
        Assert.Equal(EvaluationQualityReportJson.Serialize(EvaluationQualityReport.From(expected)), File.ReadAllText(reportPath));
        Assert.Equal(expected.FailureReasons, report.FailureReasons);
        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, report.ExitCode);
        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, loose.ExitCode);
        Assert.NotEqual(omitted.Output, loose.Output);
        Assert.True(File.Exists(unusedLoosePolicy));
    }

    [Fact]
    public void Repeated_runs_with_the_same_inputs_produce_the_same_result()
    {
        var baseline = Baseline();
        var candidate = Candidate();
        var passingPolicy = new EvaluationRegressionPolicy(6d, 0, 1);
        var stricterPolicy = new EvaluationRegressionPolicy(5d, 0, 1);
        using var files = new TempFiles();
        var baselinePath = files.Write("baseline.json", BaselineJson);
        var candidatePath = files.Write("candidate.json", CandidateJson);
        var passingPath = files.Write("passing-policy.json", PolicyJson(6, 0, 1));
        var stricterPath = files.Write("stricter-policy.json", PolicyJson(5, 0, 1));
        var missing = files.PathOf("missing-policy.json");

        var passing = RunTwice(Gate(baselinePath, candidatePath, passingPath, files.PathOf("pass-a.json")), Gate(
            baselinePath,
            candidatePath,
            passingPath,
            files.PathOf("pass-b.json")));
        var stricter = RunTwice(Gate(baselinePath, candidatePath, stricterPath, files.PathOf("fail-a.json")), Gate(
            baselinePath,
            candidatePath,
            stricterPath,
            files.PathOf("fail-b.json")));
        var omitted = RunTwice(
            Gate(baselinePath, candidatePath, report: files.PathOf("default-a.json")),
            Gate(baselinePath, candidatePath, report: files.PathOf("default-b.json")));
        var invalid = RunTwice(
            Gate(baselinePath, candidatePath, missing),
            Gate(baselinePath, candidatePath, missing));

        AssertSameRun(passing);
        AssertSameRun(stricter);
        AssertSameRun(omitted);
        AssertSameRun(invalid);
        Assert.Equal(EvaluationRegressionGateRunner.PassedExitCode, passing.First.ExitCode);
        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, stricter.First.ExitCode);
        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, omitted.First.ExitCode);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, invalid.First.ExitCode);
        Assert.Equal(
            EvaluationRegressionGate.Evaluate(baseline, candidate, passingPolicy).Report + Environment.NewLine,
            passing.First.Output);
        Assert.Equal(
            EvaluationRegressionGate.Evaluate(baseline, candidate, stricterPolicy).Report + Environment.NewLine,
            stricter.First.Output);
        Assert.Equal(
            EvaluationRegressionGate.Evaluate(baseline, candidate, EvaluationRegressionPolicy.Default).Report + Environment.NewLine,
            omitted.First.Output);
        Assert.Equal(stricter.First.Output, omitted.First.Output);
        Assert.Equal(string.Empty, invalid.First.Output);
        Assert.Equal(File.ReadAllText(files.PathOf("pass-a.json")), File.ReadAllText(files.PathOf("pass-b.json")));
        Assert.Equal(File.ReadAllText(files.PathOf("fail-a.json")), File.ReadAllText(files.PathOf("fail-b.json")));
        Assert.Equal(File.ReadAllText(files.PathOf("default-a.json")), File.ReadAllText(files.PathOf("default-b.json")));
        Assert.Equal(
            EvaluationQualityReportJson.Serialize(EvaluationQualityReport.From(
                EvaluationRegressionGate.Evaluate(baseline, candidate, passingPolicy))),
            File.ReadAllText(files.PathOf("pass-a.json")));
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = EvaluationRegressionGateRunner.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static (CommandResult First, CommandResult Second) RunTwice(string[] firstArgs, string[] secondArgs)
    {
        var first = Run(firstArgs);
        var second = Run(secondArgs);
        return (
            new CommandResult(first.ExitCode, first.Output, first.Error),
            new CommandResult(second.ExitCode, second.Output, second.Error));
    }

    private static void AssertSameRun((CommandResult First, CommandResult Second) run)
    {
        Assert.Equal(run.First.ExitCode, run.Second.ExitCode);
        Assert.Equal(run.First.Output, run.Second.Output);
        Assert.Equal(run.First.Error, run.Second.Error);
    }

    private static void AssertReport(
        EvaluationRegressionResult expected,
        (int ExitCode, string Output, string Error) actual)
    {
        var expectedCode = expected.Passed
            ? EvaluationRegressionGateRunner.PassedExitCode
            : EvaluationRegressionGateRunner.FailedExitCode;
        Assert.Equal(expectedCode, actual.ExitCode);
        Assert.Equal(expected.Report + Environment.NewLine, actual.Output);
        Assert.Equal(string.Empty, actual.Error);
    }

    private static string[] Gate(
        string baseline,
        string candidate,
        string? policy = null,
        string? report = null)
    {
        var args = new List<string>
        {
            "regression-gate",
            "--baseline",
            baseline,
            "--candidate",
            candidate
        };
        if (policy is not null)
        {
            args.Add("--policy");
            args.Add(policy);
        }

        if (report is not null)
        {
            args.Add("--report");
            args.Add(report);
        }

        return [.. args];
    }

    private static string PolicyJson(double failureRate, int ungrounded, int missing) =>
        string.Join('\n',
            "{",
            $"  \"{EvaluationRegressionPolicyJson.MaximumFailureRateRegressionProperty}\": {failureRate.ToString(CultureInfo.InvariantCulture)},",
            $"  \"{EvaluationRegressionPolicyJson.MaximumUngroundedClaimIncreaseProperty}\": {ungrounded.ToString(CultureInfo.InvariantCulture)},",
            $"  \"{EvaluationRegressionPolicyJson.MaximumMissingExpectedFactIncreaseProperty}\": {missing.ToString(CultureInfo.InvariantCulture)}",
            "}");

    private static EvaluationSnapshot Baseline() => Snapshot("baseline", 100, 10, other: 10);

    private static EvaluationSnapshot Candidate() => Snapshot("candidate", 100, 16, other: 16);

    private static EvaluationSnapshot Snapshot(
        string runId,
        int totalCases,
        int failedCases,
        int ungrounded = 0,
        int missing = 0,
        int empty = 0,
        int other = 0)
    {
        return new EvaluationSnapshot(
            runId,
            EvaluatedAt,
            totalCases,
            failedCases,
            failedCases * 100d / totalCases,
            new Dictionary<EvaluationFailureCategory, int>
            {
                [EvaluationFailureCategory.UngroundedClaim] = ungrounded,
                [EvaluationFailureCategory.MissingExpectedFact] = missing,
                [EvaluationFailureCategory.EmptyAnswer] = empty,
                [EvaluationFailureCategory.Other] = other
            });
    }

    private readonly record struct CommandResult(int ExitCode, string Output, string Error);

    private sealed class TempFiles : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            "quality-gate-policy-" + Guid.NewGuid().ToString("N"));

        public TempFiles()
        {
            Directory.CreateDirectory(_directory);
        }

        public string Write(string name, string json)
        {
            var path = Path.Combine(_directory, name);
            File.WriteAllText(path, json);
            return path;
        }

        public string PathOf(string name) => Path.Combine(_directory, name);

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
