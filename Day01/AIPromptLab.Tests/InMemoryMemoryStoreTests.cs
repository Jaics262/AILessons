namespace AIPromptLab.Tests;

public sealed class InMemoryMemoryStoreTests
{
    [Fact]
    public void Retrieve_returns_entries_in_the_order_they_were_added()
    {
        var store = new InMemoryMemoryStore();
        var older = Entry("older", "The user is building AIPromptLab with .NET 10.", "2026-10-28T09:00:00Z");
        var newer = Entry("newer", "The current task is to improve conversation memory.", "2026-10-30T09:00:00Z");

        store.Add(older);
        store.Add(newer);
        var retrieved = store.Retrieve();

        Assert.Equal([older, newer], retrieved);
        Assert.Same(older, retrieved[0]);
        Assert.Same(newer, retrieved[1]);
        store.Add(Entry("third", "The agent has a calculate_total tool.", "2026-10-30T10:00:00Z"));
        Assert.Equal(2, retrieved.Count);
    }

    [Fact]
    public void Retrieve_returns_an_empty_list_when_the_store_is_empty()
    {
        var retrieved = new InMemoryMemoryStore().Retrieve();

        Assert.Empty(retrieved);
    }

    [Fact]
    public void Find_returns_the_entry_with_the_requested_id()
    {
        var store = new InMemoryMemoryStore();
        var constraint = Entry("constraint", "Tests avoid live Gemini API calls.", "2026-10-30T09:15:00Z");
        store.Add(Entry("decision", "Build AIPromptLab with .NET 10.", "2026-10-30T09:00:00Z"));
        store.Add(constraint);

        var found = store.Find("constraint");

        Assert.NotNull(found);
        Assert.Same(constraint, found);
        Assert.Equal("Tests avoid live Gemini API calls.", found.Text);
    }

    [Fact]
    public void Find_returns_null_when_the_id_is_unknown()
    {
        var store = new InMemoryMemoryStore();
        store.Add(Entry("known", "A stored fact.", "2026-10-30T09:00:00Z"));

        Assert.Null(store.Find("missing"));
    }

    [Fact]
    public void Add_rejects_a_null_entry()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new InMemoryMemoryStore().Add(null!));

        Assert.Equal("entry", exception.ParamName);
    }

    [Fact]
    public void Add_rejects_a_duplicate_id()
    {
        var store = new InMemoryMemoryStore();
        store.Add(Entry("same", "First text.", "2026-10-30T09:00:00Z"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => store.Add(Entry("same", "Second text.", "2026-10-30T10:00:00Z")));

        Assert.Contains("same", exception.Message, StringComparison.Ordinal);
        Assert.Single(store.Retrieve());
    }

    [Fact]
    public void Add_leaves_conversation_history_unchanged()
    {
        var history = new InMemoryConversationHistory();
        var store = new InMemoryMemoryStore();
        history.Replace([new { role = "user", parts = new object[] { new { text = "Recent question." } } }]);
        var storedMessage = history.Messages[0];

        store.Add(Entry("fact", "The user wants tests to avoid live Gemini API calls.", "2026-10-30T09:00:00Z"));

        Assert.Same(storedMessage, Assert.Single(history.Messages));
        Assert.Null(history.Summary);
        Assert.Equal("The user wants tests to avoid live Gemini API calls.", store.Find("fact")!.Text);
    }

    [Fact]
    public void Constructor_rejects_a_null_id()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new MemoryEntry(null!, "A stored fact.", DateTimeOffset.Parse("2026-10-30T09:00:00Z")));

        Assert.Equal("Id", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_id(string id)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new MemoryEntry(id, "A stored fact.", DateTimeOffset.Parse("2026-10-30T09:00:00Z")));

        Assert.Equal("Id", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_null_text()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new MemoryEntry("fact", null!, DateTimeOffset.Parse("2026-10-30T09:00:00Z")));

        Assert.Equal("Text", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_text(string text)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new MemoryEntry("fact", text, DateTimeOffset.Parse("2026-10-30T09:00:00Z")));

        Assert.Equal("Text", exception.ParamName);
    }

    [Fact]
    public void Find_rejects_a_null_id()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new InMemoryMemoryStore().Find(null!));

        Assert.Equal("id", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Find_rejects_a_blank_id(string id)
    {
        var exception = Assert.Throws<ArgumentException>(() => new InMemoryMemoryStore().Find(id));

        Assert.Equal("id", exception.ParamName);
    }

    private static MemoryEntry Entry(string id, string text, string createdAt)
    {
        return new MemoryEntry(id, text, DateTimeOffset.Parse(createdAt));
    }
}
