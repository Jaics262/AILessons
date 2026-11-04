// Fixed context and the only claim wordings this exercise treats as recognizable.
public static class AnswerSupportExample
{
    public const string Context =
        "The application uses .NET 10. Unit tests use xUnit. HTTP retries respect the Retry-After header.";

    public const string Question =
        "Which framework does the application use, and how are retry delays handled?";

    public const string SupportedAnswer =
        "The application uses .NET 10, and HTTP retries respect the Retry-After header.";

    public const string UnsupportedAnswer =
        "The application uses .NET 10, retries respect Retry-After, and all requests are retried five times.";

    public static IReadOnlyList<ClaimEvidence> Mappings { get; } =
    [
        new("The application uses .NET 10", "The application uses .NET 10."),
        new("HTTP retries respect the Retry-After header", "HTTP retries respect the Retry-After header."),
        new("retries respect Retry-After", "HTTP retries respect the Retry-After header."),
        new("Unit tests use xUnit", "Unit tests use xUnit.")
    ];
}
