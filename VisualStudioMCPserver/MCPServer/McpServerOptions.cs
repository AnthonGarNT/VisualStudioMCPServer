namespace VisualStudioMCPServer.Shared;

/// <summary>
/// Strongly-typed representation of the <c>McpServer</c> section in <c>appsettings.json</c>.
/// Shared between the MCPServer and VSIX projects via a file link — edit in one place only.
/// Default values mirror those in the JSON file and act as a safe fallback.
/// </summary>
internal sealed class McpServerOptions
{
    public const string SectionName = "McpServer";

    /// <summary>Local TCP port the HTTP/SSE MCP server binds to.</summary>
    public int Port { get; set; } = 5010;

    /// <summary>Display name of the VS Output window pane.</summary>
    public string PaneName { get; set; } = "MCP Server";

    /// <summary>Sub-folder name inside <see cref="Path.GetTempPath"/> used for the log file.</summary>
    public string LogFolder { get; set; } = "VisualStudioMCPServer";

    /// <summary>Log file name written inside <see cref="LogFolder"/>.</summary>
    public string LogFile { get; set; } = "mcpserver.log";
}
