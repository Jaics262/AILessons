using Microsoft.Extensions.Hosting;
using Serilog;

public sealed class PromptLabWorker : BackgroundService
{
    private readonly GeminiLlmClient _client;

    public PromptLabWorker(GeminiLlmClient client)
    {
        _client = client;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var input = ReadPromptFields(stoppingToken);
                if (input is null || stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                if (input.Role.Equals("reset", StringComparison.OrdinalIgnoreCase))
                {
                    _client.ResetConversation();
                    Console.WriteLine("Conversation cleared.");
                    Console.WriteLine();
                    continue;
                }

                var prompt = BuildPrompt(input);
                DisplayPrompt(prompt);

                var result = await _client.SendPromptAsync(prompt, stoppingToken);
                foreach (var total in result.Totals)
                {
                    Console.WriteLine($"Total: {total}");
                    Console.WriteLine();
                }

                if (result.Summary is not null)
                {
                    DisplayResponse(result.Summary);
                }

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
                if (ex.Error is not (GeminiClientError.InvalidResponse or GeminiClientError.InvalidFunctionCall))
                {
                    PromptLabErrors.LogFailure(ex);
                }

                Console.Error.WriteLine();
                Console.Error.WriteLine(ex.Detail ?? PromptLabErrors.Describe(ex.Error));
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

    private static PromptInput? ReadPromptFields(CancellationToken cancellationToken)
    {
        Console.WriteLine("Enter each field. A blank or whitespace-only value is not accepted.");
        Console.WriteLine("Type reset as the Role to clear the conversation.");
        Console.WriteLine();

        var role = ReadRequiredField("Role", cancellationToken);
        if (role is null)
        {
            return null;
        }

        if (role.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return new PromptInput("reset", string.Empty, string.Empty, string.Empty, string.Empty);
        }

        var task = ReadRequiredField("Task", cancellationToken);
        var context = task is null ? null : ReadRequiredField("Context", cancellationToken);
        var outputFormat = context is null ? null : ReadRequiredField("Output Format", cancellationToken);
        var constraints = outputFormat is null ? null : ReadRequiredField("Constraints", cancellationToken);

        if (role is null || task is null || context is null || outputFormat is null || constraints is null)
        {
            return null;
        }

        return new PromptInput(role, task, context, outputFormat, constraints);
    }

    private static string? ReadRequiredField(string label, CancellationToken cancellationToken)
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

    private static string BuildPrompt(PromptInput input)
    {
        return $"""
            Role: {input.Role}
            Task: {input.Task}
            Context: {input.Context}
            Output Format: {input.OutputFormat}
            Constraints: {input.Constraints}
            """;
    }

    private static void DisplayPrompt(string prompt)
    {
        Console.WriteLine();
        Console.WriteLine("----- Structured Prompt -----");
        Console.WriteLine(prompt);
        Console.WriteLine("-----------------------------");
    }

    private static void DisplayResponse(LearningSummary summary)
    {
        Console.WriteLine("--AI response");
        Console.WriteLine();
        WriteProperty("Title", summary.Title);
        WriteProperty("Difficulty", summary.Difficulty);
        WriteProperty("Summary", summary.Summary);
        WriteKeyPoints(summary.KeyPoints);
        Console.WriteLine("-----");
    }

    private static void WriteProperty(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Console.WriteLine($"{label} is blank.");
        }
        else
        {
            Console.WriteLine($"{label}: {value}");
        }

        Console.WriteLine();
    }

    private static void WriteKeyPoints(IReadOnlyList<string>? keyPoints)
    {
        if (keyPoints is null || keyPoints.Count == 0 || keyPoints.All(string.IsNullOrWhiteSpace))
        {
            Console.WriteLine("Key points are blank.");
            Console.WriteLine();
            return;
        }

        Console.WriteLine("Key points:");
        foreach (var point in keyPoints)
        {
            Console.WriteLine(string.IsNullOrWhiteSpace(point)
                ? "- A key point is blank."
                : $"- {point}");
        }

        Console.WriteLine();
    }
}

sealed record PromptInput(string Role, string Task, string Context, string OutputFormat, string Constraints);
