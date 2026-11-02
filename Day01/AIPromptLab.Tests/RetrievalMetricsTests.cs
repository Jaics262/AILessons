namespace AIPromptLab.Tests;

public sealed class RetrievalMetricsTests
{
    [Fact]
    public void PrecisionAtK_counts_relevant_memories_in_the_retrieved_list()
    {
        var score = RetrievalMetrics.PrecisionAtK(["A", "B", "C"], ["A", "C"], 3);

        Assert.Equal(2d / 3d, score);
    }

    [Fact]
    public void PrecisionAtK_uses_only_the_first_k_retrieved_memories()
    {
        var score = RetrievalMetrics.PrecisionAtK(["A", "B", "C", "D"], ["A", "C", "D"], 3);

        Assert.Equal(2d / 3d, score);
    }

    [Fact]
    public void PrecisionAtK_uses_the_shorter_retrieved_list_when_fewer_than_k_are_returned()
    {
        var score = RetrievalMetrics.PrecisionAtK(["A", "B"], ["A"], 3);

        Assert.Equal(0.5d, score);
    }

    [Fact]
    public void PrecisionAtK_returns_zero_when_nothing_was_retrieved()
    {
        var score = RetrievalMetrics.PrecisionAtK([], ["A"], 3);

        Assert.Equal(0d, score);
    }

    [Fact]
    public void PrecisionAtK_returns_zero_when_no_retrieved_memory_is_relevant()
    {
        var score = RetrievalMetrics.PrecisionAtK(["A", "B", "C"], ["D"], 3);

        Assert.Equal(0d, score);
    }

    [Fact]
    public void PrecisionAtK_returns_zero_when_no_memories_are_expected()
    {
        var score = RetrievalMetrics.PrecisionAtK(["A", "B", "C"], [], 3);

        Assert.Equal(0d, score);
    }

    [Fact]
    public void PrecisionAtK_returns_one_when_every_scored_memory_is_relevant()
    {
        var score = RetrievalMetrics.PrecisionAtK(["A", "C"], ["A", "C"], 3);

        Assert.Equal(1d, score);
    }

    [Fact]
    public void PrecisionAtK_counts_each_retrieved_row()
    {
        var score = RetrievalMetrics.PrecisionAtK(["A", "A", "B"], ["A"], 3);

        Assert.Equal(2d / 3d, score);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PrecisionAtK_rejects_a_k_below_one(int k)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RetrievalMetrics.PrecisionAtK(["A"], ["A"], k));

        Assert.Equal("k", exception.ParamName);
    }

    [Fact]
    public void PrecisionAtK_rejects_a_null_id_list()
    {
        var retrieved = Assert.Throws<ArgumentNullException>(
            () => RetrievalMetrics.PrecisionAtK(null!, ["A"], 1));
        var relevant = Assert.Throws<ArgumentNullException>(
            () => RetrievalMetrics.PrecisionAtK(["A"], null!, 1));

        Assert.Equal("retrievedIds", retrieved.ParamName);
        Assert.Equal("relevantIds", relevant.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PrecisionAtK_rejects_a_blank_memory_id(string? id)
    {
        var retrieved = Assert.Throws<ArgumentException>(
            () => RetrievalMetrics.PrecisionAtK([id!], ["A"], 1));
        var relevant = Assert.Throws<ArgumentException>(
            () => RetrievalMetrics.PrecisionAtK(["A"], [id!], 1));

        Assert.Equal("retrievedIds", retrieved.ParamName);
        Assert.Equal("relevantIds", relevant.ParamName);
    }

    [Fact]
    public void RecallAtK_counts_expected_memories_found_in_the_retrieved_list()
    {
        var score = RetrievalMetrics.RecallAtK(["A", "C", "D"], ["A", "B"], 3);

        Assert.Equal(0.5d, score);
    }

    [Fact]
    public void RecallAtK_ignores_a_relevant_memory_past_k()
    {
        var score = RetrievalMetrics.RecallAtK(["A", "C", "B"], ["A", "B"], 2);

        Assert.Equal(0.5d, score);
    }

    [Fact]
    public void RecallAtK_returns_zero_when_no_retrieved_memory_is_relevant()
    {
        var score = RetrievalMetrics.RecallAtK(["A", "C", "D"], ["B"], 3);

        Assert.Equal(0d, score);
    }

    [Fact]
    public void RecallAtK_counts_a_duplicate_retrieved_id_once()
    {
        var score = RetrievalMetrics.RecallAtK(["A", "A", "C"], ["A", "B"], 3);

        Assert.Equal(0.5d, score);
    }

    [Fact]
    public void RecallAtK_returns_zero_when_nothing_was_retrieved()
    {
        var score = RetrievalMetrics.RecallAtK([], ["A", "B"], 3);

        Assert.Equal(0d, score);
    }

    [Fact]
    public void RecallAtK_returns_one_when_every_expected_memory_is_retrieved()
    {
        var score = RetrievalMetrics.RecallAtK(["B", "A"], ["A", "B"], 3);

        Assert.Equal(1d, score);
    }

    [Fact]
    public void RecallAtK_counts_each_expected_memory_once()
    {
        var score = RetrievalMetrics.RecallAtK(["A", "A"], ["A", "A", "B"], 3);

        Assert.Equal(0.5d, score);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RecallAtK_rejects_a_k_below_one(int k)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => RetrievalMetrics.RecallAtK(["A"], ["A"], k));

        Assert.Equal("k", exception.ParamName);
    }

    [Fact]
    public void RecallAtK_rejects_a_null_id_list()
    {
        var retrieved = Assert.Throws<ArgumentNullException>(
            () => RetrievalMetrics.RecallAtK(null!, ["A"], 1));
        var relevant = Assert.Throws<ArgumentNullException>(
            () => RetrievalMetrics.RecallAtK(["A"], null!, 1));

        Assert.Equal("retrievedIds", retrieved.ParamName);
        Assert.Equal("relevantIds", relevant.ParamName);
    }

    [Fact]
    public void RecallAtK_rejects_an_empty_expected_list()
    {
        var exception = Assert.Throws<ArgumentException>(() => RetrievalMetrics.RecallAtK(["A"], [], 1));

        Assert.Equal("relevantIds", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RecallAtK_rejects_a_blank_memory_id(string? id)
    {
        var retrieved = Assert.Throws<ArgumentException>(
            () => RetrievalMetrics.RecallAtK([id!], ["A"], 1));
        var relevant = Assert.Throws<ArgumentException>(
            () => RetrievalMetrics.RecallAtK(["A"], [id!], 1));

        Assert.Equal("retrievedIds", retrieved.ParamName);
        Assert.Equal("relevantIds", relevant.ParamName);
    }
}
