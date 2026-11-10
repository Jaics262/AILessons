namespace AIPromptLab.Tests;

public sealed class AnswerCitationTests
{
    [Fact]
    public void Cite_names_the_evidence_sentence_for_every_supported_claim()
    {
        var result = AnswerCitation.Cite(
            AnswerSupportExample.Context,
            AnswerSupportExample.SupportedAnswer);

        Assert.True(result.IsFullyCited);
        Assert.Empty(result.UncitedClaims);
        Assert.Equal(
            [
                new CitedClaim(
                    "The application uses .NET 10",
                    "The application uses .NET 10."),
                new CitedClaim(
                    "HTTP retries respect the Retry-After header",
                    "HTTP retries respect the Retry-After header.")
            ],
            result.Claims);
        Assert.Equal(
            string.Join(
                '\n',
                "Answer citations: COMPLETE",
                "- The application uses .NET 10: The application uses .NET 10.",
                "- HTTP retries respect the Retry-After header: HTTP retries respect the Retry-After header."),
            result.Report);
    }

    [Fact]
    public void Cite_uses_the_mapped_sentence_for_an_alias()
    {
        var result = AnswerCitation.Cite(
            AnswerSupportExample.Context,
            "retries respect Retry-After.");

        var claim = Assert.Single(result.Claims);
        Assert.True(result.IsFullyCited);
        Assert.Equal("retries respect Retry-After", claim.Text);
        Assert.Equal("HTTP retries respect the Retry-After header.", claim.Evidence);
    }

    [Fact]
    public void Cite_leaves_the_five_retry_claim_without_a_citation()
    {
        var result = AnswerCitation.Cite(
            AnswerSupportExample.Context,
            AnswerSupportExample.UnsupportedAnswer);

        Assert.False(result.IsFullyCited);
        Assert.Equal(
            [
                new CitedClaim(
                    "The application uses .NET 10",
                    "The application uses .NET 10."),
                new CitedClaim(
                    "retries respect Retry-After",
                    "HTTP retries respect the Retry-After header."),
                new CitedClaim("all requests are retried five times", null)
            ],
            result.Claims);
        Assert.Equal(["all requests are retried five times"], result.UncitedClaims);
        Assert.Contains(
            "- all requests are retried five times: (no citation)",
            result.Report,
            StringComparison.Ordinal);
        Assert.StartsWith("Answer citations: INCOMPLETE", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void Cite_reports_every_uncited_claim()
    {
        var result = AnswerCitation.Cite(
            AnswerSupportExample.Context,
            "The application uses .NET 10, all requests are retried five times, and every prompt calls Gemini.");

        Assert.False(result.IsFullyCited);
        Assert.Equal(
            [
                "all requests are retried five times",
                "every prompt calls Gemini"
            ],
            result.UncitedClaims);
        Assert.Equal(
            string.Join(
                '\n',
                "Answer citations: INCOMPLETE",
                "- The application uses .NET 10: The application uses .NET 10.",
                "- all requests are retried five times: (no citation)",
                "- every prompt calls Gemini: (no citation)"),
            result.Report);
    }

    [Fact]
    public void Cite_ignores_the_same_words_when_no_mapping_names_the_claim()
    {
        var context = AnswerSupportExample.Context + " all requests are retried five times.";

        var result = AnswerCitation.Cite(context, "all requests are retried five times");

        var claim = Assert.Single(result.Claims);
        Assert.False(claim.IsCited);
        Assert.Null(claim.Evidence);
        Assert.Equal(["all requests are retried five times"], result.UncitedClaims);
    }

    [Fact]
    public void Cite_withholds_a_citation_when_the_mapped_sentence_is_missing()
    {
        var result = AnswerCitation.Cite(
            "The application uses .NET 10.",
            "The application uses .NET 10, and HTTP retries respect the Retry-After header.");

        Assert.False(result.IsFullyCited);
        Assert.Equal(
            [
                new CitedClaim(
                    "The application uses .NET 10",
                    "The application uses .NET 10."),
                new CitedClaim("HTTP retries respect the Retry-After header", null)
            ],
            result.Claims);
    }

    [Fact]
    public void Cite_repeats_the_same_report_for_the_same_answer()
    {
        var first = AnswerCitation.Cite(
            AnswerSupportExample.Context,
            AnswerSupportExample.UnsupportedAnswer);
        var second = AnswerCitation.Cite(
            AnswerSupportExample.Context,
            AnswerSupportExample.UnsupportedAnswer);

        Assert.Equal(first.Report, second.Report);
        Assert.Equal(first.Claims, second.Claims);
        Assert.Equal(first.UncitedClaims, second.UncitedClaims);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cite_rejects_an_empty_context(string context)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AnswerCitation.Cite(context, AnswerSupportExample.SupportedAnswer));

        Assert.Equal("context", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cite_rejects_an_empty_answer(string answer)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AnswerCitation.Cite(AnswerSupportExample.Context, answer));

        Assert.Equal("answer", exception.ParamName);
    }

    [Fact]
    public void CitedClaim_rejects_blank_text_and_blank_evidence()
    {
        Assert.Throws<ArgumentException>(() => new CitedClaim("  ", "The application uses .NET 10."));
        Assert.Throws<ArgumentException>(() => new CitedClaim("The application uses .NET 10", "  "));
    }
}
