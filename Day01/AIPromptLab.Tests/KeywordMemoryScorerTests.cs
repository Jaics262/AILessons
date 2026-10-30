namespace AIPromptLab.Tests;

public sealed class KeywordMemoryScorerTests
{
    [Fact]
    public void Search_ranks_the_retry_after_memory_above_unrelated_memories()
    {
        var dotnet = Entry("dotnet", "The project uses .NET 10 and xUnit.");
        var retryAfter = Entry("retry-after", "HTTP retries should respect Retry-After.");
        var verbatim = Entry("verbatim", "Recent conversation turns must remain verbatim.");
        var store = new InMemoryMemoryStore();
        store.Add(dotnet);
        store.Add(retryAfter);
        store.Add(verbatim);
        var stored = store.Retrieve();

        var results = new KeywordMemoryScorer().Search(
            stored,
            "How should the application handle HTTP retry delays?");

        Assert.Equal([retryAfter, dotnet, verbatim], results);
        Assert.Same(retryAfter, results[0]);
        AssertSameEntries(stored, store.Retrieve());
    }

    [Fact]
    public void Search_returns_at_most_three_memories_with_the_strongest_overlap_first()
    {
        var memories = new List<MemoryEntry>
        {
            Entry("alpha-beta-gamma", "alpha beta gamma"),
            Entry("alpha-beta", "alpha beta"),
            Entry("alpha", "alpha"),
            Entry("beta", "beta"),
            Entry("unrelated", "unrelated topic")
        };
        var stored = memories.ToArray();

        var results = new KeywordMemoryScorer().Search(memories, "alpha beta gamma delta");

        Assert.Equal(KeywordMemoryScorer.ResultLimit, results.Count);
        Assert.Equal(["alpha-beta-gamma", "alpha-beta", "alpha"], results.Select(entry => entry.Id));
        Assert.DoesNotContain(results, entry => entry.Id == "beta");
        Assert.DoesNotContain(results, entry => entry.Id == "unrelated");
        AssertSameEntries(stored, memories);
    }

    [Fact]
    public void Search_returns_fewer_results_when_fewer_memories_are_available()
    {
        var memory = Entry("retry-after", "HTTP retries should respect Retry-After.");

        var results = new KeywordMemoryScorer().Search([memory], "HTTP retry delays");

        Assert.Equal([memory], results);
    }

    [Fact]
    public void Search_returns_no_results_when_the_memory_store_is_empty()
    {
        var store = new InMemoryMemoryStore();

        var results = new KeywordMemoryScorer().Search(store.Retrieve(), "How should the application handle HTTP retry delays?");

        Assert.Empty(results);
        Assert.Empty(store.Retrieve());
    }

    [Fact]
    public void Search_leaves_stored_memories_unchanged()
    {
        var dotnet = Entry("dotnet", "The project uses .NET 10 and xUnit.");
        var retryAfter = Entry("retry-after", "HTTP retries should respect Retry-After.");
        var store = new InMemoryMemoryStore();
        store.Add(dotnet);
        store.Add(retryAfter);
        var stored = store.Retrieve();

        var results = new KeywordMemoryScorer().Search(
            stored,
            "How should the application handle HTTP retry delays?");

        Assert.Equal([retryAfter, dotnet], results);
        Assert.Same(dotnet, stored[0]);
        Assert.Same(retryAfter, stored[1]);
        Assert.Same(dotnet, store.Find("dotnet"));
        Assert.Same(retryAfter, store.Find("retry-after"));
        Assert.Equal("The project uses .NET 10 and xUnit.", store.Find("dotnet")!.Text);
        Assert.Equal(dotnet.CreatedAt, store.Find("dotnet")!.CreatedAt);
        Assert.Equal([dotnet, retryAfter], store.Retrieve());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_returns_no_results_for_an_empty_query(string? query)
    {
        var memories = new List<MemoryEntry>
        {
            Entry("retry-after", "HTTP retries should respect Retry-After.")
        };

        var results = new KeywordMemoryScorer().Search(memories, query);

        Assert.Empty(results);
        Assert.Single(memories);
    }

    [Fact]
    public void Search_returns_each_memory_id_once()
    {
        var first = Entry("retry-after", "HTTP retries should respect Retry-After.");
        var duplicate = Entry("retry-after", "Retry-After is the delay header.");
        var other = Entry("verbatim", "Recent conversation turns must remain verbatim.");

        var results = new KeywordMemoryScorer().Search(
            [first, duplicate, other],
            "How should the application handle HTTP retry delays?");

        Assert.Equal([first, other], results);
        Assert.Equal(1, results.Count(entry => entry.Id == "retry-after"));
    }

    [Fact]
    public void Search_rejects_a_null_memory_list()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new KeywordMemoryScorer().Search(null!, "HTTP retry delays"));

        Assert.Equal("memories", exception.ParamName);
    }

    private static MemoryEntry Entry(string id, string text)
    {
        return new MemoryEntry(id, text, DateTimeOffset.Parse("2026-10-30T09:00:00Z"));
    }

    private static void AssertSameEntries(IReadOnlyList<MemoryEntry> expected, IReadOnlyList<MemoryEntry> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Same(expected[index], actual[index]);
        }
    }
}
