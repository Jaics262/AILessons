using System.Net;

namespace AIPromptLab.Tests;

public sealed class PromptLabErrorsTests
{
    [Theory]
    [InlineData(GeminiClientError.RateLimitExceeded, HttpStatusCode.TooManyRequests)]
    [InlineData(GeminiClientError.RequestTimeout, null)]
    [InlineData(GeminiClientError.ServerRejection, HttpStatusCode.ServiceUnavailable)]
    public void Describe_tells_the_user_the_service_is_temporarily_unavailable(
        GeminiClientError error,
        HttpStatusCode? statusCode)
    {
        var message = PromptLabErrors.Describe(new GeminiClientException(error) { StatusCode = statusCode });

        Assert.Equal(PromptLabErrors.TemporarilyUnavailable, message);
        Assert.DoesNotContain("unit-test-api-key", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_keeps_a_rejected_request_distinct_from_a_server_fault()
    {
        var message = PromptLabErrors.Describe(new GeminiClientException(GeminiClientError.ServerRejection)
        {
            StatusCode = HttpStatusCode.BadRequest
        });

        Assert.Equal("The AI service could not accept that request. Please try again.", message);
    }
}
