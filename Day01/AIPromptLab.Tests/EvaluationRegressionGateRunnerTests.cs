namespace AIPromptLab.Tests;

public sealed class EvaluationRegressionGateRunnerTests
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

    private const string PassingJson = """
        {
          "runId": "passing",
          "evaluatedAt": "2026-11-12T18:00:00+00:00",
          "totalCases": 100,
          "failedCases": 15,
          "failureRate": 15,
          "categoryCounts": {
            "UngroundedClaim": 0,
            "MissingExpectedFact": 0,
            "EmptyAnswer": 0,
            "Other": 15
          }
        }
        """;

    private const string FailingJson = """
        {
          "runId": "failing",
          "evaluatedAt": "2026-11-12T18:00:00+00:00",
          "totalCases": 100,
          "failedCases": 16,
          "failureRate": 16,
          "categoryCounts": {
            "ungroundedClaim": 0,
            "missingExpectedFact": 0,
            "emptyAnswer": 0,
            "other": 16
          }
        }
        """;

    [Fact]
    public void Exit_codes_match_the_pipeline_contract()
    {
        Assert.Equal(0, EvaluationRegressionGateRunner.PassedExitCode);
        Assert.Equal(1, EvaluationRegressionGateRunner.FailedExitCode);
        Assert.Equal(2, EvaluationRegressionGateRunner.InvalidSnapshotExitCode);
    }

    [Fact]
    public void Run_prints_the_gate_report_and_returns_zero_when_the_gate_passes()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("passing", 100, 15, other: 15);
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", BaselineJson);
        var candidatePath = files.WriteRaw("passing.json", PassingJson);

        var (code, output, error) = Execute(baselinePath, candidatePath);
        var expected = EvaluationRegressionGate.Evaluate(
            baseline,
            candidate,
            EvaluationRegressionPolicy.Default);

        Assert.True(expected.Passed);
        AssertReport(expected, code, output, error);
    }

    [Fact]
    public void Run_prints_the_gate_report_and_returns_one_when_the_gate_fails()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("failing", 100, 16, other: 16);
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", BaselineJson);
        var candidatePath = files.WriteRaw("failing.json", FailingJson);

        var (code, output, error) = Execute(baselinePath, candidatePath);
        var expected = EvaluationRegressionGate.Evaluate(
            baseline,
            candidate,
            EvaluationRegressionPolicy.Default);

        Assert.False(expected.Passed);
        Assert.Equal(6d, expected.Comparison!.FailureRateChange);
        AssertReport(expected, code, output, error);
        AssertContains(expected.FailureReasons, output);
    }

    [Fact]
    public void Run_sends_the_given_policy_to_the_gate()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("failing", 100, 16, other: 16);
        var policy = EvaluationRegressionPolicy.Default with { MaximumFailureRateRegression = 6d };
        using var files = new TempSnapshots();

        var (code, output, error) = Execute(
            files.WriteRaw("baseline.json", BaselineJson),
            files.WriteRaw("failing.json", FailingJson),
            policy);
        var expected = EvaluationRegressionGate.Evaluate(baseline, candidate, policy);

        Assert.True(expected.Passed);
        AssertReport(expected, code, output, error);
    }

    [Fact]
    public void Run_returns_one_when_the_saved_cases_differ()
    {
        const string baselineJson = """
            {
              "runId": "baseline",
              "evaluatedAt": "2026-11-12T18:00:00+00:00",
              "totalCases": 2,
              "failedCases": 0,
              "failureRate": 0,
              "categoryCounts": {
                "UngroundedClaim": 0,
                "MissingExpectedFact": 0,
                "EmptyAnswer": 0,
                "Other": 0
              },
              "caseIds": ["alpha", "beta"]
            }
            """;
        const string candidateJson = """
            {
              "RunId": "candidate",
              "EvaluatedAt": "2026-11-12T18:00:00+00:00",
              "TotalCases": 2,
              "FailedCases": 0,
              "FailureRate": 0,
              "CategoryCounts": {
                "UngroundedClaim": 0,
                "MissingExpectedFact": 0,
                "EmptyAnswer": 0,
                "Other": 0
              },
              "CaseIds": ["alpha", "gamma"]
            }
            """;
        var baseline = Snapshot("baseline", 2, 0, caseIds: ["alpha", "beta"]);
        var candidate = Snapshot("candidate", 2, 0, caseIds: ["alpha", "gamma"]);
        using var files = new TempSnapshots();

        var (code, output, error) = Execute(
            files.WriteRaw("baseline.json", baselineJson),
            files.WriteRaw("candidate.json", candidateJson));
        var expected = EvaluationRegressionGate.Evaluate(
            baseline,
            candidate,
            EvaluationRegressionPolicy.Default);

        Assert.False(expected.Passed);
        Assert.Contains("evaluation cases changed", expected.Report, StringComparison.Ordinal);
        AssertReport(expected, code, output, error);
        AssertContains(expected.FailureReasons, output);
    }

    [Fact]
    public void Run_prints_every_gate_failure_reason()
    {
        var baseline = Snapshot("baseline", 100, 10, ungrounded: 1, missing: 1, other: 8);
        var candidate = Snapshot("candidate", 100, 20, ungrounded: 3, missing: 4, other: 13);
        var expected = EvaluationRegressionGate.Evaluate(
            baseline,
            candidate,
            EvaluationRegressionPolicy.Default);
        using var files = new TempSnapshots();

        var (code, output, error) = Execute(
            files.Write("baseline.json", baseline),
            files.Write("candidate.json", candidate));

        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, code);
        Assert.Equal(string.Empty, error);
        Assert.False(expected.Passed);
        Assert.Equal(10d, expected.Comparison!.FailureRateChange);
        Assert.Equal(2, expected.Comparison.CategoryCountChanges[EvaluationFailureCategory.UngroundedClaim]);
        Assert.Equal(3, expected.Comparison.CategoryCountChanges[EvaluationFailureCategory.MissingExpectedFact]);
        Assert.Equal(3, expected.FailureReasons.Count);
        AssertContains(expected.FailureReasons, output);
    }

    [Fact]
    public void Run_repeats_the_same_report_for_identical_inputs()
    {
        var baseline = Snapshot("baseline", 100, 10, ungrounded: 1, missing: 1, other: 8);
        var candidate = Snapshot("candidate", 100, 20, ungrounded: 3, missing: 4, other: 13);
        var expected = EvaluationRegressionGate.Evaluate(
            baseline,
            candidate,
            EvaluationRegressionPolicy.Default);
        using var files = new TempSnapshots();
        var baselinePath = files.Write("baseline.json", baseline);
        var candidatePath = files.Write("candidate.json", candidate);

        var first = Execute(baselinePath, candidatePath);
        var second = Execute(baselinePath, candidatePath);

        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, first.ExitCode);
        Assert.Equal(first.ExitCode, second.ExitCode);
        Assert.Equal(first.Output, second.Output);
        Assert.Equal(first.Error, second.Error);
        Assert.Equal(expected.Report + Environment.NewLine, first.Output);
        Assert.Equal(expected.Report + Environment.NewLine, second.Output);
    }

    [Fact]
    public void Run_returns_two_when_a_snapshot_file_is_missing()
    {
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", BaselineJson);
        var missingBaseline = files.PathOf("missing-baseline.json");
        var missingCandidate = files.PathOf("missing-candidate.json");

        var missingBaselineResult = Execute(missingBaseline, baselinePath);
        var missingCandidateResult = Execute(baselinePath, missingCandidate);
        var missingBoth = Execute(missingBaseline, missingCandidate);
        var blank = Execute("   ", baselinePath);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, missingBaselineResult.ExitCode);
        Assert.Equal(string.Empty, missingBaselineResult.Output);
        Assert.Equal(
            $"The baseline snapshot file '{missingBaseline}' was not found.{Environment.NewLine}",
            missingBaselineResult.Error);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, missingCandidateResult.ExitCode);
        Assert.Equal(string.Empty, missingCandidateResult.Output);
        Assert.Contains($"'{missingCandidate}' was not found.", missingCandidateResult.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Regression gate:", missingCandidateResult.Error, StringComparison.Ordinal);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, missingBoth.ExitCode);
        Assert.Equal(string.Empty, missingBoth.Output);
        Assert.Contains("baseline snapshot file", missingBoth.Error, StringComparison.Ordinal);
        Assert.Contains("candidate snapshot file", missingBoth.Error, StringComparison.Ordinal);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, blank.ExitCode);
        Assert.Equal(string.Empty, blank.Output);
        Assert.Equal(
            $"The baseline snapshot file was not provided.{Environment.NewLine}",
            blank.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    public void Run_returns_two_when_the_snapshot_text_is_not_an_object(string json)
    {
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", json);
        var candidatePath = files.WriteRaw("candidate.json", BaselineJson);

        var (code, output, error) = Execute(baselinePath, candidatePath);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains($"'{baselinePath}' is not valid.", error, StringComparison.Ordinal);
        Assert.DoesNotContain("Regression gate:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_returns_two_when_the_candidate_json_is_malformed()
    {
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", BaselineJson);
        var candidatePath = files.WriteRaw("candidate.json", """{ "runId": """);

        var (code, output, error) = Execute(baselinePath, candidatePath);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains(
            $"The candidate snapshot file '{candidatePath}' is not valid.",
            error,
            StringComparison.Ordinal);
        Assert.DoesNotContain("baseline snapshot file", error, StringComparison.Ordinal);
        Assert.True(File.Exists(baselinePath));
    }

    [Fact]
    public void Run_returns_two_when_the_snapshot_breaks_its_own_rules()
    {
        const string json = """
            {
              "runId": "baseline",
              "evaluatedAt": "2026-11-12T18:00:00+00:00",
              "totalCases": 1,
              "failedCases": 1,
              "failureRate": 0,
              "categoryCounts": {
                "UngroundedClaim": 1,
                "MissingExpectedFact": 0,
                "EmptyAnswer": 0,
                "Other": 0
              }
            }
            """;
        using var files = new TempSnapshots();
        var path = files.WriteRaw("baseline.json", json);

        var (code, output, error) = Execute(path, files.WriteRaw("candidate.json", BaselineJson));

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("Failure rate must be", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_returns_two_when_the_snapshot_has_an_unrecognized_property()
    {
        const string json = """
            {
              "runId": "baseline",
              "evaluatedAt": "2026-11-12T18:00:00+00:00",
              "totalCases": 1,
              "failedCases": 0,
              "failureRate": 0,
              "notes": true,
              "categoryCounts": {
                "UngroundedClaim": 0,
                "MissingExpectedFact": 0,
                "EmptyAnswer": 0,
                "Other": 0
              }
            }
            """;
        using var files = new TempSnapshots();

        var (code, output, error) = Execute(
            files.WriteRaw("baseline.json", json),
            files.WriteRaw("candidate.json", PassingJson));

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("The evaluation snapshot property 'notes' is not recognized.", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_returns_two_when_a_snapshot_file_cannot_be_read()
    {
        using var files = new TempSnapshots();
        var directory = files.PathOf("baseline-directory");
        Directory.CreateDirectory(directory);

        var (code, output, error) = Execute(directory, files.WriteRaw("candidate.json", PassingJson));

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("could not be read", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_returns_two_for_both_files_when_neither_can_be_evaluated()
    {
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", "{");
        var candidatePath = files.PathOf("missing.json");

        var (code, output, error) = Execute(baselinePath, candidatePath);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains($"The baseline snapshot file '{baselinePath}' is not valid.", error, StringComparison.Ordinal);
        Assert.Contains($"The candidate snapshot file '{candidatePath}' was not found.", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Command_line_reads_both_snapshot_files()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("passing", 100, 15, other: 15);
        var expected = EvaluationRegressionGate.Evaluate(
            baseline,
            candidate,
            EvaluationRegressionPolicy.Default);
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", BaselineJson);
        var candidatePath = files.WriteRaw("passing.json", PassingJson);

        var spaced = ExecuteCommand(
            "regression-gate",
            "--candidate",
            candidatePath,
            "--baseline",
            baselinePath);
        var equals = ExecuteCommand(
            "Regression-Gate",
            "--baseline=" + baselinePath,
            "--candidate=" + candidatePath);

        Assert.True(expected.Passed);
        AssertReport(expected, spaced.ExitCode, spaced.Output, spaced.Error);
        AssertReport(expected, equals.ExitCode, equals.Output, equals.Error);
    }

    [Fact]
    public void Command_line_returns_two_when_a_snapshot_path_is_missing()
    {
        var (code, output, error) = ExecuteCommand("regression-gate", "--baseline", "baseline.json");

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Equal(EvaluationRegressionGateRunner.Usage + Environment.NewLine, error);
    }

    [Fact]
    public void Command_line_returns_two_for_an_unknown_or_repeated_option()
    {
        var unknown = ExecuteCommand(
            "regression-gate",
            "--baseline",
            "baseline.json",
            "--candidate",
            "candidate.json",
            "--threshold",
            "loose");
        var repeated = ExecuteCommand(
            "regression-gate",
            "--baseline",
            "one.json",
            "--baseline",
            "two.json",
            "--candidate",
            "candidate.json");
        var omitted = ExecuteCommand("regression-gate", "--candidate");

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, unknown.ExitCode);
        Assert.Contains("Unknown argument '--threshold'.", unknown.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, repeated.ExitCode);
        Assert.Contains("more than once", repeated.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, omitted.ExitCode);
        Assert.Contains("The --candidate file was not provided.", omitted.Error, StringComparison.Ordinal);
        Assert.Equal(string.Empty, unknown.Output);
        Assert.Equal(string.Empty, repeated.Output);
        Assert.Equal(string.Empty, omitted.Output);
    }

    [Fact]
    public void Command_line_loads_the_policy_file_and_sends_it_to_the_gate()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("failing", 100, 16, other: 16);
        var policy = new EvaluationRegressionPolicy(6d, 0, 1);
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", BaselineJson);
        var candidatePath = files.WriteRaw("failing.json", FailingJson);
        var policyPath = files.WriteRaw("policy.json", PolicyJson(6, 0, 1));

        var spaced = ExecuteCommand(
            "regression-gate",
            "--candidate",
            candidatePath,
            "--policy",
            policyPath,
            "--baseline",
            baselinePath);
        var equals = ExecuteCommand(
            "Regression-Gate",
            "--baseline=" + baselinePath,
            "--policy=" + policyPath,
            "--candidate=" + candidatePath);
        var expected = EvaluationRegressionGate.Evaluate(baseline, candidate, policy);
        var blocked = EvaluationRegressionGate.Evaluate(
            baseline,
            candidate,
            EvaluationRegressionPolicy.Default);

        Assert.True(expected.Passed);
        Assert.False(blocked.Passed);
        AssertReport(expected, spaced.ExitCode, spaced.Output, spaced.Error);
        AssertReport(expected, equals.ExitCode, equals.Output, equals.Error);
    }

    [Fact]
    public void Command_line_keeps_the_default_policy_when_the_policy_file_is_omitted()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("failing", 100, 16, other: 16);
        using var files = new TempSnapshots();

        var (code, output, error) = ExecuteCommand(
            "regression-gate",
            "--baseline",
            files.WriteRaw("baseline.json", BaselineJson),
            "--candidate",
            files.WriteRaw("failing.json", FailingJson));
        var expected = EvaluationRegressionGate.Evaluate(
            baseline,
            candidate,
            EvaluationRegressionPolicy.Default);

        Assert.False(expected.Passed);
        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, code);
        AssertReport(expected, code, output, error);
    }

    [Fact]
    public void Command_line_returns_two_when_the_policy_file_is_missing_or_invalid()
    {
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", BaselineJson);
        var candidatePath = files.WriteRaw("passing.json", PassingJson);
        var missing = files.PathOf("missing-policy.json");
        var invalid = files.WriteRaw("invalid.json", "{");
        var negative = files.WriteRaw("negative.json", PolicyJson(-1, 0, 1));
        var fractional = files.WriteRaw(
            "fractional.json",
            """
            {
              "maxFailureRateRegressionPercentagePoints": 5,
              "maxAdditionalUngroundedClaimFailures": 1.5,
              "maxAdditionalMissingExpectedFactFailures": 1
            }
            """);
        var incomplete = files.WriteRaw(
            "incomplete.json",
            """
            {
              "maxFailureRateRegressionPercentagePoints": 5
            }
            """);
        var unknownField = files.WriteRaw(
            "unknown.json",
            """
            {
              "maxFailureRateRegressionPercentagePoints": 5,
              "maxAdditionalUngroundedClaimFailures": 0,
              "maxAdditionalMissingExpectedFactFailures": 1,
              "extra": true
            }
            """);

        var missingResult = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--policy",
            missing);
        var invalidResult = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--policy",
            invalid);
        var negativeResult = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--policy",
            negative);
        var fractionalResult = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--policy",
            fractional);
        var incompleteResult = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--policy",
            incomplete);
        var unknownResult = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--policy",
            unknownField);

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, missingResult.ExitCode);
        Assert.Contains($"The policy file '{missing}' was not found.", missingResult.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, invalidResult.ExitCode);
        Assert.Contains($"The policy file '{invalid}' is not valid.", invalidResult.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, negativeResult.ExitCode);
        Assert.Contains(
            "'maxFailureRateRegressionPercentagePoints' must be a non-negative number.",
            negativeResult.Error,
            StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, fractionalResult.ExitCode);
        Assert.Contains(
            "'maxAdditionalUngroundedClaimFailures' must be an integer.",
            fractionalResult.Error,
            StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, incompleteResult.ExitCode);
        Assert.Contains(
            "The quality gate policy is missing 'maxAdditionalUngroundedClaimFailures'.",
            incompleteResult.Error,
            StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, unknownResult.ExitCode);
        Assert.Contains(
            "The quality gate policy property 'extra' is not recognized.",
            unknownResult.Error,
            StringComparison.Ordinal);
        Assert.Equal(string.Empty, missingResult.Output);
        Assert.Equal(string.Empty, invalidResult.Output);
        Assert.Equal(string.Empty, negativeResult.Output);
        Assert.Equal(string.Empty, fractionalResult.Output);
        Assert.Equal(string.Empty, incompleteResult.Output);
        Assert.Equal(string.Empty, unknownResult.Output);
        Assert.DoesNotContain("Regression gate:", missingResult.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Command_line_returns_two_for_a_missing_or_repeated_policy_path()
    {
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", BaselineJson);
        var candidatePath = files.WriteRaw("passing.json", PassingJson);
        var omitted = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--policy");
        var blank = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--policy=");
        var repeated = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--policy",
            "one.json",
            "--candidate",
            candidatePath,
            "--policy",
            "two.json");

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, omitted.ExitCode);
        Assert.Contains("The --policy file was not provided.", omitted.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, blank.ExitCode);
        Assert.Contains("The --policy file was not provided.", blank.Error, StringComparison.Ordinal);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, repeated.ExitCode);
        Assert.Contains("The --policy option was provided more than once.", repeated.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("was not found", omitted.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("was not found", blank.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("was not found", repeated.Error, StringComparison.Ordinal);
        Assert.Equal(string.Empty, omitted.Output);
        Assert.Equal(string.Empty, blank.Output);
        Assert.Equal(string.Empty, repeated.Output);
    }

    [Fact]
    public void Command_line_writes_the_report_for_an_invalid_policy_without_running_the_gate()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("passing", 100, 15, other: 15);
        using var files = new TempSnapshots();
        var baselinePath = files.WriteRaw("baseline.json", BaselineJson);
        var candidatePath = files.WriteRaw("passing.json", PassingJson);
        var policyPath = files.WriteRaw("policy.json", PolicyJson(-1, 0, 1));
        var reportPath = files.PathOf("quality.json");

        var (code, output, error) = ExecuteCommand(
            "regression-gate",
            "--baseline",
            baselinePath,
            "--candidate",
            candidatePath,
            "--policy",
            policyPath,
            "--report",
            reportPath);
        var report = EvaluationQualityReportJson.Deserialize(File.ReadAllText(reportPath));

        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("must be a non-negative number.", error, StringComparison.Ordinal);
        Assert.Equal(EvaluationQualityReport.FailedStatus, report.Status);
        Assert.Equal(baseline.TotalCases, report.BaselineCaseCount);
        Assert.Equal(baseline.FailureRate, report.BaselineFailureRate);
        Assert.Equal(candidate.TotalCases, report.CandidateCaseCount);
        Assert.Equal(candidate.FailureRate, report.CandidateFailureRate);
        Assert.Null(report.FailureRateChangePercentagePoints);
        Assert.Equal(EvaluationRegressionGateRunner.InvalidSnapshotExitCode, report.ExitCode);
        Assert.Contains("must be a non-negative number.", report.FailureReasons[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Command_line_writes_the_gate_report_when_the_policy_file_fails_the_gate()
    {
        var baseline = Snapshot("baseline", 100, 10, other: 10);
        var candidate = Snapshot("passing", 100, 15, other: 15);
        var policy = new EvaluationRegressionPolicy(4d, 0, 1);
        using var files = new TempSnapshots();
        var reportPath = files.PathOf("quality.json");

        var (code, output, error) = ExecuteCommand(
            "regression-gate",
            "--baseline",
            files.WriteRaw("baseline.json", BaselineJson),
            "--candidate",
            files.WriteRaw("passing.json", PassingJson),
            "--policy",
            files.WriteRaw("policy.json", PolicyJson(4, 0, 1)),
            "--report",
            reportPath);
        var expected = EvaluationRegressionGate.Evaluate(baseline, candidate, policy);
        var report = EvaluationQualityReportJson.Deserialize(File.ReadAllText(reportPath));
        var expectedReport = EvaluationQualityReport.From(expected);

        Assert.False(expected.Passed);
        Assert.Equal(EvaluationRegressionGateRunner.FailedExitCode, code);
        Assert.Equal(expected.Report + Environment.NewLine, output);
        Assert.Equal(string.Empty, error);
        Assert.Equal(expectedReport.Status, report.Status);
        Assert.Equal(expectedReport.FailureRateChangePercentagePoints, report.FailureRateChangePercentagePoints);
        Assert.Equal(expectedReport.FailureReasons, report.FailureReasons);
        Assert.Equal(expectedReport.ExitCode, report.ExitCode);
    }

    [Fact]
    public void IsInvocation_recognizes_only_the_regression_gate_command()
    {
        Assert.False(EvaluationRegressionGateRunner.IsInvocation(null));
        Assert.False(EvaluationRegressionGateRunner.IsInvocation([]));
        Assert.False(EvaluationRegressionGateRunner.IsInvocation(["--baseline", "a.json"]));
        Assert.True(EvaluationRegressionGateRunner.IsInvocation(["regression-gate"]));
        Assert.True(EvaluationRegressionGateRunner.IsInvocation(["Regression-Gate", "--baseline", "a.json"]));
    }

    [Fact]
    public void Json_preserves_the_snapshot_fields()
    {
        var withCases = new EvaluationSnapshot(
            " run-1 ",
            new DateTimeOffset(2026, 11, 12, 18, 30, 0, TimeSpan.FromHours(5.5)),
            6,
            4,
            (double)4 / 6 * 100d,
            Counts(ungrounded: 2, other: 3),
            [" alpha ", "beta", "gamma", "delta", "epsilon", "zeta"]);

        var loaded = EvaluationSnapshotJson.Deserialize(EvaluationSnapshotJson.Serialize(withCases));

        Assert.Equal("run-1", loaded.RunId);
        Assert.Equal(withCases.EvaluatedAt, loaded.EvaluatedAt);
        Assert.Equal(withCases.TotalCases, loaded.TotalCases);
        Assert.Equal(withCases.FailedCases, loaded.FailedCases);
        Assert.Equal(withCases.FailureRate, loaded.FailureRate);
        Assert.Equal(["alpha", "beta", "gamma", "delta", "epsilon", "zeta"], loaded.CaseIds);
        foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
        {
            Assert.Equal(withCases.CategoryCounts[category], loaded.CategoryCounts[category]);
        }

        var withoutCases = Snapshot("plain", 1, 0);
        var json = EvaluationSnapshotJson.Serialize(withoutCases);

        Assert.DoesNotContain("caseIds", json, StringComparison.Ordinal);
        Assert.Null(EvaluationSnapshotJson.Deserialize(json).CaseIds);
    }

    private static string PolicyJson(double failureRate, int ungrounded, int missing) =>
        string.Join('\n',
            "{",
            $"  \"maxFailureRateRegressionPercentagePoints\": {failureRate.ToString(System.Globalization.CultureInfo.InvariantCulture)},",
            $"  \"maxAdditionalUngroundedClaimFailures\": {ungrounded.ToString(System.Globalization.CultureInfo.InvariantCulture)},",
            $"  \"maxAdditionalMissingExpectedFactFailures\": {missing.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "}");

    private static (int ExitCode, string Output, string Error) Execute(
        string? baselinePath,
        string? candidatePath,
        EvaluationRegressionPolicy? policy = null)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = EvaluationRegressionGateRunner.Run(baselinePath, candidatePath, output, error, policy);
        return (code, output.ToString(), error.ToString());
    }

    private static (int ExitCode, string Output, string Error) ExecuteCommand(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = EvaluationRegressionGateRunner.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static void AssertReport(EvaluationRegressionResult result, int exitCode, string output, string error)
    {
        var expectedCode = result.Passed
            ? EvaluationRegressionGateRunner.PassedExitCode
            : EvaluationRegressionGateRunner.FailedExitCode;
        Assert.Equal(expectedCode, exitCode);
        Assert.Equal(result.Report + Environment.NewLine, output);
        Assert.Equal(string.Empty, error);
    }

    private static void AssertContains(IReadOnlyList<string> reasons, string output)
    {
        Assert.NotEmpty(reasons);
        foreach (var reason in reasons)
        {
            Assert.Contains($"- {reason}", output, StringComparison.Ordinal);
        }
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
            Counts(ungrounded, missing, empty, other),
            caseIds);
    }

    private static Dictionary<EvaluationFailureCategory, int> Counts(
        int ungrounded = 0,
        int missing = 0,
        int empty = 0,
        int other = 0)
    {
        return new Dictionary<EvaluationFailureCategory, int>
        {
            [EvaluationFailureCategory.UngroundedClaim] = ungrounded,
            [EvaluationFailureCategory.MissingExpectedFact] = missing,
            [EvaluationFailureCategory.EmptyAnswer] = empty,
            [EvaluationFailureCategory.Other] = other
        };
    }

    private static double Rate(int failedCases, int totalCases)
    {
        if (totalCases == 0)
        {
            return 0d;
        }

        return (double)failedCases / totalCases * 100d;
    }

    private sealed class TempSnapshots : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            "evaluation-regression-gate-" + Guid.NewGuid().ToString("N"));

        public TempSnapshots()
        {
            Directory.CreateDirectory(_directory);
        }

        public string Write(string name, EvaluationSnapshot snapshot) =>
            WriteRaw(name, EvaluationSnapshotJson.Serialize(snapshot));

        public string WriteRaw(string name, string json)
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
