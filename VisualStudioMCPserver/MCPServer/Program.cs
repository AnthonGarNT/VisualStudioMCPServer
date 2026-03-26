using MCPServer.Tools;

namespace MCPServer;

internal sealed class Program
{
    private static async Task Main(string[] args)
    {
        CommandLineArgs cliArgs = CommandLineArgs.Parse(args);

        SolutionContext.SolutionFilePath = cliArgs.SolutionPath;

        WebApplication app = BuildHost(args, cliArgs.Port);

        ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();

        WatchParentProcess(cliArgs.ParentPid, logger);

        logger.LogInformation("MCP Server listening on http://localhost:{Port}", cliArgs.Port);
        logger.LogInformation("Solution: {SolutionPath}",
            string.IsNullOrWhiteSpace(SolutionContext.SolutionFilePath)
                ? "(none)"
                : SolutionContext.SolutionFilePath);

        await app.RunAsync();
    }

    /// <summary>
    /// Watches the parent VS process and shuts this process down when it exits.
    /// </summary>
    /// <param name="parentPid">Process ID of the parent Visual Studio instance. 0 means no parent.</param>
    /// <param name="logger">Logger used to report lifecycle events.</param>
    private static void WatchParentProcess(int parentPid, ILogger<Program> logger)
    {
        if (parentPid <= 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                System.Diagnostics.Process parent = System.Diagnostics.Process.GetProcessById(parentPid);
                await parent.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not find parent VS process (PID {ParentPid}) — it may have already exited.", parentPid);
            }
            finally
            {
                logger.LogInformation("Parent VS process exited — shutting down MCP Server.");
                Environment.Exit(0);
            }
        });
    }

    /// <summary>
    /// Configures and builds the ASP.NET Core / MCP web application.
    /// </summary>
    /// <param name="args">Command-line arguments forwarded to the host builder.</param>
    /// <param name="port">TCP port the server should bind to.</param>
    /// <returns>A fully configured <see cref="WebApplication"/> ready to run.</returns>
    private static WebApplication BuildHost(string[] args, int port)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Logging
            .ClearProviders()
            .AddConsole();

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<SolutionTools>();

        WebApplication app = builder.Build();

        app.Urls.Add($"http://localhost:{port}");
        app.MapMcp();   // registers /sse and /messages endpoints

        return app;
    }
}
