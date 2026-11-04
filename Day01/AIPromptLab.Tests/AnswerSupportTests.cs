namespace AIPromptLab.Tests;

public sealed class AnswerSupportTests
{
    [Fact]
    public void Evaluate_grounds_an_answer_when_every_claim_has_evidence()
    {
        var result = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            AnswerSupportExample.SupportedAnswer);

        Assert.True(result.IsGrounded);
        Assert.True(result.IsSupported);
        Assert.Empty(result.UnsupportedClaims);
        Assert.Equal(
            [
                new AnswerSupportResult.Claim("The application uses .NET 10", true),
                new AnswerSupportResult.Claim("HTTP retries respect the Retry-After header", true)
            ],
            result.Claims);
    }

    [Fact]
    public void Evaluate_reports_the_unmapped_five_retry_claim()
    {
        var result = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            AnswerSupportExample.UnsupportedAnswer);

        Assert.False(result.IsGrounded);
        Assert.Equal(
            [
                new AnswerSupportResult.Claim("The application uses .NET 10", true),
                new AnswerSupportResult.Claim("retries respect Retry-After", true),
                new AnswerSupportResult.Claim("all requests are retried five times", false)
            ],
            result.Claims);
        Assert.Equal(["all requests are retried five times"], result.UnsupportedClaims);
        Assert.DoesNotContain(
            AnswerSupportExample.Mappings,
            mapping => mapping.Claim.Contains("five", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_does_not_treat_keyword_overlap_as_evidence()
    {
        var context = "The application uses .NET 10. The words retries, Retry-After, and five appear here.";

        var result = AnswerSupport.Evaluate(context, AnswerSupportExample.UnsupportedAnswer);

        Assert.False(result.IsGrounded);
        Assert.Equal(
            [
                "retries respect Retry-After",
                "all requests are retried five times"
            ],
            result.UnsupportedClaims);
    }

    [Fact]
    public void Evaluate_rejects_a_mapped_claim_when_its_evidence_sentence_is_missing()
    {
        var result = AnswerSupport.Evaluate(
            "The application uses .NET 10. Unit tests use xUnit.",
            AnswerSupportExample.SupportedAnswer);

        Assert.False(result.IsGrounded);
        Assert.Equal(["HTTP retries respect the Retry-After header"], result.UnsupportedClaims);
    }

    [Fact]
    public void Evaluate_grounds_the_xunit_claim_only_when_its_evidence_sentence_is_present()
    {
        var grounded = AnswerSupport.Evaluate(AnswerSupportExample.Context, "Unit tests use xUnit.");
        var keywordOnly = AnswerSupport.Evaluate(
            "Someone mentioned xUnit, unit tests, and use.",
            "Unit tests use xUnit.");

        Assert.True(grounded.IsGrounded);
        Assert.False(keywordOnly.IsGrounded);
        Assert.Equal(["Unit tests use xUnit"], keywordOnly.UnsupportedClaims);
    }

    [Fact]
    public void Evaluate_repeats_the_same_decision_for_the_fixed_answers()
    {
        var supported = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            AnswerSupportExample.SupportedAnswer);
        var supportedAgain = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            AnswerSupportExample.SupportedAnswer);
        var unsupported = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            AnswerSupportExample.UnsupportedAnswer);
        var unsupportedAgain = AnswerSupport.Evaluate(
            AnswerSupportExample.Context,
            AnswerSupportExample.UnsupportedAnswer);

        Assert.Equal(supported.IsGrounded, supportedAgain.IsGrounded);
        Assert.Equal(supported.Claims, supportedAgain.Claims);
        Assert.Equal(unsupported.IsGrounded, unsupportedAgain.IsGrounded);
        Assert.Equal(unsupported.UnsupportedClaims, unsupportedAgain.UnsupportedClaims);
    }

    [Fact]
    public void Mappings_bind_controlled_claims_to_evidence_sentences()
    {
        Assert.Contains(
            new ClaimEvidence("The application uses .NET 10", "The application uses .NET 10."),
            AnswerSupportExample.Mappings);
        Assert.Contains(
            new ClaimEvidence("retries respect Retry-After", "HTTP retries respect the Retry-After header."),
            AnswerSupportExample.Mappings);
        Assert.Contains("The application uses .NET 10.", AnswerSupportExample.Context, StringComparison.Ordinal);
        Assert.Contains(
            "HTTP retries respect the Retry-After header.",
            AnswerSupportExample.Context,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_rejects_a_duplicate_claim_mapping()
    {
        var mappings = new List<ClaimEvidence>
        {
            new("The application uses .NET 10", "The application uses .NET 10."),
            new("The application uses .NET 10.", "The application uses .NET 10.")
        };

        var exception = Assert.Throws<ArgumentException>(
            () => AnswerSupport.Evaluate(AnswerSupportExample.Context, "The application uses .NET 10.", mappings));

        Assert.Equal("mappings", exception.ParamName);
        Assert.Contains("The application uses .NET 10", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Example_keeps_the_fixed_question_and_answers()
    {
        Assert.Equal(
            "Which framework does the application use, and how are retry delays handled?",
            AnswerSupportExample.Question);
        Assert.Contains(".NET 10", AnswerSupportExample.SupportedAnswer, StringComparison.Ordinal);
        Assert.Contains("Retry-After", AnswerSupportExample.SupportedAnswer, StringComparison.Ordinal);
        Assert.Contains("five times", AnswerSupportExample.UnsupportedAnswer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "The application uses .NET 10.")]
    [InlineData("", "The application uses .NET 10.")]
    [InlineData("   ", "The application uses .NET 10.")]
    public void Evaluate_rejects_a_blank_context(string? context, string answer)
    {
        Assert.ThrowsAny<ArgumentException>(() => AnswerSupport.Evaluate(context!, answer));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    public void Evaluate_rejects_an_answer_without_a_claim(string? answer)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => AnswerSupport.Evaluate(AnswerSupportExample.Context, answer!));
    }

    [Fact]
    public void Evaluate_rejects_a_null_mapping_list()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => AnswerSupport.Evaluate(
                AnswerSupportExample.Context,
                AnswerSupportExample.SupportedAnswer,
                null!));

        Assert.Equal("mappings", exception.ParamName);
    }
}
