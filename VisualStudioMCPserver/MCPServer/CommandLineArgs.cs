namespace MCPServer;

/// <summary>
/// Parses and exposes the command-line arguments passed by the VSIX when launching this process.
/// </summary>
internal sealed class CommandLineArgs
{
    /// <summary>Full path to the open solution file.</summary>
    public string SolutionPath { get; }

    /// <summary>Process ID of the parent Visual Studio instance.</summary>
    public int ParentPid { get; }

    /// <summary>TCP port the MCP HTTP/SSE server should bind to.</summary>
    public int Port { get; }

    private CommandLineArgs(string solutionPath, int parentPid, int port)
    {
        SolutionPath = solutionPath;
        ParentPid    = parentPid;
        Port         = port;
    }

    /// <summary>
    /// Parses raw CLI arguments. Falls back to safe defaults for missing or unparseable values.
    /// </summary>
    /// <param name="args">The <c>args</c> array received by <c>Main</c>.</param>
    public static CommandLineArgs Parse(string[] args)
    {
        string solutionPath = args.Length > 0 ? args[0] : string.Empty;
        int    parentPid    = args.Length > 1 && int.TryParse(args[1], out int pid) ? pid : 0;
        int    port         = args.Length > 2 && int.TryParse(args[2], out int p)   ? p   : 5010;

        return new CommandLineArgs(solutionPath, parentPid, port);
    }
}
