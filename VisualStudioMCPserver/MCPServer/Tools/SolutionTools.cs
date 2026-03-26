using System.ComponentModel;
using System.Xml.Linq;
using ModelContextProtocol.Server;

namespace MCPServer.Tools;

[McpServerToolType]
public class SolutionTools
{
    [McpServerTool]
    [Description("Lists all projects in the currently open Visual Studio solution.")]
    public static string ListProjects()
    {
        var path = SolutionContext.SolutionFilePath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "No solution is loaded or the solution file could not be found.";

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".slnx" => ParseSlnxProjects(path),
            ".sln"  => ParseSlnProjects(path),
            _       => $"Unsupported solution format: {Path.GetExtension(path)}"
        };
    }

    [McpServerTool]
    [Description(
        "Lists files inside the solution directory. " +
        "Pass a file extension (e.g. '.cs') to filter results, or leave empty for all files. " +
        "Skips bin/, obj/ and .vs/ folders automatically.")]
    public static string ListSolutionFiles(
        [Description("Optional file extension filter, e.g. '.cs'. Leave empty for all files.")]
        string extension = "")
    {
        var path = SolutionContext.SolutionFilePath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "No solution is loaded or the solution file could not be found.";

        var solutionDir = Path.GetDirectoryName(path)!;
        var pattern     = string.IsNullOrWhiteSpace(extension) ? "*.*" : $"*{extension}";

        var ignoredSegments = new[] { Path.DirectorySeparatorChar + "bin"  + Path.DirectorySeparatorChar,
                                      Path.DirectorySeparatorChar + "obj"  + Path.DirectorySeparatorChar,
                                      Path.DirectorySeparatorChar + ".vs"  + Path.DirectorySeparatorChar };

        var files = Directory
            .EnumerateFiles(solutionDir, pattern, SearchOption.AllDirectories)
            .Where(f => !ignoredSegments.Any(seg => f.Contains(seg)))
            .Select(f => Path.GetRelativePath(solutionDir, f))
            .OrderBy(f => f)
            .ToList();

        return files.Count > 0
            ? string.Join("\n", files)
            : "No matching files found.";
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────

    private static string ParseSlnxProjects(string slnxPath)
    {
        var doc      = XDocument.Load(slnxPath);
        var projects = doc.Descendants("Project")
                          .Select(p => p.Attribute("Path")?.Value)
                          .Where(p => !string.IsNullOrWhiteSpace(p))
                          .Cast<string>()
                          .OrderBy(p => p)
                          .ToList();

        return projects.Count > 0
            ? string.Join("\n", projects)
            : "No projects found in solution.";
    }

    private static string ParseSlnProjects(string slnPath)
    {
        // Project lines look like:
        //   Project("{FAE04EC0...}") = "Name", "Relative\Path.csproj", "{GUID}"
        var projects = File.ReadLines(slnPath)
                           .Where(l => l.StartsWith("Project(\""))
                           .Select(l =>
                           {
                               var parts = l.Split(',');
                               return parts.Length > 1 ? parts[1].Trim().Trim('"') : string.Empty;
                           })
                           .Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                                    || p.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                           .OrderBy(p => p)
                           .ToList();

        return projects.Count > 0
            ? string.Join("\n", projects)
            : "No projects found in solution.";
    }
}
