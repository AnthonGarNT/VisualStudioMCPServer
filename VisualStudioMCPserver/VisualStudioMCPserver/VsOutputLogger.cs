using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;
using System.IO;
using VisualStudioMCPServer.Shared;

namespace VisualStudioMCPserver
{
    /// <summary>
    /// Writes timestamped messages to a dedicated "MCP Server" pane in the Visual Studio Output
    /// window and to a rolling log file shared by both the VSIX and the MCPServer process.
    /// </summary>
    internal sealed class VsOutputLogger : IDisposable
    {
        private readonly IVsOutputWindowPane _pane;
        private readonly JoinableTaskFactory _jtf;
        private readonly StreamWriter        _fileWriter;

        /// <summary>
        /// Initialises the logger, creating the Output pane and log file if they do not exist.
        /// Must be called on the UI thread.
        /// </summary>
        /// <param name="outputWindow">The VS output-window service.</param>
        /// <param name="jtf">
        /// The <see cref="JoinableTaskFactory"/> from the owning <see cref="AsyncPackage"/>.
        /// Must not be <see cref="ThreadHelper.JoinableTaskFactory"/> — that instance is not
        /// safe for use inside Visual Studio extensions.
        /// </param>
        /// <param name="settings">
        /// Settings read from <c>appsettings.json</c>; defaults are used when <see langword="null"/>.
        /// </param>
        public VsOutputLogger(IVsOutputWindow outputWindow, JoinableTaskFactory jtf, McpServerOptions settings = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (outputWindow == null)
            {
                throw new ArgumentNullException(nameof(outputWindow));
            }

            if (jtf == null)
            {
                throw new ArgumentNullException(nameof(jtf));
            }

            settings    = settings ?? new McpServerOptions();
            _jtf        = jtf;
            _pane       = GetOrCreatePane(outputWindow, settings.PaneName);
            _fileWriter = CreateFileWriter(settings.LogFolder, settings.LogFile);
        }

        /// <summary>Writes an informational message.</summary>
        public void Log(string message) => Write("INFO ", message);

        /// <summary>Writes an error message.</summary>
        public void LogError(string message) => Write("ERROR", message);

        /// <summary>Writes an error message and exception details.</summary>
        public void LogError(string message, Exception ex)
            => Write("ERROR", $"{message}{Environment.NewLine}  {ex}");

        /// <inheritdoc />
        public void Dispose()
        {
            _fileWriter?.Flush();
            _fileWriter?.Dispose();
        }

        // -----------------------------------------------------------------------------------------

        private void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";

            // Write to file synchronously — StreamWriter is only accessed here and is thread-safe
            // via its internal lock when AutoFlush is true.
            try
            {
                _fileWriter?.WriteLine(line);
            }
            catch (Exception)
            {
                // Never let a logging failure crash the extension.
            }

            // Both OutputStringNoPump and OutputStringThreadSafe must be called on the UI thread.
            // FileAndForget is the VS SDK's recommended safe fire-and-forget pattern.
            _jtf.RunAsync(async () =>
            {
                await _jtf.SwitchToMainThreadAsync();

                string paneMessage = line + Environment.NewLine;

                if (_pane is IVsOutputWindowPaneNoPump noPump)
                {
                    noPump.OutputStringNoPump(paneMessage);
                }
                else
                {
                    _pane.OutputStringThreadSafe(paneMessage);
                }
            }).FileAndForget("vs/mcpserver/logger");
        }

        private static StreamWriter CreateFileWriter(string logFolder, string logFile)
        {
            try
            {
                string folder = Path.Combine(Path.GetTempPath(), logFolder);
                Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, logFile);

                // Append to the existing file so log history is preserved across sessions.
                StreamWriter writer = new StreamWriter(path, append: true)
                {
                    AutoFlush = true
                };

                writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===== Session started =====");
                return writer;
            }
            catch (Exception)
            {
                // If the file cannot be created, silently degrade to Output-window-only logging.
                return null;
            }
        }

        private static IVsOutputWindowPane GetOrCreatePane(IVsOutputWindow outputWindow, string paneName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Guid paneGuid = new Guid("A9B8C3D4-E5F6-7890-AB12-CD34EF567890");

            int hr = outputWindow.GetPane(ref paneGuid, out IVsOutputWindowPane existingPane);
            if (ErrorHandler.Succeeded(hr) && existingPane != null)
            {
                return existingPane;
            }

            outputWindow.CreatePane(ref paneGuid, paneName, fInitVisible: 1, fClearWithSolution: 0);
            outputWindow.GetPane(ref paneGuid, out IVsOutputWindowPane newPane);

            // Activate forces the pane to appear in the Output window dropdown immediately.
            newPane?.Activate();

            return newPane;
        }
    }
}
