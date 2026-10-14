try
{
    var input = ReadPromptFields();
    var prompt = BuildPrompt(input);
    DisplayPrompt(prompt);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
catch (IOException ex)
{
    Console.Error.WriteLine($"Could not read input: {ex.Message}");
    Environment.ExitCode = 1;
}

static PromptInput ReadPromptFields()
{
    Console.WriteLine("AI Prompt Lab");
    Console.WriteLine("Enter each field. A blank or whitespace-only value is not accepted.");
    Console.WriteLine();

    return new PromptInput(
        Role: ReadRequiredField("Role"),
        Task: ReadRequiredField("Task"),
        Context: ReadRequiredField("Context"),
        OutputFormat: ReadRequiredField("Output Format"),
        Constraints: ReadRequiredField("Constraints"));
}

static string ReadRequiredField(string label)
{
    while (true)
    {
        Console.Write($"{label}: ");
        var raw = Console.ReadLine()
            ?? throw new InvalidOperationException($"Input ended before {label} was provided.");

        var value = raw.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        Console.WriteLine($"{label} cannot be empty or whitespace. Try again.");
    }
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

sealed record PromptInput(string Role, string Task, string Context, string OutputFormat, string Constraints);
