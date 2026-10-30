namespace AIPromptLab.Tests;

public sealed class EmbeddingMemoryRetrieverTests
{
    private readonly FakeTextEmbedder _embedder = new();

    [Fact]
    public void Search_ranks_a_paraphrase_above_unrelated_memories()
    {
        var dotnet = Entry("dotnet", FakeTextEmbedder.DotNetProject);
        var verbatim = Entry("verbatim", FakeTextEmbedder.VerbatimTurns);
        var retryAfter = Entry("retry-after", FakeTextEmbedder.RetryAfter);
        var serverFailure = Entry("server-failure", FakeTextEmbedder.TemporaryServerFailure);
        var memories = new List<MemoryEntry> { dotnet, verbatim, retryAfter, serverFailure };
        var retriever = IndexAll(memories);

        var results = retriever.Search(memories, FakeTextEmbedder.TransientHttpError);
        var keywordResults = new KeywordMemoryScorer().Search(memories, FakeTextEmbedder.TransientHttpError);

        Assert.Equal(["server-failure", "retry-after", "dotnet"], results.Select(entry => entry.Id));
        Assert.Equal(["retry-after", "dotnet", "verbatim"], keywordResults.Select(entry => entry.Id));
        Assert.Equal([dotnet, verbatim, retryAfter, serverFailure], memories);
    }

    [Fact]
    public void Search_returns_at_most_three_memories()
    {
        var memories = new List<MemoryEntry>
        {
            Entry("dotnet", FakeTextEmbedder.DotNetProject),
            Entry("verbatim", FakeTextEmbedder.VerbatimTurns),
            Entry("retry-after", FakeTextEmbedder.RetryAfter),
            Entry("server-failure", FakeTextEmbedder.TemporaryServerFailure)
        };

        var results = IndexAll(memories).Search(memories, FakeTextEmbedder.RetryDelayQuestion);

        Assert.Equal(EmbeddingMemoryRetriever.ResultLimit, results.Count);
        Assert.Equal(["retry-after", "server-failure", "dotnet"], results.Select(entry => entry.Id));
        Assert.DoesNotContain(results, entry => entry.Id == "verbatim");
    }

    [Fact]
    public void Search_returns_fewer_results_when_fewer_embeddings_are_stored()
    {
        var indexed = Entry("retry-after", FakeTextEmbedder.RetryAfter);
        var missing = Entry("verbatim", FakeTextEmbedder.VerbatimTurns);
        var retriever = new EmbeddingMemoryRetriever(_embedder);
        retriever.Index(indexed);

        var results = retriever.Search([missing, indexed], FakeTextEmbedder.RetryDelayQuestion);

        Assert.Equal([indexed], results);
    }

    [Fact]
    public void Search_returns_no_results_when_nothing_is_stored()
    {
        var memories = new List<MemoryEntry>
        {
            Entry("retry-after", FakeTextEmbedder.RetryAfter)
        };

        var results = new EmbeddingMemoryRetriever(_embedder).Search(memories, FakeTextEmbedder.RetryDelayQuestion);

        Assert.Empty(results);
        Assert.Single(memories);
    }

    [Fact]
    public void Search_leaves_the_memory_store_unchanged()
    {
        var dotnet = Entry("dotnet", FakeTextEmbedder.DotNetProject);
        var retryAfter = Entry("retry-after", FakeTextEmbedder.RetryAfter);
        var verbatim = Entry("verbatim", FakeTextEmbedder.VerbatimTurns);
        var serverFailure = Entry("server-failure", FakeTextEmbedder.TemporaryServerFailure);
        var store = new InMemoryMemoryStore();
        store.Add(dotnet);
        store.Add(retryAfter);
        store.Add(verbatim);
        store.Add(serverFailure);
        var stored = store.Retrieve();
        var retriever = IndexAll(stored);

        var results = retriever.Search(store.Retrieve(), FakeTextEmbedder.RetryDelayQuestion);

        Assert.Equal(["retry-after", "server-failure", "dotnet"], results.Select(entry => entry.Id));
        Assert.Same(dotnet, store.Find("dotnet"));
        Assert.Same(retryAfter, store.Find("retry-after"));
        Assert.Same(verbatim, store.Find("verbatim"));
        Assert.Same(serverFailure, store.Find("server-failure"));
        Assert.Equal(FakeTextEmbedder.DotNetProject, store.Find("dotnet")!.Text);
        Assert.Equal(dotnet.CreatedAt, store.Find("dotnet")!.CreatedAt);
        Assert.Equal([dotnet, retryAfter, verbatim, serverFailure], store.Retrieve());
        var after = store.Retrieve();
        for (var index = 0; index < stored.Count; index++)
        {
            Assert.Same(stored[index], after[index]);
        }
    }

    [Fact]
    public void Search_leaves_the_memory_list_unchanged()
    {
        var dotnet = Entry("dotnet", FakeTextEmbedder.DotNetProject);
        var retryAfter = Entry("retry-after", FakeTextEmbedder.RetryAfter);
        var memories = new List<MemoryEntry> { dotnet, retryAfter };
        var retriever = IndexAll(memories);

        var results = retriever.Search(memories, FakeTextEmbedder.RetryDelayQuestion);

        Assert.Equal([retryAfter, dotnet], results);
        Assert.Same(dotnet, memories[0]);
        Assert.Same(retryAfter, memories[1]);
        Assert.Equal(FakeTextEmbedder.DotNetProject, memories[0].Text);
    }

    [Fact]
    public void Search_keeps_a_copy_of_the_stored_vector()
    {
        var serverVector = new float[] { 1f, 0f };
        var embedder = new SharedVectorEmbedder();
        embedder.Set("The server fails.", serverVector);
        embedder.Set("The query.", [1f, 0f]);
        embedder.Set("Other.", [0f, 1f]);
        var other = Entry("other", "Other.");
        var server = Entry("server", "The server fails.");
        var retriever = new EmbeddingMemoryRetriever(embedder);
        retriever.Index(other);
        retriever.Index(server);
        serverVector[0] = 0f;
        serverVector[1] = 1f;

        var results = retriever.Search([other, server], "The query.");

        Assert.Same(server, results[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_returns_no_results_for_an_empty_query(string? query)
    {
        var memory = Entry("retry-after", FakeTextEmbedder.RetryAfter);
        var retriever = new EmbeddingMemoryRetriever(_embedder);
        retriever.Index(memory);

        var results = retriever.Search([memory], query);

        Assert.Empty(results);
    }

    [Fact]
    public void Search_returns_each_memory_id_once()
    {
        var first = Entry("retry-after", FakeTextEmbedder.RetryAfter);
        var duplicate = Entry("retry-after", FakeTextEmbedder.DotNetProject);
        var retriever = new EmbeddingMemoryRetriever(_embedder);
        retriever.Index(first);

        var results = retriever.Search([first, duplicate], FakeTextEmbedder.RetryDelayQuestion);

        Assert.Equal([first], results);
    }

    [Fact]
    public void Search_rejects_a_null_memory_list()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new EmbeddingMemoryRetriever(_embedder).Search(null!, FakeTextEmbedder.RetryDelayQuestion));

        Assert.Equal("memories", exception.ParamName);
    }

    [Fact]
    public void Index_rejects_a_null_entry()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new EmbeddingMemoryRetriever(_embedder).Index(null!));

        Assert.Equal("entry", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_null_embedder()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new EmbeddingMemoryRetriever(null!));

        Assert.Equal("embedder", exception.ParamName);
    }

    private EmbeddingMemoryRetriever IndexAll(IReadOnlyList<MemoryEntry> memories)
    {
        var retriever = new EmbeddingMemoryRetriever(_embedder);
        foreach (var memory in memories)
        {
            retriever.Index(memory);
        }

        return retriever;
    }

    private static MemoryEntry Entry(string id, string text)
    {
        return new MemoryEntry(id, text, DateTimeOffset.Parse("2026-10-30T09:00:00Z"));
    }

    private sealed class SharedVectorEmbedder : ITextEmbedder
    {
        private readonly Dictionary<string, float[]> _vectors = new(StringComparer.Ordinal);

        public void Set(string text, float[] vector)
        {
            _vectors[text] = vector;
        }

        public TextEmbedding Embed(string text)
        {
            return new TextEmbedding(text, _vectors[text.Trim()]);
        }
    }
}
