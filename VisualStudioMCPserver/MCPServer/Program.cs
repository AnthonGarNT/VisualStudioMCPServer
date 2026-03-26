using MCPServer;
using MCPServer.Tools;

// ── Read solution path from the first command-line argument ──────────────────
// The VSIX passes the path of the currently open solution when it starts this
// process, e.g.:  MCPServer.exe "C:\MySolution\MySolution.slnx"
SolutionContext.SolutionFilePath = args.Length > 0 ? args[0] : string.Empty;

// ── Build the ASP.NET Core / MCP host ───────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<SolutionTools>();

// Listen on a fixed localhost port so Claude Code can find us reliably.
// Override with --urls or ASPNETCORE_URLS if you need a different port.
builder.WebHost.UseUrls("http://localhost:5010");

var app = builder.Build();

app.MapMcp();   // registers /sse  and  /messages  endpoints

Console.WriteLine($"MCP Server listening on http://localhost:5010");
Console.WriteLine($"Solution : {(string.IsNullOrWhiteSpace(SolutionContext.SolutionFilePath)
                                    ? "(none)"
                                    : SolutionContext.SolutionFilePath)}");

await app.RunAsync();
