using System.Text.Json;

namespace AIPromptLab.Tests;

public sealed class PromptLabSettingsTests
{
    [Fact]
    public void Read_uses_the_configured_limits()
    {
        using var document = JsonDocument.Parse("""
            {
              "ConversationHistory": "InMemory",
              "MaxContextTurns": 5,
              "MaxFunctionRounds": 2,
              "Model": "gemini-test",
              "RequestTimeoutMinutes": 3
            }
            """);

        var settings = PromptLabSettings.Read(document.RootElement);

        Assert.Equal(5, settings.MaxContextTurns);
        Assert.Equal(2, settings.MaxFunctionRounds);
        Assert.Equal("gemini-test", settings.Model);
        Assert.Equal(TimeSpan.FromMinutes(3), settings.RequestTimeout);
    }

    [Fact]
    public void Read_uses_defaults_when_the_limits_are_omitted()
    {
        using var document = JsonDocument.Parse("""{"ConversationHistory":"InMemory"}""");

        var settings = PromptLabSettings.Read(document.RootElement);

        Assert.Equal(PromptLabSettings.DefaultMaxContextTurns, settings.MaxContextTurns);
        Assert.Equal(PromptLabSettings.DefaultMaxFunctionRounds, settings.MaxFunctionRounds);
        Assert.Equal(PromptLabSettings.DefaultModel, settings.Model);
        Assert.Equal(TimeSpan.FromMinutes(PromptLabSettings.DefaultRequestTimeoutMinutes), settings.RequestTimeout);
    }

    [Theory]
    [InlineData("{\"MaxContextTurns\":0}")]
    [InlineData("{\"MaxContextTurns\":-1}")]
    [InlineData("{\"MaxContextTurns\":1.5}")]
    [InlineData("{\"MaxContextTurns\":\"6\"}")]
    [InlineData("{\"MaxFunctionRounds\":0}")]
    [InlineData("{\"MaxFunctionRounds\":\"4\"}")]
    [InlineData("{\"RequestTimeoutMinutes\":0}")]
    [InlineData("{\"Model\":\"\"}")]
    [InlineData("{\"Model\":4}")]
    public void Read_rejects_an_invalid_setting(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Throws<InvalidOperationException>(() => PromptLabSettings.Read(document.RootElement));
    }
}
