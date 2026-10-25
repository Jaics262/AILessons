using Serilog;

public static class PromptLabErrors
{
    public static void LogFailure(GeminiClientException exception)
    {
        var message = Describe(exception.Error);
        if (exception.Error is GeminiClientError.RateLimitExceeded or GeminiClientError.EmptyResponse)
        {
            Log.Warning(exception, "{Message}", message);
            return;
        }

        Log.Error(exception, "{Message}", message);
    }

    public static string Describe(GeminiClientError error)
    {
        return error switch
        {
            GeminiClientError.MissingApiKey =>
                "Missing API key. Set GEMINI_API_KEY in the environment or in user secrets.",
            GeminiClientError.InvalidApiKey =>
                "Invalid API key. Check GEMINI_API_KEY in the environment or in user secrets.",
            GeminiClientError.NetworkFailure =>
                "Network failure. Check your connection and try again.",
            GeminiClientError.RateLimitExceeded =>
                "API rate limit exceeded. Wait a moment and try again.",
            GeminiClientError.ServerRejection =>
                "The API rejected the request. Try again later.",
            GeminiClientError.EmptyResponse =>
                "Empty response from the API. Try again.",
            GeminiClientError.InvalidResponse =>
                "The API returned a response that is not a valid learning summary. Try again.",
            GeminiClientError.InvalidFunctionCall =>
                "The function call was rejected. Quantity, amount, and rate must be greater than zero, and unit price cannot be negative.",
            _ =>
                "Something went wrong while contacting the API. Try again."
        };
    }
}
