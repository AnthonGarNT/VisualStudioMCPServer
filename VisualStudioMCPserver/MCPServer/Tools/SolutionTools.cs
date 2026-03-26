using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Xml.Linq;

namespace MCPServer.Tools;

/// <summary>
/// MCP tools that expose Visual Studio solution structure to AI clients.
/// </summary>
[McpServerToolType]
public sealed class SolutionTools
{
    private readonly ILogger<SolutionTools> _logger;

    /// <summary>
    /// Initialises the tool class with its required dependencies.
    /// </summary>
    /// <param name="logger">Logger for recording tool invocations and errors.</param>
    public SolutionTools(ILogger<SolutionTools> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Lists all projects in the currently open Visual Studio solution.</summary>
    [McpServerTool]
    [Description("Lists all projects in the currently open Visual Studio solution.")]
    public string ListProjects()
    {
        string path = SolutionContext.SolutionFilePath;
        _logger.LogInformation("ListProjects invoked. Solution path: {Path}", path);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.LogWarning("ListProjects: no solution loaded or file not found at '{Path}'.", path);
            return "No solution is loaded or the solution file could not be found.";
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".slnx" => ParseSlnxProjects(path),
            ".sln" => ParseSlnProjects(path),
            _ => $"Unsupported solution format: {Path.GetExtension(path)}"
        };
    }

    /// <summary>
    /// Lists files inside the solution directory, with an optional extension filter.
    /// Skips <c>bin/</c>, <c>obj/</c> and <c>.vs/</c> folders automatically.
    /// </summary>
    [McpServerTool]
    [Description(
        "Lists files inside the solution directory. " +
        "Pass a file extension (e.g. '.cs') to filter results, or leave empty for all files. " +
        "Skips bin/, obj/ and .vs/ folders automatically.")]
    public string ListSolutionFiles(
        [Description("Optional file extension filter, e.g. '.cs'. Leave empty for all files.")]
        string extension = "")
    {
        string path = SolutionContext.SolutionFilePath;
        _logger.LogInformation("ListSolutionFiles invoked. Solution: {Path}, Filter: '{Extension}'", path, extension);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.LogWarning("ListSolutionFiles: no solution loaded or file not found at '{Path}'.", path);
            return "No solution is loaded or the solution file could not be found.";
        }

        string solutionDir = Path.GetDirectoryName(path)!;
        string pattern = string.IsNullOrWhiteSpace(extension) ? "*.*" : $"*{extension}";

        string[] ignoredSegments =
        [
            Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + ".vs" + Path.DirectorySeparatorChar,
        ];

        List<string> files = Directory
            .EnumerateFiles(solutionDir, pattern, SearchOption.AllDirectories)
            .Where(f => !ignoredSegments.Any(seg => f.Contains(seg)))
            .Select(f => Path.GetRelativePath(solutionDir, f))
            .OrderBy(f => f)
            .ToList();

        _logger.LogInformation("ListSolutionFiles: found {Count} file(s).", files.Count);

        return files.Count > 0
            ? string.Join("\n", files)
            : "No matching files found.";
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private string ParseSlnxProjects(string slnxPath)
    {
        XDocument doc = XDocument.Load(slnxPath);
        List<string> projects = doc.Descendants("Project")
                                   .Select(p => p.Attribute("Path")?.Value)
                                   .Where(p => !string.IsNullOrWhiteSpace(p))
                                   .Cast<string>()
                                   .OrderBy(p => p)
                                   .ToList();

        _logger.LogInformation("ParseSlnxProjects: found {Count} project(s).", projects.Count);

        return projects.Count > 0
            ? string.Join("\n", projects)
            : "No projects found in solution.";
    }

    private string ParseSlnProjects(string slnPath)
    {
        // Project lines look like:
        //   Project("{FAE04EC0...}") = "Name", "Relative\Path.csproj", "{GUID}"
        List<string> projects = File.ReadLines(slnPath)
                                    .Where(l => l.StartsWith("Project(\""))
                                    .Select(l =>
                                    {
                                        string[] parts = l.Split(',');
                                        return parts.Length > 1 ? parts[1].Trim().Trim('"') : string.Empty;
                                    })
                                    .Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                                             || p.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                                    .OrderBy(p => p)
                                    .ToList();

        _logger.LogInformation("ParseSlnProjects: found {Count} project(s).", projects.Count);

        return projects.Count > 0
            ? string.Join("\n", projects)
            : "No projects found in solution.";
    }
}
