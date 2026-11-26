// Fingerprints of the files a regression-gate run reads.
// The dataset is the baseline snapshot followed by the candidate snapshot.
// Length prefixes keep one file's bytes from being confused with the other.
// The policy fingerprint is the policy file's own bytes.
public static class EvaluationInputFingerprints
{
    public static string Dataset(ReadOnlySpan<byte> baselineContent, ReadOnlySpan<byte> candidateContent)
    {
        var buffer = new byte[8 + baselineContent.Length + candidateContent.Length];
        WriteLength(buffer, 0, baselineContent.Length);
        baselineContent.CopyTo(buffer.AsSpan(4));
        WriteLength(buffer, 4 + baselineContent.Length, candidateContent.Length);
        candidateContent.CopyTo(buffer.AsSpan(8 + baselineContent.Length));
        return ContentFingerprint.FromBytes(buffer);
    }

    public static string Policy(ReadOnlySpan<byte> policyContent) =>
        ContentFingerprint.FromBytes(policyContent);

    private static void WriteLength(byte[] buffer, int offset, int length)
    {
        buffer[offset] = (byte)(length >> 24);
        buffer[offset + 1] = (byte)(length >> 16);
        buffer[offset + 2] = (byte)(length >> 8);
        buffer[offset + 3] = (byte)length;
    }
}
