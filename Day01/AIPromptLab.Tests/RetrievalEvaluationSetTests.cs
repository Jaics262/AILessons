namespace AIPromptLab.Tests;

public sealed class RetrievalEvaluationSetTests
{
    [Fact]
    public void Cases_contains_the_ten_fixed_queries()
    {
        var cases = RetrievalEvaluationSet.Cases;

        Assert.Equal(10, cases.Count);
        Assert.Equal(
            [RetrievalEvaluationSet.RetryAfter],
            Case(cases, "How should HTTP retries work?").ExpectedMemoryIds);
        Assert.Equal(
            [RetrievalEvaluationSet.DotNet],
            Case(cases, "Which framework does the project use?").ExpectedMemoryIds);
        Assert.Equal(
            [RetrievalEvaluationSet.SessionHistory],
            Case(cases, "How is conversation history managed?").ExpectedMemoryIds);
        Assert.Equal(
            [RetrievalEvaluationSet.ConversationSummary],
            Case(cases, "How are older conversations compressed?").ExpectedMemoryIds);
        Assert.Equal(
            [RetrievalEvaluationSet.VectorSimilarity],
            Case(cases, "How are similar memories found?").ExpectedMemoryIds);
        Assert.Equal(
            [RetrievalEvaluationSet.LocalTools],
            Case(cases, "Which tools can the model call?").ExpectedMemoryIds);
        Assert.Equal(
            [RetrievalEvaluationSet.LearningSummary],
            Case(cases, "What must a learning summary contain?").ExpectedMemoryIds);
        Assert.Equal(
            [RetrievalEvaluationSet.VerbatimTurns],
            Case(cases, "Do recent turns remain verbatim?").ExpectedMemoryIds);
        Assert.Equal(
            [RetrievalEvaluationSet.FunctionRounds],
            Case(cases, "How many tool rounds can one prompt use?").ExpectedMemoryIds);
        Assert.Equal(
            [RetrievalEvaluationSet.HistoryOnFailure],
            Case(cases, "What happens to stored history when a request fails?").ExpectedMemoryIds);
        Assert.Equal(cases.Count, cases.Select(item => item.Query).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Constructor_copies_the_expected_memory_ids()
    {
        var ids = new[] { RetrievalEvaluationSet.DotNet };

        var testCase = new RetrievalTestCase("Which framework does the project use?", ids);
        ids[0] = RetrievalEvaluationSet.RetryAfter;

        Assert.Equal([RetrievalEvaluationSet.DotNet], testCase.ExpectedMemoryIds);
    }

    [Fact]
    public void Constructor_rejects_a_null_query()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new RetrievalTestCase(null!, [RetrievalEvaluationSet.DotNet]));

        Assert.Equal("query", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_query(string query)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new RetrievalTestCase(query, [RetrievalEvaluationSet.DotNet]));

        Assert.Equal("query", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_missing_expected_memory_ids()
    {
        var missing = Assert.Throws<ArgumentNullException>(
            () => new RetrievalTestCase("Which framework does the project use?", null!));
        var empty = Assert.Throws<ArgumentException>(
            () => new RetrievalTestCase("Which framework does the project use?", []));

        Assert.Equal("expectedMemoryIds", missing.ParamName);
        Assert.Equal("expectedMemoryIds", empty.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_expected_memory_id(string? id)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new RetrievalTestCase("Which framework does the project use?", [id!]));

        Assert.Equal("expectedMemoryIds", exception.ParamName);
    }

    private static RetrievalTestCase Case(IReadOnlyList<RetrievalTestCase> cases, string query)
    {
        return Assert.Single(cases, item => item.Query == query);
    }
}
