using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Serilog;

public enum GeminiClientError
{
    MissingApiKey,
    InvalidApiKey,
    NetworkFailure,
    RateLimitExceeded,
    ServerRejection,
    EmptyResponse,
    InvalidResponse
}

public sealed class GeminiClientException : Exception
{
    public GeminiClientException(GeminiClientError error)
        : this(error, innerException: null)
    {
    }

    public GeminiClientException(GeminiClientError error, Exception? innerException)
        : base(error.ToString(), innerException)
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

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

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

    public async Task<LearningSummary> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
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
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        title = new { type = "STRING" },
                        summary = new { type = "STRING" },
                        keyPoints = new
                        {
                            type = "ARRAY",
                            items = new { type = "STRING" }
                        },
                        difficulty = new
                        {
                            type = "STRING",
                            @enum = new[]
                            {
                                LearningSummary.Beginner,
                                LearningSummary.Intermediate,
                                LearningSummary.Advanced
                            }
                        }
                    },
                    required = new[] { "title", "summary", "keyPoints", "difficulty" }
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

            var apiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(raw, ResponseJsonOptions)
                ?? throw new GeminiClientException(GeminiClientError.EmptyResponse);
            var text = Redact(apiResponse.ReadModelText());
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new GeminiClientException(GeminiClientError.EmptyResponse);
            }

            return DeserializeSummary(text);
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
        catch (JsonException ex)
        {
            throw new GeminiClientException(GeminiClientError.EmptyResponse, ex);
        }
    }

    private static LearningSummary DeserializeSummary(string text)
    {
        try
        {
            return LearningSummary.Deserialize(text);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            Log.Error(ex, "The API response could not be deserialized to a learning summary.");
            throw new GeminiClientException(GeminiClientError.InvalidResponse, ex);
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
}
