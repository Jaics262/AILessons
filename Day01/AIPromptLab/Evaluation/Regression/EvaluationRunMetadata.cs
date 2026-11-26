using System.Globalization;

// Input identity for one regression-gate report.
// Fingerprints name the file bytes the run read. They do not make an AI evaluation deterministic.
// The timestamp names when this run happened, so a later run of the same files has a later timestamp.
public sealed class EvaluationRunMetadata
{
    public const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public EvaluationRunMetadata(
        string? datasetFingerprint,
        string? policyFingerprint,
        DateTimeOffset runTimestampUtc)
    {
        DatasetFingerprint = RequireFingerprint(datasetFingerprint, nameof(datasetFingerprint));
        PolicyFingerprint = RequireFingerprint(policyFingerprint, nameof(policyFingerprint));
        RunTimestampUtc = runTimestampUtc.ToUniversalTime();
    }

    public string? DatasetFingerprint { get; }

    public string? PolicyFingerprint { get; }

    public DateTimeOffset RunTimestampUtc { get; }

    public string RunTimestampUtcText =>
        RunTimestampUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static string? RequireFingerprint(string? fingerprint, string paramName)
    {
        if (fingerprint is null)
        {
            return null;
        }

        if (fingerprint.Length != ContentFingerprint.Prefix.Length + 64 ||
            !fingerprint.StartsWith(ContentFingerprint.Prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A fingerprint must be sha256: followed by 64 lowercase hexadecimal characters.",
                paramName);
        }

        for (var index = ContentFingerprint.Prefix.Length; index < fingerprint.Length; index++)
        {
            var character = fingerprint[index];
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                throw new ArgumentException(
                    "A fingerprint must be sha256: followed by 64 lowercase hexadecimal characters.",
                    paramName);
            }
        }

        return fingerprint;
    }
}
