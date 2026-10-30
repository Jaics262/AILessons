namespace AIPromptLab.Tests;

public sealed class VectorSimilarityTests
{
    [Fact]
    public void Cosine_scores_identical_orthogonal_and_opposite_vectors()
    {
        const double tolerance = 0.000001d;

        var identical = VectorSimilarity.Cosine([0.2f, 0.4f, 0.4f], [0.2f, 0.4f, 0.4f]);
        var orthogonal = VectorSimilarity.Cosine([1f, 0f, 0f], [0f, 1f, 0f]);
        var opposite = VectorSimilarity.Cosine([0.3f, -0.4f], [-0.3f, 0.4f]);

        Assert.InRange(identical, 1d - tolerance, 1d + tolerance);
        Assert.InRange(orthogonal, -tolerance, tolerance);
        Assert.InRange(opposite, -1d - tolerance, -1d + tolerance);
    }

    [Fact]
    public void Cosine_returns_one_for_vectors_in_the_same_direction()
    {
        var score = VectorSimilarity.Cosine([1f, 0f], [2f, 0f]);

        Assert.Equal(1d, score);
    }

    [Fact]
    public void Cosine_returns_zero_for_perpendicular_vectors()
    {
        var score = VectorSimilarity.Cosine([1f, 0f], [0f, 1f]);

        Assert.Equal(0d, score);
    }

    [Fact]
    public void Cosine_returns_minus_one_for_opposite_vectors()
    {
        var score = VectorSimilarity.Cosine([1f, 0f], [-1f, 0f]);

        Assert.Equal(-1d, score);
    }

    [Fact]
    public void Cosine_scores_related_failure_phrases_above_an_unrelated_phrase()
    {
        var embedder = new FakeTextEmbedder();
        var serverFailure = embedder.Embed(FakeTextEmbedder.TemporaryServerFailure).Vector;
        var httpError = embedder.Embed(FakeTextEmbedder.TransientHttpError).Vector;
        var dotnet = embedder.Embed(FakeTextEmbedder.DotNetProject).Vector;

        var related = VectorSimilarity.Cosine(serverFailure, httpError);
        var unrelated = VectorSimilarity.Cosine(serverFailure, dotnet);

        Assert.True(related > unrelated);
        Assert.True(related > 0.9d);
        Assert.Equal(0d, unrelated);
    }

    [Fact]
    public void Cosine_rejects_vectors_of_different_lengths()
    {
        var exception = Assert.Throws<ArgumentException>(() => VectorSimilarity.Cosine([1f, 0f], [1f]));

        Assert.Equal("right", exception.ParamName);
    }

    [Fact]
    public void Cosine_rejects_an_empty_vector()
    {
        var left = Assert.Throws<ArgumentException>(() => VectorSimilarity.Cosine([], [1f]));
        var right = Assert.Throws<ArgumentException>(() => VectorSimilarity.Cosine([1f], []));

        Assert.Equal("left", left.ParamName);
        Assert.Equal("right", right.ParamName);
    }

    [Fact]
    public void Cosine_rejects_a_zero_magnitude_vector()
    {
        var left = Assert.Throws<ArgumentException>(() => VectorSimilarity.Cosine([0f, 0f], [1f, 0f]));
        var right = Assert.Throws<ArgumentException>(() => VectorSimilarity.Cosine([1f, 0f], [0f, 0f]));

        Assert.Equal("left", left.ParamName);
        Assert.Equal("right", right.ParamName);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Cosine_rejects_a_non_finite_value(float value)
    {
        var left = Assert.Throws<ArgumentException>(() => VectorSimilarity.Cosine([value, 1f], [1f, 0f]));
        var right = Assert.Throws<ArgumentException>(() => VectorSimilarity.Cosine([1f, 0f], [1f, value]));

        Assert.Equal("left", left.ParamName);
        Assert.Equal("right", right.ParamName);
    }

    [Fact]
    public void Cosine_rejects_a_null_vector()
    {
        var left = Assert.Throws<ArgumentNullException>(() => VectorSimilarity.Cosine(null!, [1f]));
        var right = Assert.Throws<ArgumentNullException>(() => VectorSimilarity.Cosine([1f], null!));

        Assert.Equal("left", left.ParamName);
        Assert.Equal("right", right.ParamName);
    }
}
