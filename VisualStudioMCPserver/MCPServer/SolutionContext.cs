namespace MCPServer;

/// <summary>
/// Holds shared state for the current Visual Studio solution.
/// Set at startup from command-line arguments passed by the VSIX.
/// </summary>
public static class SolutionContext
{
    /// <summary>
    /// Full path to the open .sln or .slnx solution file.
    /// </summary>
    public static string SolutionFilePath { get; set; } = string.Empty;
}
