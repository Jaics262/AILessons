using System.Diagnostics;
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
    public async Task SendPromptAsync_stores_the_turn_in_the_injected_history()
    {
        var history = new InMemoryConversationHistory();
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: "Stored in the injected history.")));

        using (new ApiKeyScope(ApiKey))
        using (var client = new GeminiLlmClient(CreateHttpClient(handler), history))
        {
            await client.SendPromptAsync(Prompt);

            Assert.NotEmpty(history.Messages);
            client.ResetConversation();
            Assert.Empty(history.Messages);
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
            Assert.Contains("\"name\":\"calculate_vat\"", handler.Body, StringComparison.Ordinal);
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
            Assert.Contains("functionDeclarations", handler.Bodies[1], StringComparison.Ordinal);
            Assert.DoesNotContain("responseMimeType", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains(Prompt, handler.Bodies[1], StringComparison.Ordinal);
            Assert.NotNull(response.Summary);
            Assert.Equal("The total is 19.0", response.Summary.Summary);
            Assert.Equal(19.0m, Assert.Single(response.Totals));
        }
    }
     

    [Fact]
    public async Task SendPromptAsync_calls_calculate_vat_and_returns_the_final_summary()
    {
        var calls = 0;
        var functionCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_vat","args":{"amount":10,"rate":20}},"thoughtSignature":"sig-vat"}]}}]}
            """;
        var finalSummary = SuccessJson(LearningSummaryJson(summary: "The VAT is 2.00."));
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
            var response = await client.SendPromptAsync("Task: Use the calculate_vat tool.");

            Assert.Equal(2, handler.Bodies.Count);
            Assert.Contains("\"name\":\"calculate_vat\"", handler.Bodies[0], StringComparison.Ordinal);
            Assert.Contains("\"name\":\"calculate_total\"", handler.Bodies[0], StringComparison.Ordinal);
            Assert.Contains("\"vat\":2", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-vat\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Equal(2m, Assert.Single(response.Totals));
            Assert.NotNull(response.Summary);
            Assert.Equal("The VAT is 2.00.", response.Summary.Summary);
        }
    }

    [Theory]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_vat","args":{"amount":0,"rate":20}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_vat","args":{"amount":10,"rate":0}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_vat","args":{"amount":-1,"rate":20}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_vat","args":{"amount":10,"rate":-5}}}]}}]}""")]
    public async Task SendPromptAsync_rejects_a_calculate_vat_call_that_is_not_greater_than_zero(string body)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, body);

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidFunctionCall, exception.Error);
            Assert.Single(handler.Bodies);
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

    [Fact]
    public async Task SendPromptAsync_calls_calculate_total_then_calculate_vat_before_the_summary()
    {
        var calls = 0;
        var totalCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":2,"unitPrice":10}},"thoughtSignature":"sig-total"}]}}]}
            """;
        var vatCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_vat","args":{"amount":20,"rate":10}},"thoughtSignature":"sig-vat"}]}}]}
            """;
        var finalSummary = SuccessJson(LearningSummaryJson(summary: "The total is 20 and the VAT is 2."));
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ switch
            {
                0 => totalCall,
                1 => vatCall,
                _ => finalSummary
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var response = await client.SendPromptAsync(Prompt);

            Assert.Equal(3, handler.Bodies.Count);
            Assert.Contains("\"total\":20", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("functionDeclarations", handler.Bodies[1], StringComparison.Ordinal);
            Assert.DoesNotContain("responseMimeType", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"vat\":2", handler.Bodies[2], StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-total\"", handler.Bodies[2], StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-vat\"", handler.Bodies[2], StringComparison.Ordinal);
            Assert.Equal([20m, 2m], response.Totals);
            Assert.NotNull(response.Summary);
            Assert.Equal("The total is 20 and the VAT is 2.", response.Summary.Summary);
        }
    }

    [Fact]
    public async Task SendPromptAsync_rejects_an_unsupported_function_name()
    {
        const string body = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"abcd","args":{"quantity":2,"unitPrice":9.5}}}]}}]}
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
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":2}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total"}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_vat","args":{}}}]}}]}""")]
    [InlineData("""{"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_vat","args":{"amount":10}}}]}}]}""")]
    public async Task SendPromptAsync_rejects_a_function_call_with_missing_arguments(string body)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, body);

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidFunctionCall, exception.Error);
            Assert.Single(handler.Bodies);
        }
    }

    [Fact]
    public async Task SendPromptAsync_stops_after_four_function_calling_rounds()
    {
        const string functionCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":1,"unitPrice":1}},"thoughtSignature":"sig"}]}}]}
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(functionCall, Encoding.UTF8, "application/json")
        }));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidFunctionCall, exception.Error);
            Assert.Equal(5, handler.Bodies.Count);
            Assert.All(handler.Bodies.Take(4), body => Assert.Contains("functionDeclarations", body, StringComparison.Ordinal));
            Assert.DoesNotContain("functionDeclarations", handler.Bodies[4], StringComparison.Ordinal);
            Assert.Contains("\"responseMimeType\":\"application/json\"", handler.Bodies[4], StringComparison.Ordinal);
            Assert.Equal(4, CountOccurrences(handler.Bodies[4], "\"functionResponse\""));
        }
    }

    [Fact]
    public async Task SendPromptAsync_stops_after_the_configured_number_of_function_rounds()
    {
        const string functionCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":1,"unitPrice":1}},"thoughtSignature":"sig"}]}}]}
            """;
        var handler = new RecordingHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(functionCall, Encoding.UTF8, "application/json")
        }));

        using (new ApiKeyScope(ApiKey))
        using (var client = new GeminiLlmClient(
            CreateHttpClient(handler),
            history: null,
            maxContextTurns: PromptLabSettings.DefaultMaxContextTurns,
            maxFunctionRounds: 1,
            model: "gemini-test",
            requestTimeout: TimeSpan.FromMinutes(PromptLabSettings.DefaultRequestTimeoutMinutes)))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidFunctionCall, exception.Error);
            Assert.Equal(2, handler.Bodies.Count);
            Assert.Contains("functionDeclarations", handler.Bodies[0], StringComparison.Ordinal);
            Assert.DoesNotContain("functionDeclarations", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"responseMimeType\":\"application/json\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Equal(
                "/v1beta/models/gemini-test:generateContent",
                handler.Request!.RequestUri!.AbsolutePath);
        }
    }

    [Fact]
    public async Task SendPromptAsync_sends_the_previous_turn_with_the_next_prompt()
    {
        var calls = 0;
        var first = SuccessJson(LearningSummaryJson(summary: "First answer."));
        var second = SuccessJson(LearningSummaryJson(summary: "Second answer."));
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ == 0 ? first : second;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            await client.SendPromptAsync("First question about gravity.");
            var response = await client.SendPromptAsync("Second question about gravity.");

            Assert.Equal(2, handler.Bodies.Count);
            Assert.Contains("First question about gravity.", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("First answer.", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("Second question about gravity.", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("\"role\":\"model\"", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("functionDeclarations", handler.Bodies[1], StringComparison.Ordinal);
            Assert.NotNull(response.Summary);
            Assert.Equal("Second answer.", response.Summary.Summary);
        }
    }

    [Fact]
    public async Task SendPromptAsync_preserves_function_calls_and_tool_responses_in_later_turns()
    {
        var calls = 0;
        var totalCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"id":"c1","name":"calculate_total","args":{"quantity":2,"unitPrice":9.5}},"thoughtSignature":"sig-abc"}]}}]}
            """;
        var vatCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"id":"c2","name":"calculate_vat","args":{"amount":19,"rate":20}},"thoughtSignature":"sig-vat"}]}}]}
            """;
        var firstSummary = SuccessJson(LearningSummaryJson(summary: "The total is 19.0"));
        var secondSummary = SuccessJson(LearningSummaryJson(summary: "The VAT is 3.8"));
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ switch
            {
                0 => totalCall,
                1 => firstSummary,
                2 => vatCall,
                _ => secondSummary
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            await client.SendPromptAsync("Calculate the notebook total.");
            var response = await client.SendPromptAsync("Calculate the VAT on that total.");

            var followUp = handler.Bodies[2];
            Assert.Contains("Calculate the notebook total.", followUp, StringComparison.Ordinal);
            Assert.Contains("\"id\":\"c1\"", followUp, StringComparison.Ordinal);
            Assert.Contains("\"name\":\"calculate_total\"", followUp, StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-abc\"", followUp, StringComparison.Ordinal);
            Assert.Contains("\"functionResponse\"", followUp, StringComparison.Ordinal);
            Assert.Contains("\"total\":19.0", followUp, StringComparison.Ordinal);
            Assert.Contains("The total is 19.0", followUp, StringComparison.Ordinal);
            Assert.Contains("Calculate the VAT on that total.", followUp, StringComparison.Ordinal);
            Assert.Contains("\"vat\":3.8", handler.Bodies[3], StringComparison.Ordinal);
            Assert.Contains("\"id\":\"c2\"", handler.Bodies[3], StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-vat\"", handler.Bodies[3], StringComparison.Ordinal);
            Assert.Equal(3.8m, Assert.Single(response.Totals));
            Assert.NotNull(response.Summary);
            Assert.Equal("The VAT is 3.8", response.Summary.Summary);
        }
    }

    [Fact]
    public async Task ResetConversation_removes_history_from_the_next_request()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: "Stored answer.")));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            await client.SendPromptAsync("Remember this question.");
            client.ResetConversation();
            await client.SendPromptAsync("A question after reset.");

            Assert.Equal(2, handler.Bodies.Count);
            Assert.DoesNotContain("Remember this question.", handler.Bodies[1], StringComparison.Ordinal);
            Assert.DoesNotContain("Stored answer.", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("A question after reset.", handler.Bodies[1], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_does_not_store_a_rejected_turn()
    {
        var calls = 0;
        var rejected = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"abcd","args":{"quantity":1,"unitPrice":1}}}]}}]}
            """;
        var summary = SuccessJson(LearningSummaryJson(summary: "A fresh answer."));
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ == 0 ? rejected : summary;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() =>
                client.SendPromptAsync("Do not remember this rejected prompt."));

            Assert.Equal(GeminiClientError.InvalidFunctionCall, exception.Error);
            await client.SendPromptAsync("A prompt that should stand alone.");

            Assert.DoesNotContain("Do not remember this rejected prompt.", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("A prompt that should stand alone.", handler.Bodies[1], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_keeps_conversation_history_separate_for_each_client()
    {
        var firstHandler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: "Answer for session A.")));
        var secondHandler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: "Answer for session B.")));

        using (new ApiKeyScope(ApiKey))
        using (var first = CreateClient(firstHandler))
        using (var second = CreateClient(secondHandler))
        {
            await first.SendPromptAsync("Question for session A.");
            await second.SendPromptAsync("Question for session B.");
            await first.SendPromptAsync("Follow-up for session A.");

            Assert.Contains("Question for session A.", firstHandler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("Answer for session A.", firstHandler.Bodies[1], StringComparison.Ordinal);
            Assert.DoesNotContain("Question for session B.", firstHandler.Bodies[1], StringComparison.Ordinal);
            Assert.Single(secondHandler.Bodies);
            Assert.DoesNotContain("Question for session A.", secondHandler.Bodies[0], StringComparison.Ordinal);
            Assert.Contains("Question for session B.", secondHandler.Bodies[0], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_still_stops_after_four_function_rounds_when_history_exists()
    {
        var calls = 0;
        var summary = SuccessJson(LearningSummaryJson(summary: "Remember this answer."));
        const string functionCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":1,"unitPrice":1}},"thoughtSignature":"sig"}]}}]}
            """;
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ == 0 ? summary : functionCall;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            await client.SendPromptAsync("Store this turn.");
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() =>
                client.SendPromptAsync("Keep calling the tool."));

            Assert.Equal(GeminiClientError.InvalidFunctionCall, exception.Error);
            Assert.Equal(6, handler.Bodies.Count);
            Assert.Contains("Store this turn.", handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("Remember this answer.", handler.Bodies[1], StringComparison.Ordinal);
            Assert.All(handler.Bodies.Skip(1).Take(4), body => Assert.Contains("functionDeclarations", body, StringComparison.Ordinal));
            Assert.DoesNotContain("functionDeclarations", handler.Bodies[5], StringComparison.Ordinal);
            Assert.Contains("\"responseMimeType\":\"application/json\"", handler.Bodies[5], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_does_not_send_the_api_key_back_in_conversation_history()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: $"before {ApiKey} after")));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            await client.SendPromptAsync(Prompt);
            await client.SendPromptAsync("Ask again.");

            Assert.DoesNotContain(ApiKey, handler.Bodies[1], StringComparison.Ordinal);
            Assert.Contains("before  after", handler.Bodies[1], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_sends_no_earlier_context_when_the_history_is_empty()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: "A first answer.")));
        var history = new InMemoryConversationHistory();

        using (new ApiKeyScope(ApiKey))
        using (var client = new GeminiLlmClient(CreateHttpClient(handler), history, maxContextTurns: 5))
        {
            await client.SendPromptAsync(Prompt);

            Assert.DoesNotContain("\"role\":\"model\"", handler.Bodies[0], StringComparison.Ordinal);
            Assert.Contains(Prompt, handler.Bodies[0], StringComparison.Ordinal);
            Assert.Equal(2, history.Messages.Count);
        }
    }

    [Fact]
    public async Task SendPromptAsync_sends_both_turns_when_two_stored_turns_are_within_a_limit_of_five()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: "A stored answer.")));
        var history = new InMemoryConversationHistory();

        using (new ApiKeyScope(ApiKey))
        using (var client = new GeminiLlmClient(CreateHttpClient(handler), history, maxContextTurns: 5))
        {
            await client.SendPromptAsync("Question 1.");
            await client.SendPromptAsync("Question 2.");
            await client.SendPromptAsync("Question 3.");

            Assert.Contains("Question 1.", handler.Bodies[2], StringComparison.Ordinal);
            Assert.Contains("Question 2.", handler.Bodies[2], StringComparison.Ordinal);
            Assert.Contains("Question 3.", handler.Bodies[2], StringComparison.Ordinal);
            AssertChronological(handler.Bodies[2], "Question 1.", "Question 2.", "Question 3.");
            Assert.Equal(6, history.Messages.Count);
        }
    }

    [Fact]
    public async Task SendPromptAsync_sends_only_the_latest_five_of_eight_stored_turns()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: "A stored answer.")));
        var history = new InMemoryConversationHistory();

        using (new ApiKeyScope(ApiKey))
        using (var client = new GeminiLlmClient(CreateHttpClient(handler), history, maxContextTurns: 5))
        {
            for (var number = 1; number <= 8; number++)
            {
                await client.SendPromptAsync($"Question {number}.");
            }

            var firstStoredQuestion = history.Messages[0];
            await client.SendPromptAsync("Question 9.");

            var request = handler.Bodies[8];
            Assert.DoesNotContain("Question 1.", request, StringComparison.Ordinal);
            Assert.DoesNotContain("Question 2.", request, StringComparison.Ordinal);
            Assert.DoesNotContain("Question 3.", request, StringComparison.Ordinal);
            Assert.Contains("Question 4.", request, StringComparison.Ordinal);
            Assert.Contains("Question 5.", request, StringComparison.Ordinal);
            Assert.Contains("Question 6.", request, StringComparison.Ordinal);
            Assert.Contains("Question 7.", request, StringComparison.Ordinal);
            Assert.Contains("Question 8.", request, StringComparison.Ordinal);
            Assert.Contains("Question 9.", request, StringComparison.Ordinal);
            AssertChronological(
                request,
                "Question 4.",
                "Question 5.",
                "Question 6.",
                "Question 7.",
                "Question 8.",
                "Question 9.");
            Assert.Equal(18, history.Messages.Count);
            Assert.Same(firstStoredQuestion, history.Messages[0]);
            Assert.Contains("Question 1.", JsonText(history.Messages[0]), StringComparison.Ordinal);
            Assert.Contains("Question 9.", JsonText(history.Messages[16]), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ResetConversation_clears_turns_outside_the_active_window()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            SuccessJson(LearningSummaryJson(summary: "A stored answer.")));
        var history = new InMemoryConversationHistory();

        using (new ApiKeyScope(ApiKey))
        using (var client = new GeminiLlmClient(CreateHttpClient(handler), history, maxContextTurns: 5))
        {
            for (var number = 1; number <= 8; number++)
            {
                await client.SendPromptAsync($"Question {number}.");
            }

            client.ResetConversation();

            Assert.Empty(history.Messages);
            await client.SendPromptAsync("Question after reset.");

            Assert.DoesNotContain("Question 1.", handler.Bodies[8], StringComparison.Ordinal);
            Assert.DoesNotContain("Question 8.", handler.Bodies[8], StringComparison.Ordinal);
            Assert.DoesNotContain("\"role\":\"model\"", handler.Bodies[8], StringComparison.Ordinal);
            Assert.Contains("Question after reset.", handler.Bodies[8], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_uses_the_recent_turn_window_without_dropping_stored_history()
    {
        var calls = 0;
        var toolCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"id":"c1","name":"calculate_total","args":{"quantity":2,"unitPrice":9.5}},"thoughtSignature":"sig-abc"}]}}]}
            """;
        var firstSummary = SuccessJson(LearningSummaryJson(summary: "The first answer."));
        var notebookSummary = SuccessJson(LearningSummaryJson(summary: "The notebook total is 19.0"));
        var gravitySummary = SuccessJson(LearningSummaryJson(summary: "Gravity is a pull."));
        var thirdSummary = SuccessJson(LearningSummaryJson(summary: "The third answer."));
        var handler = new RecordingHandler(_ =>
        {
            var body = calls switch
            {
                0 => firstSummary,
                1 => toolCall,
                2 => notebookSummary,
                3 => gravitySummary,
                _ => thirdSummary
            };
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        });
        var history = new InMemoryConversationHistory();

        using (new ApiKeyScope(ApiKey))
        using (var client = new GeminiLlmClient(CreateHttpClient(handler), history, maxContextTurns: 2))
        {
            await client.SendPromptAsync("First question.");
            await client.SendPromptAsync("Calculate the notebook total.");
            var functionCallMessage = history.Messages[3];
            await client.SendPromptAsync("What is gravity?");

            var gravityRequest = handler.Bodies[3];
            Assert.Contains("First question.", gravityRequest, StringComparison.Ordinal);
            Assert.Contains("The first answer.", gravityRequest, StringComparison.Ordinal);
            Assert.Contains("Calculate the notebook total.", gravityRequest, StringComparison.Ordinal);
            Assert.Contains("\"id\":\"c1\"", gravityRequest, StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-abc\"", gravityRequest, StringComparison.Ordinal);
            Assert.Contains("\"functionResponse\"", gravityRequest, StringComparison.Ordinal);
            Assert.Contains("\"total\":19.0", gravityRequest, StringComparison.Ordinal);
            AssertChronological(
                gravityRequest,
                "First question.",
                "\"thoughtSignature\":\"sig-abc\"",
                "\"functionResponse\"",
                "What is gravity?");

            await client.SendPromptAsync("A third question.");

            var thirdRequest = handler.Bodies[4];
            Assert.DoesNotContain("First question.", thirdRequest, StringComparison.Ordinal);
            Assert.DoesNotContain("The first answer.", thirdRequest, StringComparison.Ordinal);
            Assert.Contains("Calculate the notebook total.", thirdRequest, StringComparison.Ordinal);
            Assert.Contains("\"id\":\"c1\"", thirdRequest, StringComparison.Ordinal);
            Assert.Contains("\"thoughtSignature\":\"sig-abc\"", thirdRequest, StringComparison.Ordinal);
            Assert.Contains("\"functionResponse\"", thirdRequest, StringComparison.Ordinal);
            Assert.Contains("\"total\":19.0", thirdRequest, StringComparison.Ordinal);
            Assert.Contains("What is gravity?", thirdRequest, StringComparison.Ordinal);
            Assert.Contains("Gravity is a pull.", thirdRequest, StringComparison.Ordinal);
            Assert.Contains("A third question.", thirdRequest, StringComparison.Ordinal);
            AssertChronological(
                thirdRequest,
                "Calculate the notebook total.",
                "\"id\":\"c1\"",
                "\"thoughtSignature\":\"sig-abc\"",
                "\"functionResponse\"",
                "What is gravity?",
                "A third question.");

            Assert.Equal(10, history.Messages.Count);
            Assert.Same(functionCallMessage, history.Messages[3]);
            Assert.Contains("First question.", JsonText(history.Messages[0]), StringComparison.Ordinal);
            Assert.Contains("A third question.", JsonText(history.Messages[8]), StringComparison.Ordinal);
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
    public async Task SendPromptAsync_classifies_a_client_timeout_separately_from_caller_cancellation()
    {
        var handler = new RecordingHandler(_ => throw new TaskCanceledException("The request timed out."));

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.RequestTimeout, exception.Error);
            Assert.DoesNotContain(ApiKey, exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(Prompt, exception.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SendPromptAsync_http_failure_does_not_carry_the_response_body_or_prompt()
    {
        var body = "{\"error\":{\"message\":\"rejected " + ApiKey + " and " + Prompt + "\"}}";
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError, body);

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateClient(handler))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.ServerRejection, exception.Error);
            Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
            Assert.DoesNotContain(ApiKey, exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(Prompt, exception.ToString(), StringComparison.Ordinal);
            Assert.Null(exception.Detail);
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

    [Fact]
    public async Task SendPromptAsync_retries_http_429_and_then_returns_the_summary()
    {
        var calls = 0;
        var delay = new RecordingRetryDelay();
        var handler = new RecordingHandler(_ =>
        {
            var status = calls++ == 0 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK;
            var body = status == HttpStatusCode.OK ? SuccessJson(LearningSummaryJson(summary: "Retried")) : "{}";
            return Task.FromResult(JsonResponse(status, body));
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateRetryClient(handler, delay))
        {
            var started = Stopwatch.StartNew();
            var response = await client.SendPromptAsync(Prompt);
            started.Stop();

            Assert.Equal("Retried", response.Summary!.Summary);
            Assert.Equal(2, handler.Bodies.Count);
            Assert.Equal([HttpRetryPolicy.InitialDelay], delay.Delays);
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(1));
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SendPromptAsync_retries_http_5xx_with_exponential_backoff(HttpStatusCode statusCode)
    {
        var calls = 0;
        var delay = new RecordingRetryDelay();
        var handler = new RecordingHandler(_ =>
        {
            var status = calls++ < 2 ? statusCode : HttpStatusCode.OK;
            var body = status == HttpStatusCode.OK ? SuccessJson(LearningSummaryJson()) : """{"error":{"status":"INTERNAL"}}""";
            return Task.FromResult(JsonResponse(status, body));
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateRetryClient(handler, delay))
        {
            var started = Stopwatch.StartNew();
            var response = await client.SendPromptAsync(Prompt);
            started.Stop();

            Assert.NotNull(response.Summary);
            Assert.Equal(3, handler.Bodies.Count);
            Assert.Equal([HttpRetryPolicy.InitialDelay, HttpRetryPolicy.InitialDelay * 2], delay.Delays);
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task SendPromptAsync_stops_after_two_retries_and_keeps_the_original_error()
    {
        var delay = new RecordingRetryDelay();
        var handler = new RecordingHandler(HttpStatusCode.InternalServerError, """{"error":{"status":"INTERNAL"}}""");

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateRetryClient(handler, delay))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.ServerRejection, exception.Error);
            Assert.Equal(HttpRetryPolicy.MaxRetries + 1, handler.Bodies.Count);
            Assert.Equal([HttpRetryPolicy.InitialDelay, HttpRetryPolicy.InitialDelay * 2], delay.Delays);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, GeminiClientError.ServerRejection)]
    [InlineData(HttpStatusCode.Unauthorized, GeminiClientError.InvalidApiKey)]
    [InlineData(HttpStatusCode.Forbidden, GeminiClientError.InvalidApiKey)]
    public async Task SendPromptAsync_does_not_retry_ordinary_400_401_or_403(HttpStatusCode statusCode, GeminiClientError expected)
    {
        var delay = new RecordingRetryDelay();
        var handler = new RecordingHandler(statusCode, "{}");

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateRetryClient(handler, delay))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(expected, exception.Error);
            Assert.Single(handler.Bodies);
            Assert.Empty(delay.Delays);
        }
    }

    [Fact]
    public async Task SendPromptAsync_retries_a_transient_connection_failure()
    {
        var calls = 0;
        var delay = new RecordingRetryDelay();
        var handler = new RecordingHandler(_ =>
        {
            if (calls++ == 0)
            {
                throw new HttpRequestException("Could not connect.");
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, SuccessJson(LearningSummaryJson(summary: "Connected"))));
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateRetryClient(handler, delay))
        {
            var response = await client.SendPromptAsync(Prompt);

            Assert.Equal("Connected", response.Summary!.Summary);
            Assert.Equal(2, handler.Bodies.Count);
            Assert.Equal([HttpRetryPolicy.InitialDelay], delay.Delays);
        }
    }

    [Fact]
    public async Task SendPromptAsync_retries_a_request_timeout_without_waiting_in_the_test()
    {
        var calls = 0;
        var delay = new RecordingRetryDelay();
        var handler = new RecordingHandler(_ =>
        {
            if (calls++ == 0)
            {
                throw new TaskCanceledException("The request timed out.");
            }

            return Task.FromResult(JsonResponse(HttpStatusCode.OK, SuccessJson(LearningSummaryJson())));
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateRetryClient(handler, delay))
        {
            var started = Stopwatch.StartNew();
            var response = await client.SendPromptAsync(Prompt);
            started.Stop();

            Assert.NotNull(response.Summary);
            Assert.Equal([HttpRetryPolicy.InitialDelay], delay.Delays);
            Assert.True(started.Elapsed < TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task SendPromptAsync_does_not_retry_after_the_caller_cancels()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var delay = new RecordingRetryDelay();
        var handler = new RecordingHandler(_ => throw new TaskCanceledException());

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateRetryClient(handler, delay))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.SendPromptAsync(Prompt, cancellation.Token));

            Assert.Empty(delay.Delays);
            Assert.True(handler.Bodies.Count <= 1);
        }
    }

    [Fact]
    public async Task SendPromptAsync_keeps_http_retries_separate_from_the_four_function_rounds()
    {
        var calls = 0;
        var delay = new RecordingRetryDelay();
        const string functionCall = """
            {"candidates":[{"content":{"parts":[{"functionCall":{"name":"calculate_total","args":{"quantity":1,"unitPrice":1}},"thoughtSignature":"sig"}]}}]}
            """;
        var handler = new RecordingHandler(_ =>
        {
            var body = calls++ == 0
                ? """{"error":{"status":"UNAVAILABLE"}}"""
                : functionCall;
            var status = calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            return Task.FromResult(JsonResponse(status, body));
        });

        using (new ApiKeyScope(ApiKey))
        using (var client = CreateRetryClient(handler, delay))
        {
            var exception = await Assert.ThrowsAsync<GeminiClientException>(() => client.SendPromptAsync(Prompt));

            Assert.Equal(GeminiClientError.InvalidFunctionCall, exception.Error);
            Assert.Equal(6, handler.Bodies.Count);
            Assert.Equal([HttpRetryPolicy.InitialDelay], delay.Delays);
            Assert.Equal(5, handler.Bodies.Count(body => body.Contains("functionDeclarations", StringComparison.Ordinal)));
            Assert.DoesNotContain("functionDeclarations", handler.Bodies[5], StringComparison.Ordinal);
            Assert.Equal(4, CountOccurrences(handler.Bodies[5], "\"functionResponse\""));
        }
    }

    private static void AssertChronological(string body, params string[] tokens)
    {
        var previous = -1;
        foreach (var token in tokens)
        {
            var index = body.IndexOf(token, StringComparison.Ordinal);
            Assert.True(index > previous, $"Expected '{token}' after the preceding token.");
            previous = index;
        }
    }

    private static string JsonText(object message)
    {
        return System.Text.Json.JsonSerializer.Serialize(message);
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

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static GeminiLlmClient CreateClient(HttpMessageHandler handler)
    {
        return new GeminiLlmClient(CreateHttpClient(handler), new HttpRetryPolicy(new ImmediateRetryDelay()));
    }

    private static GeminiLlmClient CreateRetryClient(HttpMessageHandler handler, RecordingRetryDelay delay)
    {
        return new GeminiLlmClient(CreateHttpClient(handler), new HttpRetryPolicy(delay));
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };
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

    private sealed class ImmediateRetryDelay : IHttpRetryDelay
    {
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRetryDelay : IHttpRetryDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
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
