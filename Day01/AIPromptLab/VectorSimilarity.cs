public static class VectorSimilarity
{
    // Empty vectors, unequal lengths, non-finite values, and zero magnitudes throw ArgumentException.
    public static double Cosine(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Count == 0)
        {
            throw new ArgumentException("The vector cannot be empty.", nameof(left));
        }

        if (right.Count == 0)
        {
            throw new ArgumentException("The vector cannot be empty.", nameof(right));
        }

        if (left.Count != right.Count)
        {
            throw new ArgumentException("Vectors must contain the same number of values.", nameof(right));
        }

        double dot = 0;
        double leftSquare = 0;
        double rightSquare = 0;
        for (var index = 0; index < left.Count; index++)
        {
            var leftValue = left[index];
            var rightValue = right[index];
            if (!float.IsFinite(leftValue))
            {
                throw new ArgumentException("Vectors must contain only finite values.", nameof(left));
            }

            if (!float.IsFinite(rightValue))
            {
                throw new ArgumentException("Vectors must contain only finite values.", nameof(right));
            }

            dot += leftValue * (double)rightValue;
            leftSquare += leftValue * (double)leftValue;
            rightSquare += rightValue * (double)rightValue;
        }

        if (leftSquare == 0)
        {
            throw new ArgumentException("The vector must have a non-zero magnitude.", nameof(left));
        }

        if (rightSquare == 0)
        {
            throw new ArgumentException("The vector must have a non-zero magnitude.", nameof(right));
        }

        return dot / (Math.Sqrt(leftSquare) * Math.Sqrt(rightSquare));
    }
}
