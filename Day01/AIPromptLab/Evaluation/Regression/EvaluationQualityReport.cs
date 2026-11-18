// Machine-readable result of one regression-gate command.
// Status is "Passed" or "Failed". ExitCode is the runner's 0, 1, or 2.
// Failure rates are percentages. 10 means 10%, the same unit a snapshot already stores.
// The failure-rate change is the gate comparison's FailureRateChange: candidate minus baseline.
// This report records that result. It does not apply the policy again.
// A count or rate that was not loaded is null. 0 is a real rate, not a stand-in for a missing file.
// There is no timestamp. The same inputs produce the same report.
public sealed class EvaluationQualityReport
{
    public const string PassedStatus = "Passed";

    public const string FailedStatus = "Failed";

    private EvaluationQualityReport(
        string status,
        int? baselineCaseCount,
        int? candidateCaseCount,
        double? baselineFailureRate,
        double? candidateFailureRate,
        double? failureRateChangePercentagePoints,
        IReadOnlyList<string> failureReasons,
        int exitCode)
    {
        Status = RequireStatus(status);
        BaselineCaseCount = RequireCount(baselineCaseCount);
        CandidateCaseCount = RequireCount(candidateCaseCount);
        BaselineFailureRate = RequireRate(baselineFailureRate);
        CandidateFailureRate = RequireRate(candidateFailureRate);
        FailureRateChangePercentagePoints = RequireRate(failureRateChangePercentagePoints);
        FailureReasons = failureReasons;
        ExitCode = RequireExitCode(exitCode, Status, FailureReasons);
    }

    public string Status { get; }

    public int? BaselineCaseCount { get; }

    public int? CandidateCaseCount { get; }

    public double? BaselineFailureRate { get; }

    public double? CandidateFailureRate { get; }

    public double? FailureRateChangePercentagePoints { get; }

    public IReadOnlyList<string> FailureReasons { get; }

    public int ExitCode { get; }

    public static EvaluationQualityReport From(EvaluationRegressionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Comparison is null)
        {
            throw new ArgumentException(
                "A quality report needs the regression-gate comparison.",
                nameof(result));
        }

        var comparison = result.Comparison;
        var exitCode = result.Passed
            ? EvaluationRegressionGateRunner.PassedExitCode
            : EvaluationRegressionGateRunner.FailedExitCode;
        return new EvaluationQualityReport(
            result.Passed ? PassedStatus : FailedStatus,
            comparison.Previous.TotalCases,
            comparison.Latest.TotalCases,
            comparison.Previous.FailureRate,
            comparison.Latest.FailureRate,
            comparison.FailureRateChange,
            CopyReasons(result.FailureReasons),
            exitCode);
    }

    public static EvaluationQualityReport FromSnapshotLoadFailure(
        EvaluationSnapshot? baseline,
        EvaluationSnapshot? candidate,
        IReadOnlyList<string> failureReasons)
    {
        if (baseline is not null && candidate is not null)
        {
            throw new ArgumentException(
                "Both snapshots were loaded. Build the quality report from the regression-gate result.",
                nameof(candidate));
        }

        return new EvaluationQualityReport(
            FailedStatus,
            baseline?.TotalCases,
            candidate?.TotalCases,
            baseline?.FailureRate,
            candidate?.FailureRate,
            null,
            CopyReasons(failureReasons),
            EvaluationRegressionGateRunner.InvalidSnapshotExitCode);
    }

    private static string RequireStatus(string status)
    {
        if (status is not (PassedStatus or FailedStatus))
        {
            throw new ArgumentException(
                "Status must be Passed or Failed.",
                nameof(status));
        }

        return status;
    }

    private static int? RequireCount(int? count)
    {
        if (count is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "A case count cannot be negative.");
        }

        return count;
    }

    private static double? RequireRate(double? rate)
    {
        if (rate is double value && !double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "A failure rate must be a finite percentage.");
        }

        return rate;
    }

    private static int RequireExitCode(int exitCode, string status, IReadOnlyList<string> failureReasons)
    {
        if (exitCode is not (
            EvaluationRegressionGateRunner.PassedExitCode or
            EvaluationRegressionGateRunner.FailedExitCode or
            EvaluationRegressionGateRunner.InvalidSnapshotExitCode))
        {
            throw new ArgumentOutOfRangeException(nameof(exitCode), "The exit code must be 0, 1, or 2.");
        }

        var passed = status == PassedStatus;
        if (passed != (exitCode == EvaluationRegressionGateRunner.PassedExitCode))
        {
            throw new ArgumentException("The status does not match the exit code.", nameof(exitCode));
        }

        if (passed && failureReasons.Count > 0)
        {
            throw new ArgumentException(
                "A passed quality report cannot include failure reasons.",
                nameof(failureReasons));
        }

        if (!passed && failureReasons.Count == 0)
        {
            throw new ArgumentException(
                "A failed quality report needs a failure reason.",
                nameof(failureReasons));
        }

        return exitCode;
    }

    private static IReadOnlyList<string> CopyReasons(IReadOnlyList<string> failureReasons)
    {
        ArgumentNullException.ThrowIfNull(failureReasons);
        var copy = new string[failureReasons.Count];
        for (var index = 0; index < failureReasons.Count; index++)
        {
            copy[index] = failureReasons[index] ?? throw new ArgumentException(
                "Failure reasons cannot contain null values.",
                nameof(failureReasons));
        }

        return copy;
    }
}
