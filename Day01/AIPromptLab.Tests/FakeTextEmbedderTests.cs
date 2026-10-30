namespace AIPromptLab.Tests;

public sealed class FakeTextEmbedderTests
{
    private readonly FakeTextEmbedder _embedder = new();

    [Fact]
    public void Embed_returns_the_predefined_vector_for_a_known_phrase()
    {
        var first = _embedder.Embed(FakeTextEmbedder.TemporaryServerFailure);
        var second = _embedder.Embed("  " + FakeTextEmbedder.TemporaryServerFailure + "  ");

        Assert.Equal(FakeTextEmbedder.TemporaryServerFailure, first.Text);
        Assert.Equal(FakeTextEmbedder.VectorLength, first.Vector.Count);
        Assert.Equal(first.Vector, second.Vector);
        Assert.NotSame(first.Vector, second.Vector);
    }

    [Fact]
    public void Embed_places_the_transient_failure_phrases_near_each_other()
    {
        var serverFailure = _embedder.Embed(FakeTextEmbedder.TemporaryServerFailure);
        var httpError = _embedder.Embed(FakeTextEmbedder.TransientHttpError);
        var dotnet = _embedder.Embed(FakeTextEmbedder.DotNetProject);

        Assert.True(Dot(serverFailure.Vector, httpError.Vector) > Dot(serverFailure.Vector, dotnet.Vector));
    }

    [Fact]
    public void Embed_returns_distinct_vectors_for_unrelated_known_phrases()
    {
        var retryAfter = _embedder.Embed(FakeTextEmbedder.RetryAfter);
        var verbatim = _embedder.Embed(FakeTextEmbedder.VerbatimTurns);
        var dotnet = _embedder.Embed(FakeTextEmbedder.DotNetProject);

        Assert.NotEqual(retryAfter.Vector, verbatim.Vector);
        Assert.NotEqual(retryAfter.Vector, dotnet.Vector);
        Assert.NotEqual(verbatim.Vector, dotnet.Vector);
    }

    [Fact]
    public void Embed_rejects_a_phrase_without_a_predefined_vector()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _embedder.Embed("An unknown phrase."));

        Assert.Contains("An unknown phrase.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Embed_rejects_a_null_phrase()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _embedder.Embed(null!));

        Assert.Equal("text", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Embed_rejects_a_blank_phrase(string text)
    {
        var exception = Assert.Throws<ArgumentException>(() => _embedder.Embed(text));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_an_empty_vector()
    {
        var exception = Assert.Throws<ArgumentException>(() => new TextEmbedding("A phrase.", []));

        Assert.Equal("vector", exception.ParamName);
    }

    private static float Dot(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var sum = 0f;
        for (var index = 0; index < left.Count; index++)
        {
            sum += left[index] * right[index];
        }

        return sum;
    }
}
