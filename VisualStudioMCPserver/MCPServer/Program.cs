using MCPServer.Tools;
using Microsoft.Extensions.Options;
using VisualStudioMCPServer.Shared;

namespace MCPServer;

internal sealed class Program
{
    private static async Task Main(string[] args)
    {
        SolutionContext.SolutionFilePath = args.Length > 0 ? args[0] : string.Empty;

        WebApplication app = BuildHost(args);

        ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();

        WatchParentProcess(args, logger);

        McpServerOptions options = app.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;
        logger.LogInformation("MCP Server listening on http://localhost:{Port}", options.Port);
        logger.LogInformation("Solution: {SolutionPath}",
            string.IsNullOrWhiteSpace(SolutionContext.SolutionFilePath)
                ? "(none)"
                : SolutionContext.SolutionFilePath);

        await app.RunAsync();
    }

    /// <summary>
    /// Reads the parent VS process ID from <paramref name="args"/>[1] and starts a background
    /// watcher that shuts this process down when the parent VS instance exits.
    /// </summary>
    /// <param name="args">Command-line arguments passed at startup.</param>
    /// <param name="logger">Logger used to report lifecycle events.</param>
    private static void WatchParentProcess(string[] args, ILogger<Program> logger)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out int parentPid))
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
    /// <returns>A fully configured <see cref="WebApplication"/> ready to run.</returns>
    private static WebApplication BuildHost(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Logging
            .ClearProviders()
            .AddConsole();

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<SolutionTools>();

        // Bind the McpServer config section so the port is driven by appsettings.json.
        McpServerOptions options = builder.Configuration
            .GetSection(McpServerOptions.SectionName)
            .Get<McpServerOptions>() ?? new McpServerOptions();

        builder.Services.Configure<McpServerOptions>(
            builder.Configuration.GetSection(McpServerOptions.SectionName));

        builder.WebHost.UseUrls($"http://localhost:{options.Port}");

        WebApplication app = builder.Build();

        app.MapMcp();   // registers /sse and /messages endpoints

        return app;
    }
}
