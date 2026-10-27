using Microsoft.Extensions.Configuration;

namespace AIPromptLab.Tests;

public sealed class PromptLabSettingsTests
{
    [Fact]
    public void Read_uses_the_configured_limits()
    {
        var settings = PromptLabSettings.Read(Configuration(
            ("ConversationHistory", "InMemory"),
            ("MaxContextTurns", "5"),
            ("MaxHistoryTokens", "1200"),
            ("MaxFunctionRounds", "2"),
            ("Model", "gemini-test"),
            ("RequestTimeoutMinutes", "3")));

        Assert.Equal(5, settings.MaxContextTurns);
        Assert.Equal(1200, settings.MaxHistoryTokens);
        Assert.Equal(2, settings.MaxFunctionRounds);
        Assert.Equal("gemini-test", settings.Model);
        Assert.Equal(TimeSpan.FromMinutes(3), settings.RequestTimeout);
    }

    [Fact]
    public void Read_uses_defaults_when_the_limits_are_omitted()
    {
        var settings = PromptLabSettings.Read(Configuration(("ConversationHistory", "InMemory")));

        Assert.Equal(PromptLabSettings.DefaultMaxContextTurns, settings.MaxContextTurns);
        Assert.Equal(PromptLabSettings.DefaultMaxHistoryTokens, settings.MaxHistoryTokens);
        Assert.Equal(4000, settings.MaxHistoryTokens);
        Assert.Equal(PromptLabSettings.DefaultMaxFunctionRounds, settings.MaxFunctionRounds);
        Assert.Equal(PromptLabSettings.DefaultModel, settings.Model);
        Assert.Equal(TimeSpan.FromMinutes(PromptLabSettings.DefaultRequestTimeoutMinutes), settings.RequestTimeout);
    }

    [Fact]
    public void Read_lets_a_later_source_override_an_earlier_value()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaxContextTurns"] = "6",
                ["Model"] = "gemini-3.8-flash"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MaxContextTurns"] = "2"
            })
            .Build();

        var settings = PromptLabSettings.Read(configuration);

        Assert.Equal(2, settings.MaxContextTurns);
        Assert.Equal("gemini-3.8-flash", settings.Model);
    }

    [Theory]
    [InlineData("MaxContextTurns", "0")]
    [InlineData("MaxContextTurns", "-1")]
    [InlineData("MaxContextTurns", "1.5")]
    [InlineData("MaxContextTurns", "six")]
    [InlineData("MaxHistoryTokens", "0")]
    [InlineData("MaxHistoryTokens", "-1")]
    [InlineData("MaxHistoryTokens", "1.5")]
    [InlineData("MaxHistoryTokens", "four thousand")]
    [InlineData("MaxFunctionRounds", "0")]
    [InlineData("MaxFunctionRounds", "4.2")]
    [InlineData("RequestTimeoutMinutes", "0")]
    [InlineData("Model", "")]
    [InlineData("Model", "   ")]
    public void Read_rejects_an_invalid_setting(string name, string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => PromptLabSettings.Read(Configuration((name, value))));

        Assert.Contains(name, exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => pair.Value))
            .Build();
    }
}
