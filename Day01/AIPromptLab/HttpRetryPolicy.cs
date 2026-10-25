using System.Net;

public interface IHttpRetryDelay
{
    Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public readonly record struct HttpRetryResult(HttpStatusCode StatusCode, string Body);

public sealed class HttpRetryPolicy
{
    public const int MaxRetries = 2;

    public static readonly TimeSpan InitialDelay = TimeSpan.FromMilliseconds(200);

    private readonly IHttpRetryDelay _delay;

    public HttpRetryPolicy()
        : this(new TaskHttpRetryDelay())
    {
    }

    public HttpRetryPolicy(IHttpRetryDelay delay)
    {
        ArgumentNullException.ThrowIfNull(delay);
        _delay = delay;
    }

    public static TimeSpan GetDelay(int retryNumber)
    {
        if (retryNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryNumber),
                "The retry number must be greater than zero.");
        }

        return InitialDelay * (1 << (retryNumber - 1));
    }

    public async Task<HttpRetryResult> SendAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);
        var maxAttempts = MaxRetries + 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var response = await send(cancellationToken);
                var responseBody = response.Content is null
                    ? string.Empty
                    : await response.Content.ReadAsStringAsync(cancellationToken);
                if (attempt == maxAttempts || !IsRetryableStatus(response.StatusCode))
                {
                    return new HttpRetryResult(response.StatusCode, responseBody);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsTransientNetworkFailure(exception) && attempt < maxAttempts)
            {
            }

            await _delay.WaitAsync(GetDelay(attempt), cancellationToken);
        }

        throw new InvalidOperationException("The HTTP retry loop ended without a response.");
    }

    public static bool IsRetryableStatus(HttpStatusCode statusCode)
    {
        var status = (int)statusCode;
        return statusCode == HttpStatusCode.TooManyRequests || status is >= 500 and <= 599;
    }

    private static bool IsTransientNetworkFailure(Exception exception)
    {
        return exception is HttpRequestException or OperationCanceledException;
    }

    private sealed class TaskHttpRetryDelay : IHttpRetryDelay
    {
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }
    }
}
