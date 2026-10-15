using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public enum GeminiClientError
{
    MissingApiKey,
    InvalidApiKey,
    NetworkFailure,
    RateLimitExceeded,
    ServerRejection,
    EmptyResponse
}

public sealed class GeminiClientException : Exception
{
    public GeminiClientException(GeminiClientError error)
        : base(error.ToString())
    {
        Error = error;
    }

    public GeminiClientError Error { get; }
}

public sealed class GeminiLlmClient : IDisposable
{
    private const string Model = "gemini-3.8-flash";
    private const string ApiKeyVariable = "GEMINI_API_KEY";
    private const string ApiKeyHeader = "x-goog-api-key";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _apiKey;

    public GeminiLlmClient()
        : this(httpClient: null)
    {
    }

    public GeminiLlmClient(HttpClient? httpClient)
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new GeminiClientException(GeminiClientError.MissingApiKey);
        }

        _apiKey = apiKey;

        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
                Timeout = TimeSpan.FromMinutes(5)
            };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }

        _httpClient.DefaultRequestHeaders.Remove(ApiKeyHeader);
        _httpClient.DefaultRequestHeaders.Add(ApiKeyHeader, _apiKey);
    }

    public async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("The prompt cannot be empty.", nameof(prompt));
        }

        var requestUri = $"v1beta/models/{Model}:generateContent";
        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(requestUri, body, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new GeminiClientException(ClassifyFailure(response.StatusCode, raw));
            }

            var text = Redact(ParseResponseText(raw));
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new GeminiClientException(GeminiClientError.EmptyResponse);
            }

            return text;
        }
        catch (GeminiClientException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new GeminiClientException(GeminiClientError.NetworkFailure);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
           throw new GeminiClientException(GeminiClientError.NetworkFailure);
        }
        catch (JsonException)
        {
            throw new GeminiClientException(GeminiClientError.EmptyResponse);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static GeminiClientError ClassifyFailure(HttpStatusCode statusCode, string raw)
    {
        if (statusCode == HttpStatusCode.TooManyRequests || BodyIndicates(raw, "RESOURCE_EXHAUSTED"))
        {
            return GeminiClientError.RateLimitExceeded;
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ||
            BodyIndicates(raw, "API_KEY_INVALID") ||
            BodyIndicates(raw, "API key not valid"))
        {
            return GeminiClientError.InvalidApiKey;
        }

        return GeminiClientError.ServerRejection;
    }

    private static bool BodyIndicates(string raw, string marker)
    {
        return !string.IsNullOrEmpty(raw) &&
               raw.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }

    private string Redact(string value)
    {
        return value.Replace(_apiKey, string.Empty, StringComparison.Ordinal);
    }

    private static string ParseResponseText(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        if (root.TryGetProperty("promptFeedback", out var feedback) &&
            feedback.TryGetProperty("blockReason", out _))
        {
            throw new GeminiClientException(GeminiClientError.EmptyResponse);
        }

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            throw new GeminiClientException(GeminiClientError.EmptyResponse);
        }

        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            throw new GeminiClientException(GeminiClientError.EmptyResponse);
        }

        var texts = new List<string>();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textElement))
            {
                var text = textElement.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    texts.Add(text);
                }
            }
        }

        if (texts.Count == 0)
        {
            throw new GeminiClientException(GeminiClientError.EmptyResponse);
        }

        return string.Concat(texts);
    }
}
