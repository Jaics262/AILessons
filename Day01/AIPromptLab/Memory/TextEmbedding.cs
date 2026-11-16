public sealed class TextEmbedding
{
    public TextEmbedding(string text, IReadOnlyList<float> vector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(vector);

        if (vector.Count == 0)
        {
            throw new ArgumentException("The embedding vector cannot be empty.", nameof(vector));
        }

        Text = text.Trim();
        Vector = vector.ToArray();
    }

    public string Text { get; }

    public IReadOnlyList<float> Vector { get; }
}
