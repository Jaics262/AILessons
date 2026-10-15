using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .WriteTo.File(
        path: Path.Combine("logs", "ai-prompt-lab-.log"),
        rollingInterval: RollingInterval.Day,
        restrictedToMinimumLevel: LogEventLevel.Warning,
        shared: true)
    .CreateLogger();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

Console.WriteLine("AI Prompt Lab");
Console.WriteLine("Press Ctrl+C to exit.");
Console.WriteLine();

try
{
    using var client = new GeminiLlmClient();

    while (!cancellation.IsCancellationRequested)
    {
        try
        {
            var input = ReadPromptFields(cancellation.Token);
            if (input is null || cancellation.IsCancellationRequested)
            {
                break;
            }

            var prompt = BuildPrompt(input);
            DisplayPrompt(prompt);

            var response = await client.SendPromptAsync(prompt, cancellation.Token);
            DisplayResponse(response);
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to exit.");
            Console.WriteLine();
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (GeminiClientException ex)
        {
            LogFailure(ex);
            Console.Error.WriteLine();
            Console.Error.WriteLine(DescribeApiError(ex.Error));
            Console.Error.WriteLine();
        }
        catch (IOException ex)
        {
            Log.Error(ex, "Could not read input.");
            Console.Error.WriteLine($"Could not read input: {ex.Message}");
            Environment.ExitCode = 1;
            break;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Something went wrong while contacting the API.");
            Console.Error.WriteLine("Something went wrong while contacting the API. Try again.");
            Console.Error.WriteLine();
        }
    }
}
catch (GeminiClientException ex)
{
    LogFailure(ex);
    Console.Error.WriteLine();
    Console.Error.WriteLine(DescribeApiError(ex.Error));
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    Log.Error(ex, "Something went wrong while contacting the API.");
    Console.Error.WriteLine("Something went wrong while contacting the API. Try again.");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

static PromptInput? ReadPromptFields(CancellationToken cancellationToken)
{
    Console.WriteLine("Enter each field. A blank or whitespace-only value is not accepted.");
    Console.WriteLine();

    var role = ReadRequiredField("Role", cancellationToken);
    var task = role is null ? null : ReadRequiredField("Task", cancellationToken);
    var context = task is null ? null : ReadRequiredField("Context", cancellationToken);
    var outputFormat = context is null ? null : ReadRequiredField("Output Format", cancellationToken);
    var constraints = outputFormat is null ? null : ReadRequiredField("Constraints", cancellationToken);

    if (role is null || task is null || context is null || outputFormat is null || constraints is null)
    {
        return null;
    }

    return new PromptInput(role, task, context, outputFormat, constraints);
}

static string? ReadRequiredField(string label, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        Console.Write($"{label}: ");
        var raw = Console.ReadLine();
        if (raw is null || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var value = raw.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        Log.Warning("{Label} cannot be empty or whitespace.", label);
        Console.WriteLine($"{label} cannot be empty or whitespace. Try again.");
    }

    return null;
}

static string BuildPrompt(PromptInput input)
{
    return $"""
        Role: {input.Role}
        Task: {input.Task}
        Context: {input.Context}
        Output Format: {input.OutputFormat}
        Constraints: {input.Constraints}
        """;
}

static void DisplayPrompt(string prompt)
{
    Console.WriteLine();
    Console.WriteLine("----- Structured Prompt -----");
    Console.WriteLine(prompt);
    Console.WriteLine("-----------------------------");
}

static void DisplayResponse(string response)
{
    Console.WriteLine("--AI response");
    Console.WriteLine();
    Console.WriteLine(response);
    Console.WriteLine();
    Console.WriteLine("-----");
}

static void LogFailure(GeminiClientException exception)
{
    var message = DescribeApiError(exception.Error);
    if (exception.Error is GeminiClientError.RateLimitExceeded or GeminiClientError.EmptyResponse)
    {
        Log.Warning(exception, "{Message}", message);
        return;
    }

    Log.Error(exception, "{Message}", message);
}

static string DescribeApiError(GeminiClientError error)
{
    return error switch
    {
        GeminiClientError.MissingApiKey =>
            "Missing API key. Set the GEMINI_API_KEY environment variable and try again.",
        GeminiClientError.InvalidApiKey =>
            "Invalid API key. Check the GEMINI_API_KEY environment variable and try again.",
        GeminiClientError.NetworkFailure =>
            "Network failure. Check your connection and try again.",
        GeminiClientError.RateLimitExceeded =>
            "API rate limit exceeded. Wait a moment and try again.",
        GeminiClientError.ServerRejection =>
            "The API rejected the request. Try again later.",
        GeminiClientError.EmptyResponse =>
            "Empty response from the API. Try again.",
        _ =>
            "Something went wrong while contacting the API. Try again."
    };
}

sealed record PromptInput(string Role, string Task, string Context, string OutputFormat, string Constraints);
