using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using VisualStudioMCPServer.Shared;

namespace VisualStudioMCPserver
{
    /// <summary>
    /// Reads MCPServer's <c>appsettings.json</c> from the folder that contains
    /// <c>MCPServer.exe</c> and returns a populated <see cref="McpServerOptions"/> instance.
    /// Falls back to default values silently on any failure so the VSIX always starts.
    /// </summary>
    internal static class AppSettingsReader
    {
        private const string FileName    = "appsettings.json";
        private const string SectionName = "McpServer";

        /// <summary>
        /// Loads settings from the <c>appsettings.json</c> file that lives next to
        /// <paramref name="serverExePath"/>.
        /// </summary>
        /// <param name="serverExePath">Full path to <c>MCPServer.exe</c>.</param>
        /// <returns>
        /// Populated <see cref="McpServerOptions"/>; defaults are used for any missing or
        /// unparseable values.
        /// </returns>
        public static McpServerOptions Read(string serverExePath)
        {
            try
            {
                string serverDir = Path.GetDirectoryName(serverExePath);

                if (string.IsNullOrEmpty(serverDir) || !Directory.Exists(serverDir))
                {
                    return new McpServerOptions();
                }

                IConfiguration config = new ConfigurationBuilder()
                    .SetBasePath(serverDir)
                    .AddJsonFile(FileName, optional: true, reloadOnChange: false)
                    .Build();

                McpServerOptions options = config
                    .GetSection(SectionName)
                    .Get<McpServerOptions>() ?? new McpServerOptions();

                return options;
            }
            catch (Exception)
            {
                // Never let a config failure prevent the extension from loading.
                return new McpServerOptions();
            }
        }
    }
}
