using System.Globalization;

// Compares one completed run with a later one.
// FailureRateChange is the latest failure rate minus the previous one, in percentage points.
// 30% to 15% is -15 percentage points, which is an improvement. It is not a 50% relative change.
// A lower rate improved. A higher rate worsened. An equal rate stayed the same.
// An empty run stores failure rate 0. This comparison uses that stored rate and does not divide.
// Different dataset sizes stay comparable on the rate, because each rate is that run's own share of failed cases.
// The failed-case change stays the raw count, so it can move differently from the rate when the sizes differ.
public sealed class EvaluationRunComparison
{
    private readonly Dictionary<EvaluationFailureCategory, int> _previousCounts;
    private readonly Dictionary<EvaluationFailureCategory, int> _latestCounts;

    public EvaluationRunComparison(EvaluationSnapshot previous, EvaluationSnapshot latest)
    {
        Previous = Copy(previous, nameof(previous));
        Latest = Copy(latest, nameof(latest));
        _previousCounts = Freeze(Previous);
        _latestCounts = Freeze(Latest);
        FailureRateChange = Latest.FailureRate - Previous.FailureRate;
        FailedCaseChange = Latest.FailedCases - Previous.FailedCases;
        DatasetSizeChanged = Previous.TotalCases != Latest.TotalCases;
        Direction = DirectionOf(FailureRateChange);
    }

    public EvaluationSnapshot Previous { get; }

    public EvaluationSnapshot Latest { get; }

    public double FailureRateChange { get; }

    public int FailedCaseChange { get; }

    public bool DatasetSizeChanged { get; }

    public EvaluationRateDirection Direction { get; }

    public IReadOnlyDictionary<EvaluationFailureCategory, int> PreviousCategoryCounts =>
        new Dictionary<EvaluationFailureCategory, int>(_previousCounts);

    public IReadOnlyDictionary<EvaluationFailureCategory, int> LatestCategoryCounts =>
        new Dictionary<EvaluationFailureCategory, int>(_latestCounts);

    public IReadOnlyDictionary<EvaluationFailureCategory, int> CategoryCountChanges
    {
        get
        {
            var changes = new Dictionary<EvaluationFailureCategory, int>();
            foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
            {
                changes.Add(category, _latestCounts[category] - _previousCounts[category]);
            }

            return changes;
        }
    }

    public string Report
    {
        get
        {
            var lines = new List<string>
            {
                "Evaluation comparison:",
                $"Previous: {Previous.RunId}",
                $"Latest: {Latest.RunId}",
                $"Total cases: {Previous.TotalCases} -> {Latest.TotalCases}",
                $"Failed cases: {Previous.FailedCases} -> {Latest.FailedCases}",
                $"Failed-case change: {FailedCaseChange.ToString(CultureInfo.InvariantCulture)}",
                $"Failure rate: {FormatRate(Previous.FailureRate)} -> {FormatRate(Latest.FailureRate)}",
                $"Failure-rate change: {FormatPoints(FailureRateChange)} percentage points"
            };

            foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
            {
                var change = _latestCounts[category] - _previousCounts[category];
                lines.Add(
                    $"{category}: {_previousCounts[category]} -> {_latestCounts[category]} (change {change.ToString(CultureInfo.InvariantCulture)})");
            }

            var note = Note();
            if (note is not null)
            {
                lines.Add(note);
            }

            lines.Add(Verdict());
            return string.Join('\n', lines);
        }
    }

    public override string ToString() => Report;

    private string? Note()
    {
        if (Previous.TotalCases == 0 && Latest.TotalCases == 0)
        {
            return "Both runs have no cases. Their failure rates are 0.";
        }

        if (Previous.TotalCases == 0)
        {
            return "The previous run has no cases. Its failure rate is 0.";
        }

        if (Latest.TotalCases == 0)
        {
            return "The latest run has no cases. Its failure rate is 0.";
        }

        if (DatasetSizeChanged)
        {
            return
                $"Dataset size changed from {Previous.TotalCases} to {Latest.TotalCases} cases. " +
                "The failure rate is the share of failed cases in each run. " +
                "The failed-case change is the raw count between those sizes.";
        }

        return null;
    }

    private string Verdict()
    {
        var points = FormatPoints(Math.Abs(FailureRateChange));
        return Direction switch
        {
            EvaluationRateDirection.Improved =>
                $"The failure rate improved by {points} percentage points.",
            EvaluationRateDirection.Worsened =>
                $"The failure rate worsened by {points} percentage points.",
            EvaluationRateDirection.Same => "The failure rate stayed the same.",
            _ => throw new InvalidOperationException("The failure-rate direction is not defined.")
        };
    }

    private static EvaluationRateDirection DirectionOf(double failureRateChange)
    {
        if (failureRateChange < 0d)
        {
            return EvaluationRateDirection.Improved;
        }

        if (failureRateChange > 0d)
        {
            return EvaluationRateDirection.Worsened;
        }

        return EvaluationRateDirection.Same;
    }

    private static EvaluationSnapshot Copy(EvaluationSnapshot snapshot, string paramName)
    {
        ArgumentNullException.ThrowIfNull(snapshot, paramName);
        return new EvaluationSnapshot(
            snapshot.RunId,
            snapshot.EvaluatedAt,
            snapshot.TotalCases,
            snapshot.FailedCases,
            snapshot.FailureRate,
            snapshot.CategoryCounts,
            snapshot.CaseIds);
    }

    private static Dictionary<EvaluationFailureCategory, int> Freeze(EvaluationSnapshot snapshot)
    {
        var counts = new Dictionary<EvaluationFailureCategory, int>();
        foreach (var category in Enum.GetValues<EvaluationFailureCategory>())
        {
            counts.Add(category, snapshot.CategoryCounts[category]);
        }

        return counts;
    }

    private static string FormatRate(double value) =>
        $"{FormatPoints(value)}%";

    private static string FormatPoints(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
