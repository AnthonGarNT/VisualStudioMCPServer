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
    private static string _solutionFilePath = string.Empty;
    private static readonly object _lock = new();

    public static string SolutionFilePath
    {
        get
        {
            lock (_lock)
            {
                return _solutionFilePath;
            }
        }
        set
        {
            lock (_lock)
            {
                _solutionFilePath = value;
            }
        }
    }
}
