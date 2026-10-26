using System.Net;
using Serilog;

public static class PromptLabErrors
{
    public const string TemporarilyUnavailable = "The AI service is temporarily unavailable. Please try again.";

    public static void LogFailure(GeminiClientException exception)
    {
        var statusCode = exception.StatusCode is HttpStatusCode status ? (int?)status : null;
        var exceptionType = exception.InnerException?.GetType().Name;
        if (IsTemporary(exception) || exception.Error is GeminiClientError.EmptyResponse)
        {
            Log.Warning(
                "Gemini request failed. {Error} {StatusCode} {ExceptionType}",
                exception.Error,
                statusCode,
                exceptionType);
            return;
        }

        Log.Error(
            "Gemini request failed. {Error} {StatusCode} {ExceptionType}",
            exception.Error,
            statusCode,
            exceptionType);
    }

    public static string Describe(GeminiClientException exception)
    {
        return exception.Error switch
        {
            GeminiClientError.MissingApiKey =>
                "Missing API key. Set GEMINI_API_KEY in the environment or in user secrets.",
            GeminiClientError.InvalidApiKey =>
                "The API key was rejected. Check GEMINI_API_KEY in the environment or in user secrets.",
            GeminiClientError.NetworkFailure =>
                "The AI service could not be reached. Check your connection and try again.",
            GeminiClientError.RateLimitExceeded or GeminiClientError.RequestTimeout =>
                TemporarilyUnavailable,
            GeminiClientError.ServerRejection when IsServerFault(exception.StatusCode) =>
                TemporarilyUnavailable,
            GeminiClientError.ServerRejection =>
                "The AI service could not accept that request. Please try again.",
            GeminiClientError.EmptyResponse =>
                "The AI service returned an empty response. Please try again.",
            GeminiClientError.InvalidResponse =>
                "The AI service returned a response that could not be used. Please try again.",
            GeminiClientError.InvalidFunctionCall =>
                "The function call was rejected. Quantity, amount, and rate must be greater than zero, and unit price cannot be negative.",
            _ =>
                "Something went wrong while contacting the API. Please try again."
        };
    }

    private static bool IsTemporary(GeminiClientException exception)
    {
        return exception.Error is GeminiClientError.RateLimitExceeded or GeminiClientError.RequestTimeout
            || (exception.Error is GeminiClientError.ServerRejection && IsServerFault(exception.StatusCode));
    }

    private static bool IsServerFault(HttpStatusCode? statusCode)
    {
        var status = (int?)statusCode;
        return status is >= 500 and <= 599;
    }
}
