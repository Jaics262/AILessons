using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

Console.WriteLine("AI Prompt Lab");
Console.WriteLine("Press Ctrl+C to exit.");
Console.WriteLine();

try
{
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Logging.ClearProviders();
    builder.Configuration.AddUserSecrets(typeof(PromptLabWorker).Assembly, optional: true);
    builder.Services.AddPromptLab();

    using var host = builder.Build();
    _ = host.Services.GetRequiredService<GeminiLlmClient>();
    await host.RunAsync();
}
catch (InvalidOperationException ex)
{
    Log.Error(ex, "Conversation history could not be created.");
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
catch (GeminiClientException ex)
{
    PromptLabErrors.LogFailure(ex);
    Console.Error.WriteLine();
    Console.Error.WriteLine(PromptLabErrors.Describe(ex.Error));
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
