// Compares two completed evaluation snapshots.
// The change in failure rate is in percentage points: latest rate minus previous rate.
public static class EvaluationRunComparer
{
    public static EvaluationRunComparison Compare(EvaluationSnapshot previous, EvaluationSnapshot latest)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(latest);
        return new EvaluationRunComparison(previous, latest);
    }
}
