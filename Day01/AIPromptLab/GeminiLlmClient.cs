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
    InvalidResponse,
    InvalidFunctionCall
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

    public string? Detail { get; init; }
}

public sealed class GeminiLlmClient : IDisposable
{
    private const string Model = "gemini-3.8-flash";
    private const string ApiKeyVariable = "GEMINI_API_KEY";
    private const string ApiKeyHeader = "x-goog-api-key";
    private const string CalculateTotalFunctionName = "calculate_total";
    private const string CalculateVatFunctionName = "calculate_vat";
    private const int MaxFunctionRounds = 4;

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

    public async Task<GeminiPromptResult> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("The prompt cannot be empty.", nameof(prompt));
        }

        var contents = new List<object>
        {
            new
            {
                role = "user",
                parts = new object[] { new { text = prompt } }
            }
        };
        var totals = new List<decimal>();

        try
        {
            var functionRounds = 0;
            var formattingSummary = false;
            while (functionRounds <= MaxFunctionRounds)
            {
                var allowTools = functionRounds < MaxFunctionRounds && !formattingSummary;
                var apiResponse = await PostGenerateContentAsync(
                    contents,
                    includeTools: allowTools,
                    includeSchema: !allowTools,
                    cancellationToken);
                var text = apiResponse.ReadModelText();
                var functionCalls = apiResponse.ReadFunctionCalls();
                if (functionCalls.Count == 0)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        throw new GeminiClientException(GeminiClientError.EmptyResponse);
                    }

                    var safeText = Redact(text);
                    if (TryDeserializeSummary(safeText, out var summary))
                    {
                        return new GeminiPromptResult(summary, functionCalls, totals);
                    }

                    if (formattingSummary || !allowTools)
                    {
                        return new GeminiPromptResult(DeserializeSummary(safeText), functionCalls, totals);
                    }

                    Log.Warning("The model text was not a learning summary. Requesting the structured summary.");
                    contents.Add(new { role = "model", parts = CreateModelTextParts(apiResponse) });
                    contents.Add(new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = "Format that answer as the learning summary JSON." }
                        }
                    });
                    formattingSummary = true;
                    continue;
                }

                if (!allowTools)
                {
                    Log.Error("The model requested a function call after {MaxFunctionRounds} rounds.", MaxFunctionRounds);
                    throw new GeminiClientException(GeminiClientError.InvalidFunctionCall);
                }

                var modelParts = new List<object>();
                var responseParts = new List<object>();
                foreach (var part in apiResponse.ReadParts())
                {
                    if (part.FunctionCall is null)
                    {
                        if (!string.IsNullOrEmpty(part.Text) || !string.IsNullOrEmpty(part.ThoughtSignature))
                        {
                            modelParts.Add(CreateTextPart(part));
                        }

                        continue;
                    }

                    var total = DispatchFunction(part.FunctionCall);
                    totals.Add(total);
                    modelParts.Add(CreateFunctionCallPart(part));
                    responseParts.Add(CreateFunctionResponsePart(part.FunctionCall, total));
                }

                contents.Add(new { role = "model", parts = modelParts });
                contents.Add(new { role = "user", parts = responseParts });
                functionRounds++;
            }

            Log.Error("The model did not return a final answer after {MaxFunctionRounds} function rounds.", MaxFunctionRounds);
            throw new GeminiClientException(GeminiClientError.InvalidFunctionCall);
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


    private async Task<GeminiApiResponse> PostGenerateContentAsync(
        List<object> contents,
        bool includeTools,
        bool includeSchema,
        CancellationToken cancellationToken)
    {
        var requestUri = $"v1beta/models/{Model}:generateContent";
        object body = includeSchema
            ? new { contents, generationConfig = LearningSummarySchema() }
            : includeTools
                ? new { contents, tools = CalculateTotalTools() }
                : new { contents };

        using var response = await _httpClient.PostAsJsonAsync(requestUri, body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = Redact(raw.Length <= 800 ? raw : raw[..800]);
            Log.Error("Gemini returned HTTP {StatusCode}: {Body}", (int)response.StatusCode, detail);
            throw new GeminiClientException(ClassifyFailure(response.StatusCode, raw));
        }

        return JsonSerializer.Deserialize<GeminiApiResponse>(raw, ResponseJsonOptions)
            ?? throw new GeminiClientException(GeminiClientError.EmptyResponse);
    }

    private static object LearningSummarySchema()
    {
        return new
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
                        minItems = LearningSummary.RequiredKeyPointCount,
                        maxItems = LearningSummary.RequiredKeyPointCount,
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
        };
    }

    private static object CalculateTotalTools()
    {
        return new[]
        {
            new
            {
                functionDeclarations = new object[]
                {
                    new
                    {
                        name = CalculateTotalFunctionName,
                        description = "Calculate the tool price from quantity and unit price",
                        parameters = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                quantity = new { type = "INTEGER" },
                                unitPrice = new { type = "NUMBER" }
                            },
                            required = new[] { "quantity", "unitPrice" }
                        }
                    },
                    new
                    {
                        name = CalculateVatFunctionName,
                        description = "Calculate the VAT amount from an amount and a percentage rate",
                        parameters = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                amount = new { type = "NUMBER" },
                                rate = new { type = "NUMBER" }
                            },
                            required = new[] { "amount", "rate" }
                        }
                    }
                }
            }
        };
    }

    private static decimal DispatchFunction(GeminiFunctionCall call)
    {
        try
        {
            return call.Name switch
            {
                CalculateTotalFunctionName => ExecuteCalculateTotal(call),
                CalculateVatFunctionName => ExecuteCalculateVat(call),
                _ => throw new ArgumentException($"Unknown function '{call.Name}'.")
            };
        }
        catch (ArgumentException ex)
        {
            Log.Error(ex, "The function call was rejected.");
            throw new GeminiClientException(GeminiClientError.InvalidFunctionCall, ex);
        }
    }

    private static decimal ExecuteCalculateTotal(GeminiFunctionCall call)
    {
        if (call.Args.ValueKind != JsonValueKind.Object ||
            !call.Args.TryGetProperty("quantity", out var quantityElement) ||
            !call.Args.TryGetProperty("unitPrice", out var unitPriceElement))
        {
            throw new ArgumentException("calculate_total requires quantity and unitPrice.");
        }

        if (quantityElement.ValueKind != JsonValueKind.Number || !quantityElement.TryGetInt32(out var quantity))
        {
            throw new ArgumentException("Quantity must be an integer.");
        }

        if (unitPriceElement.ValueKind != JsonValueKind.Number || !unitPriceElement.TryGetDecimal(out var unitPrice))
        {
            throw new ArgumentException("Unit price must be a number.");
        }

        return new CalculateTotalTool().CalculateTotal(quantity, unitPrice);
    }

    private static decimal ExecuteCalculateVat(GeminiFunctionCall call)
    {
        if (call.Args.ValueKind != JsonValueKind.Object ||
            !call.Args.TryGetProperty("amount", out var amountElement) ||
            !call.Args.TryGetProperty("rate", out var rateElement))
        {
            throw new ArgumentException("calculate_vat requires amount and rate.");
        }

        if (amountElement.ValueKind != JsonValueKind.Number || !amountElement.TryGetDecimal(out var amount))
        {
            throw new ArgumentException("Amount must be a number.");
        }

        if (rateElement.ValueKind != JsonValueKind.Number || !rateElement.TryGetDecimal(out var rate))
        {
            throw new ArgumentException("Rate must be a number.");
        }

        return new CalculateTotalTool().CalculateVat(amount, rate);
    }

    private static object CreateFunctionCallPart(GeminiPart part)
    {
        var call = part.FunctionCall!;
        var functionCall = new Dictionary<string, object?>
        {
            ["name"] = call.Name,
            ["args"] = call.Args
        };
        if (!string.IsNullOrEmpty(call.Id))
        {
            functionCall["id"] = call.Id;
        }

        if (string.IsNullOrEmpty(part.ThoughtSignature))
        {
            return new { functionCall };
        }

        return new { functionCall, thoughtSignature = part.ThoughtSignature };
    }

    private static object CreateTextPart(GeminiPart part)
    {
        if (string.IsNullOrEmpty(part.ThoughtSignature))
        {
            return new { text = part.Text };
        }

        return new { text = part.Text, thoughtSignature = part.ThoughtSignature };
    }

    private static object CreateFunctionResponsePart(GeminiFunctionCall call, decimal total)
    {
        var functionResponse = new Dictionary<string, object?>
        {
            ["name"] = call.Name,
            ["response"] = new Dictionary<string, decimal>
            {
                [call.Name == CalculateVatFunctionName ? "vat" : "total"] = total
            }
        };
        if (!string.IsNullOrEmpty(call.Id))
        {
            functionResponse["id"] = call.Id;
        }

        return new { functionResponse };
    }

    private static List<object> CreateModelTextParts(GeminiApiResponse apiResponse)
    {
        var modelParts = new List<object>();
        foreach (var part in apiResponse.ReadParts())
        {
            if (!string.IsNullOrEmpty(part.Text) || !string.IsNullOrEmpty(part.ThoughtSignature))
            {
                modelParts.Add(CreateTextPart(part));
            }
        }

        return modelParts;
    }

    private static bool TryDeserializeSummary(string text, out LearningSummary? summary)
    {
        try
        {
            summary = LearningSummary.Deserialize(NormalizeModelJson(text));
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            summary = null;
            return false;
        }
    }

    private static string NormalizeModelJson(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var lineBreak = trimmed.IndexOf('\n');
        if (lineBreak < 0)
        {
            return text;
        }

        var body = trimmed[(lineBreak + 1)..].TrimEnd();
        if (body.EndsWith("```", StringComparison.Ordinal))
        {
            body = body[..^3].TrimEnd();
        }

        return body.Trim();
    }

    private static LearningSummary DeserializeSummary(string text)
    {
        try
        {
            return LearningSummary.Deserialize(NormalizeModelJson(text));
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
