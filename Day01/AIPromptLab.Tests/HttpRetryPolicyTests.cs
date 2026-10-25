using System.Net;
using System.Text;

namespace AIPromptLab.Tests;

public sealed class HttpRetryPolicyTests
{
    [Fact]
    public void GetDelay_doubles_for_each_retry()
    {
        Assert.Equal(HttpRetryPolicy.InitialDelay, HttpRetryPolicy.GetDelay(1));
        Assert.Equal(HttpRetryPolicy.InitialDelay * 2, HttpRetryPolicy.GetDelay(2));
    }

    [Fact]
    public async Task SendAsync_retries_429_and_5xx_then_returns_the_final_response()
    {
        var statuses = new Queue<HttpStatusCode>(
        [
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.OK
        ]);
        var sends = 0;
        var delay = new PolicyRetryDelay();
        var policy = new HttpRetryPolicy(delay);

        var result = await policy.SendAsync(_ =>
        {
            sends++;
            return Task.FromResult(Response(statuses.Dequeue(), "{\"ok\":true}"));
        }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("{\"ok\":true}", result.Body);
        Assert.Equal(3, sends);
        Assert.Equal([HttpRetryPolicy.InitialDelay, HttpRetryPolicy.InitialDelay * 2], delay.Delays);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SendAsync_does_not_retry_ordinary_client_errors(HttpStatusCode statusCode)
    {
        var sends = 0;
        var delay = new PolicyRetryDelay();
        var policy = new HttpRetryPolicy(delay);

        var result = await policy.SendAsync(_ =>
        {
            sends++;
            return Task.FromResult(Response(statusCode));
        }, CancellationToken.None);

        Assert.Equal(statusCode, result.StatusCode);
        Assert.Equal(1, sends);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task SendAsync_returns_the_last_5xx_after_two_retries()
    {
        var sends = 0;
        var delay = new PolicyRetryDelay();
        var policy = new HttpRetryPolicy(delay);

        var result = await policy.SendAsync(_ =>
        {
            sends++;
            return Task.FromResult(Response(HttpStatusCode.ServiceUnavailable, "{\"error\":\"unavailable\"}"));
        }, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.Equal("{\"error\":\"unavailable\"}", result.Body);
        Assert.Equal(HttpRetryPolicy.MaxRetries + 1, sends);
        Assert.Equal(HttpRetryPolicy.MaxRetries, delay.Delays.Count);
    }

    [Fact]
    public async Task SendAsync_retries_a_connection_failure_and_a_timeout()
    {
        var sends = 0;
        var delay = new PolicyRetryDelay();
        var policy = new HttpRetryPolicy(delay);

        var result = await policy.SendAsync(_ =>
        {
            sends++;
            if (sends == 1)
            {
                throw new HttpRequestException("Could not connect.");
            }

            if (sends == 2)
            {
                throw new TaskCanceledException("The request timed out.");
            }

            return Task.FromResult(Response(HttpStatusCode.OK, "done"));
        }, CancellationToken.None);

        Assert.Equal("done", result.Body);
        Assert.Equal(3, sends);
        Assert.Equal([HttpRetryPolicy.InitialDelay, HttpRetryPolicy.InitialDelay * 2], delay.Delays);
    }

    [Fact]
    public async Task SendAsync_does_not_retry_when_the_caller_cancels()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sends = 0;
        var delay = new PolicyRetryDelay();
        var policy = new HttpRetryPolicy(delay);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => policy.SendAsync(_ =>
        {
            sends++;
            throw new TaskCanceledException();
        }, cancellation.Token));

        Assert.Equal(1, sends);
        Assert.Empty(delay.Delays);
    }

    [Fact]
    public async Task SendAsync_stops_when_cancellation_is_requested_during_backoff()
    {
        using var cancellation = new CancellationTokenSource();
        var sends = 0;
        var policy = new HttpRetryPolicy(new CancelOnWait(cancellation));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => policy.SendAsync(_ =>
        {
            sends++;
            return Task.FromResult(Response(HttpStatusCode.InternalServerError));
        }, cancellation.Token));

        Assert.Equal(1, sends);
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string body = "{}")
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class PolicyRetryDelay : IHttpRetryDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class CancelOnWait : IHttpRetryDelay
    {
        private readonly CancellationTokenSource _cancellation;

        public CancelOnWait(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            _cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
