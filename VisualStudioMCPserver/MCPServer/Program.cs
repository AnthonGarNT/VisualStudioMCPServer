using MCPServer.Tools;

namespace MCPServer;

internal sealed class Program
{
    private static async Task Main(string[] args)
    {
        SolutionContext.SolutionFilePath = args.Length > 0 ? args[0] : string.Empty;

        WatchParentProcess(args);

        var app = BuildHost(args);

        Console.WriteLine($"MCP Server listening on http://localhost:5010");
        Console.WriteLine($"Solution : {(string.IsNullOrWhiteSpace(SolutionContext.SolutionFilePath)
                                            ? "(none)"
                                            : SolutionContext.SolutionFilePath)}");

        await app.RunAsync();
    }

    /// <summary>
    /// Reads the parent VS process ID from args[1] and starts a background watcher
    /// that shuts this process down when VS exits.
    /// </summary>
    private static void WatchParentProcess(string[] args)
    {
        if (args.Length < 2 || !int.TryParse(args[1], out var parentPid))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var parent = System.Diagnostics.Process.GetProcessById(parentPid);
                await parent.WaitForExitAsync();
            }
            catch
            {
                // Process already gone — fall through to exit.
            }
            finally
            {
                Console.WriteLine("[MCPServer] Parent VS process exited — shutting down.");
                Environment.Exit(0);
            }
        });
    }

    /// <summary>
    /// Configures and builds the ASP.NET Core / MCP web application.
    /// </summary>
    private static WebApplication BuildHost(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<SolutionTools>();

        // Fixed localhost port so Claude Code can find us reliably.
        // Override with --urls or ASPNETCORE_URLS if needed.
        builder.WebHost.UseUrls("http://localhost:5010");

        var app = builder.Build();

        app.MapMcp();   // registers /sse and /messages endpoints

        return app;
    }
}
