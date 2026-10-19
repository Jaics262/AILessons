using System.Net;
using System.Text;

namespace AIPromptLab.Tests;

public sealed class GeminiLlmClientTests
{
    private const string ApiKeyVariable = "GEMINI_API_KEY";
    private const string ApiKey = "unit-test-api-key";
    private const string Prompt = "Explain gravity in one sentence.";

    [Fact]
    public void Constructor_when_api_key_is_missing_throws_missing_api_key()
    {
        using (new ApiKeyScope(null))
        {
            var exception = Assert.Throws<GeminiClientException>(() => new GeminiLlmClient());

            Assert.Equal(GeminiClientError.MissingApiKey, exception.Error);
            Assert.DoesNotContain(ApiKey, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Constructor_when_api_key_is_blank_throws_missing_api_key()
    {
        using (new ApiKeyScope("   "))
        {
            var exception = Assert.Throws<GeminiClientException>(() => new GeminiLlmClient());

            Assert.Equal(GeminiClientError.MissingApiKey, exception.Error);
        }
    }

    [Fact]
    public async Task SendPromptAsync_rejects_a_blank_prompt_without_calling_the_api()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessJson("unused"));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.SendPromptAsync("  "));

            Assert.Equal("prompt", exception.ParamName);
            Assert.Null(handler.Request);
        }
    }

    [Fact]
    public async Task SendPromptAsync_posts_the_prompt_and_returns_the_learning_summary()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(
                summary: "Hello world",
                keyPoints: ["Mass attracts mass", "Distance weakens the pull", "Weight depends on mass"])));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var response = await client.SendPromptAsync(Prompt);

            Assert.NotNull(response.Summary);
            Assert.Empty(response.FunctionCalls);
            Assert.Empty(response.Totals);
            Assert.Single(handler.Bodies);
            Assert.Equal("Gravity", response.Summary.Title);
            Assert.Equal("Hello world", response.Summary.Summary);
            Assert.Equal(LearningSummary.Beginner, response.Summary.Difficulty);
            Assert.Equal(
                ["Mass attracts mass", "Distance weakens the pull", "Weight depends on mass"],
                response.Summary.KeyPoints);
            Assert.NotNull(handler.Request);
            Assert.Equal(HttpMethod.Post, handler.Request.Method);
            Assert.Equal(
                "/v1beta/models/gemini-3.8-flash:generateContent",
                handler.Request.RequestUri!.AbsolutePath);
            Assert.DoesNotContain(ApiKey, handler.Request.RequestUri.Query, StringComparison.Ordinal);
            Assert.Equal(ApiKey, Assert.Single(handler.Request.Headers.GetValues("x-goog-api-key")));
            Assert.Contains(Prompt, handler.Body, StringComparison.Ordinal);
            Assert.Contains("\"name\":\"calculate_total\"", handler.Body, StringComparison.Ordinal);
            Assert.Contains("\"description\":\"Calculate the tool price from quantity and unit price\"", handler.Body, StringComparison.Ordinal);
            Assert.Contains("\"quantity\":{\"type\":\"INTEGER\"}", handler.Body, StringComparison.Ordinal);
            Assert.Contains("\"unitPrice\":{\"type\":\"NUMBER\"}", handler.Body, StringComparison.Ordinal);
            Assert.Contains("\"required\":[\"quantity\",\"unitPrice\"]", handler.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("responseMimeType", handler.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(ApiKey, handler.Body, StringComparison.Ordinal);
            AssertSummaryDoesNotContainApiKey(response.Summary);
        }
    }

    [Fact]
    public async Task SendPromptAsync_removes_the_api_key_from_the_response_text()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: $"before {ApiKey} after")));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var response = await client.SendPromptAsync(Prompt);

            Assert.NotNull(response.Summary);
            Assert.Equal("before  after", response.Summary.Summary);
            AssertSummaryDoesNotContainApiKey(response.Summary);
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"title\":\"Only title\"}")]
    [InlineData("{\"title\":\"T\",\"summary\":\"S\",\"keyPoints\":[\"a\"],\"difficulty\":\"Expert\"}")]
    [InlineData("null")]
    public async Task SendPromptAsync_classifies_an_invalid_learning_summary(string text)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessJson(text));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidResponse, exception.Error);
            Assert.DoesNotContain(ApiKey, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_calls_CalculateTotal_and_returns_the_final_summary()
    {
        var calls = 0;
        var functionCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":2,"unitPrice":9.5}},"thoughtSignature":"sig-abc"}]}}]}
            """;
        var finalSummary = SuccessJson(LearningSummaryJson(summary: "The total is 19.0"));
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ == 0 ? functionCall : finalSummary;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var response = await client.SendPromptAsync(Prompt);

            Assert.Equal(2, handler.Bodies.Count);
            Assert.DoesNotContain("responseMimeType", handler.Bodies[0], StringComparison.Ordinal);
            Assert.Contains("\"functionResponse\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-abc\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"name\":\"calculate_total\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"total\":19.0", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"responseMimeType\":\"application/json\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"minItems\":3", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"maxItems\":3", handler.Bodies[1], StringComparison.Ordinal);
            Assert.DoesNotContain("functionDeclarations", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains(Prompt, handler.Bodies[1], StringComparison.Ordinal);
            Assert.NotNull(response.Summary);
            Assert.Equal("The total is 19.0", response.Summary.Summary);
            Assert.Equal(19.0m, Assert.Single(response.Totals));
        }
    }

    [Fact]
    public async Task SendPromptAsync_returns_the_summary_when_the_response_has_no_function_call()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: "No tool was needed.")));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var response = await client.SendPromptAsync(Prompt);

            Assert.Single(handler.Bodies);
            Assert.Empty(response.Totals);
            Assert.Empty(response.FunctionCalls);
            Assert.NotNull(response.Summary);
            Assert.Equal("No tool was needed.", response.Summary.Summary);
        }
    }

    [Fact]
    public async Task SendPromptAsync_requests_a_learning_summary_when_the_model_text_is_not_json()
    {
        var calls = 0;
        var prose = SuccessJson("```json {\"topic\":\"Umbraco\"} ```");
        var finalSummary = SuccessJson(LearningSummaryJson(summary: "Umbraco is a content management system."));
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ == 0 ? prose : finalSummary;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var response = await client.SendPromptAsync(Prompt);

            Assert.Equal(2, handler.Bodies.Count);
            Assert.DoesNotContain("responseMimeType", handler.Bodies[0], StringComparison.Ordinal);
            Assert.Contains("\"responseMimeType\":\"application/json\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.DoesNotContain("functionDeclarations", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("Umbraco", handler.Bodies[1], StringComparison.Ordinal);
            Assert.NotNull(response.Summary);
            Assert.Equal("Umbraco is a content management system.", response.Summary.Summary);
            Assert.Empty(response.Totals);
        }
    }

    [Fact]
    public async Task SendPromptAsync_executes_every_function_call_in_one_response()
    {
        var calls = 0;
        var functionCalls = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"id":"c1","name":"calculate_total","args":{"quantity":2,"unitPrice":9.5}},"thoughtSignature":"sig-1"},{"functionCall":{"id":"c2","name":"calculate_total","args":{"quantity":1,"unitPrice":5}},"thoughtSignature":"sig-2"}]}}]}
            """;
        var finalSummary = SuccessJson(LearningSummaryJson(summary: "Both totals are included."));
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ == 0 ? functionCalls : finalSummary;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var response = await client.SendPromptAsync(Prompt);

            Assert.Equal(2, handler.Bodies.Count);
            Assert.Equal(2, CountOccurrences(handler.Bodies[1], "\"functionResponse\""));
            Assert.Contains("\"id\":\"c1\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"id\":\"c2\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-1\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-2\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"total\":19.0", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"total\":5", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Equal([19.0m, 5.0m], response.Totals);
            Assert.NotNull(response.Summary);
            Assert.Equal("Both totals are included.", response.Summary.Summary);
        }
    }

    [Fact]
    public async Task SendPromptAsync_executes_a_function_call_when_the_response_also_contains_text()
    {
        var calls = 0;
        var functionCall = """
            {"candidates":[{"content":{"parts":[{"text":"I will calculate that.","thoughtSignature":"sig-text"},{"functionCall":{"name":"calculate_total","args":{"quantity":3,"unitPrice":2.5}},"thoughtSignature":"sig-abc"}]}}]}
            """;
        var finalSummary = SuccessJson(LearningSummaryJson(summary: "Three notebooks cost 7.50."));
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ == 0 ? functionCall : finalSummary;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var response = await client.SendPromptAsync(Prompt);

            Assert.Equal(2, handler.Bodies.Count);
            Assert.Contains("\"text\":\"I will calculate that.\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-text\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"total\":7.5", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Equal(7.5m, Assert.Single(response.Totals));
            Assert.NotNull(response.Summary);
            Assert.Equal("Three notebooks cost 7.50.", response.Summary.Summary);
        }
    }

    [Fact]
    public async Task SendPromptAsync_rejects_the_response_when_any_function_call_is_invalid()
    {
        const string body = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":2,"unitPrice":9.5}}},{"functionCall":{"name":"calculate_total","args":{"quantity":0,"unitPrice":5}}}]}}]}
            """;
        var handler = new RecordingHandler(HttpStatusCode.OK, body);

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidFunctionCall, exception.Error);
            Assert.Single(handler.Bodies);
        }
    }

    [Theory]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":0,"unitPrice":9.5}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":-1,"unitPrice":9.5}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":2.5,"unitPrice":9.5}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":2,"unitPrice":-1}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"Other","args":{"quantity":2,"unitPrice":9.5}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"CalculateTotal","args":{"quantity":2,"unitPrice":9.5}}}]}}]}""")]
    public async Task SendPromptAsync_rejects_an_invalid_function_call_without_a_follow_up(string body)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, body);

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidFunctionCall, exception.Error);
            Assert.Single(handler.Bodies);
            Assert.DoesNotContain(ApiKey, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task SendPromptAsync_rejects_a_summary_that_does_not_have_exactly_three_key_points(int count)
    {
        var keyPoints = Enumerable.Range(1, count).Select(index => $"Point {index}").ToArray();
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessJson(LearningSummaryJson(keyPoints: keyPoints)));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidResponse, exception.Error);
        }
    }

    [Fact]
    public async Task SendPromptAsync_invalid_summary_does_not_include_the_api_key_in_the_error()
    {
        var text = LearningSummaryJson(summary: $"before {ApiKey} after", difficulty: "Expert");
        var handler = new RecordingHandler(HttpStatusCode.OK, SuccessJson(text));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidResponse, exception.Error);
            Assert.DoesNotContain(ApiKey, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.Forbidden, "{}")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"message":"API key not valid. Please pass a valid API key."}}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"details":[{"reason":"API_KEY_INVALID"}]}}""")]
    public async Task SendPromptAsync_classifies_an_invalid_api_key(HttpStatusCode statusCode, string body)
    {
        await AssertFailure(statusCode, body, GeminiClientError.InvalidApiKey);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"error":{"status":"RESOURCE_EXHAUSTED"}}""")]
    public async Task SendPromptAsync_classifies_a_rate_limit(HttpStatusCode statusCode, string body)
    {
        await AssertFailure(statusCode, body, GeminiClientError.RateLimitExceeded);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"code":404,"status":"NOT_FOUND","message":"model unavailable"}}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"status":"INVALID_ARGUMENT","message":"The prompt is invalid."}}""")]
    [InlineData(HttpStatusCode.InternalServerError, """{"error":{"status":"INTERNAL"}}""")]
    public async Task SendPromptAsync_classifies_other_http_failures_as_server_rejection(HttpStatusCode statusCode, string body)
    {
        await AssertFailure(statusCode, body, GeminiClientError.ServerRejection);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"candidates":[]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"text":""}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"text":"   "}]}}]}""")]
    [InlineData("""{"promptFeedback":{"blockReason":"SAFETY"},"candidates":[{"content":{"parts":[{"text":"hidden"}]}}]}""")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task SendPromptAsync_classifies_a_body_without_text_as_an_empty_response(string body)
    {
        await AssertFailure(HttpStatusCode.OK, body, GeminiClientError.EmptyResponse);
    }

    [Fact]
    public async Task SendPromptAsync_classifies_a_connection_error_as_a_network_failure()
    {
        var handler = new RecordingHandler(_ => throw new HttpRequestException("Could not connect."));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.NetworkFailure, exception.Error);
            Assert.DoesNotContain(ApiKey, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_classifies_a_client_timeout_as_a_network_failure()
    {
        var handler = new RecordingHandler(_ => throw new TaskCanceledException("The request timed out."));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.NetworkFailure, exception.Error);
            Assert.DoesNotContain(ApiKey, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_lets_caller_cancellation_through()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handler = new RecordingHandler(_ => throw new TaskCanceledException());

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.SendPromptAsync(Prompt, cancellation.Token));
        }
    }

    private static async Task AssertFailure(HttpStatusCode statusCode, string body, GeminiClientError expected)
    {
        var handler = new RecordingHandler(statusCode, body);

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(expected, exception.Error);
            Assert.DoesNotContain(ApiKey, exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(ApiKey, body, StringComparison.Ordinal);
        }
    }

    private static GeminiLlmClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };

        return new GeminiLlmClient(httpClient);
    }

    private static void AssertSummaryDoesNotContainApiKey(LearningSummary summary)
    {
        Assert.DoesNotContain(ApiKey, summary.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, summary.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, summary.Difficulty, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, string.Join(" ", summary.KeyPoints), StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string LearningSummaryJson(
        string title = "Gravity",
        string summary = "Objects pull on each other.",
        string difficulty = LearningSummary.Beginner,
        IReadOnlyList<string>? keyPoints = null)
    {
        keyPoints ??= ["Mass attracts mass", "Distance weakens the pull", "Weight depends on mass"];

        var points = string.Join(",", keyPoints.Select(JsonString));
        return "{\"title\":" + JsonString(title)
            + ",\"summary\":" + JsonString(summary)
            + ",\"keyPoints\":[" + points + "]"
            + ",\"difficulty\":" + JsonString(difficulty) + "}";
    }

    private static string SuccessJson(params string[] parts)
    {
        var partJson = string.Join(",", parts.Select(part => "{\"text\":" + JsonString(part) + "}"));
        return "{\"candidates\":[{\"content\":{\"parts\":[" + partJson + "]}}]}";
    }

    private static string JsonString(string value)
    {
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private sealed class ApiKeyScope : IDisposable
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private readonly string? _previous;

        public ApiKeyScope(string? apiKey)
        {
            Gate.Wait();
            _previous = Environment.GetEnvironmentVariable(ApiKeyVariable);
            Environment.SetEnvironmentVariable(ApiKeyVariable, apiKey);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(ApiKeyVariable, _previous);
            Gate.Release();
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _send;

        public RecordingHandler(HttpStatusCode statusCode, string body)
            : this(_ => Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }))
        {
        }

        public RecordingHandler(Func<CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = string.Empty;

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Bodies.Add(Body);

            return await _send(cancellationToken);
        }
    }
}
