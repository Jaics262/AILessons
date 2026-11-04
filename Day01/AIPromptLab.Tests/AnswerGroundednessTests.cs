namespace AIPromptLab.Tests;

public sealed class AnswerGroundednessTests
{
    [Fact]
    public void Evaluate_accepts_an_answer_whose_claims_are_fully_supported()
    {
        var result = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            AnswerSupportExample.SupportedAnswer);

        Assert.True(result.IsGrounded);
        Assert.Empty(result.UnsupportedClaims);
        Assert.All(result.Claims, claim => Assert.True(claim.IsSupported));
        Assert.Equal(
            [
                "The application uses .NET 10",
                "HTTP retries respect the Retry-After header"
            ],
            result.Claims.Select(claim => claim.Text));
    }

    [Fact]
    public void Evaluate_reports_one_unsupported_claim()
    {
        var result = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            "The application uses .NET 10, and all requests are retried five times.");

        Assert.False(result.IsGrounded);
        Assert.Equal(
            [
                new AnswerSupportResult.Claim("The application uses .NET 10", true),
                new AnswerSupportResult.Claim("all requests are retried five times", false)
            ],
            result.Claims);
        Assert.Equal(["all requests are retried five times"], result.UnsupportedClaims);
    }

    [Fact]
    public void Evaluate_reports_every_unsupported_claim()
    {
        var result = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            "The application uses .NET 10, all requests are retried five times, and every prompt calls Gemini.");

        Assert.False(result.IsGrounded);
        Assert.Equal(
            [
                new AnswerSupportResult.Claim("The application uses .NET 10", true),
                new AnswerSupportResult.Claim("all requests are retried five times", false),
                new AnswerSupportResult.Claim("every prompt calls Gemini", false)
            ],
            result.Claims);
        Assert.Equal(
            [
                "all requests are retried five times",
                "every prompt calls Gemini"
            ],
            result.UnsupportedClaims);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_rejects_an_empty_context(string context)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AnswerSupport.Evaluate(context, AnswerSupportExample.SupportedAnswer));

        Assert.Equal("context", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_rejects_an_empty_answer(string answer)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AnswerSupport.Evaluate(AnswerSupportExample.Context, answer));

        Assert.Equal("answer", exception.ParamName);
    }

    [Fact]
    public void Evaluate_rejects_a_relevant_answer_that_adds_an_unsupported_fact()
    {
        var result = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            AnswerSupportExample.UnsupportedAnswer);

        Assert.Equal(
            "Which framework does the application use, and how are retry delays handled?",
            AnswerSupportExample.Question);
        Assert.False(result.IsGrounded);
        Assert.Equal(
            [
                new AnswerSupportResult.Claim("The application uses .NET 10", true),
                new AnswerSupportResult.Claim("retries respect Retry-After", true),
                new AnswerSupportResult.Claim("all requests are retried five times", false)
            ],
            result.Claims);
        Assert.Equal(["all requests are retried five times"], result.UnsupportedClaims);
    }
}
