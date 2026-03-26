namespace VisualStudioMCPserver
{
    /// <summary>
    /// Strongly-typed representation of the <c>VsixOptions</c> section in <c>vsix-settings.json</c>.
    /// Default values act as a safe fallback when the file is missing or unparseable.
    /// </summary>
    internal sealed class VsixOptions
    {
        public const string SectionName = "VsixOptions";

        /// <summary>Local TCP port the MCPServer process binds to.</summary>
        public int Port { get; set; } = 5010;

        /// <summary>Display name of the VS Output window pane.</summary>
        public string PaneName { get; set; } = "MCP Server";

        /// <summary>Sub-folder name inside <see cref="System.IO.Path.GetTempPath"/> used for the log file.</summary>
        public string LogFolder { get; set; } = "VisualStudioMCPServer";

        /// <summary>Log file name written inside <see cref="LogFolder"/>.</summary>
        public string LogFile { get; set; } = "mcpserver.log";
    }
}
