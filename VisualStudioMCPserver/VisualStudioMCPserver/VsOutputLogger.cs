using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using System;

namespace VisualStudioMCPserver
{
    /// <summary>
    /// Writes timestamped messages to a dedicated "MCP Server" pane
    /// in the Visual Studio Output window.
    /// </summary>
    internal sealed class VsOutputLogger
    {
        private const string PaneName = "MCP Server";

        private readonly IVsOutputWindowPane _pane;
        private readonly JoinableTaskFactory _jtf;

        /// <summary>
        /// Initialises the logger, creating the Output pane if it does not already exist.
        /// Must be called on the UI thread.
        /// </summary>
        /// <param name="outputWindow">The VS output-window service.</param>
        /// <param name="jtf">
        /// The <see cref="JoinableTaskFactory"/> from the owning <see cref="AsyncPackage"/>.
        /// Must not be <see cref="ThreadHelper.JoinableTaskFactory"/> — that instance is not
        /// safe for use inside Visual Studio extensions.
        /// </param>
        public VsOutputLogger(IVsOutputWindow outputWindow, JoinableTaskFactory jtf)
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

            _jtf = jtf;
            _pane = GetOrCreatePane(outputWindow);
        }

        /// <summary>Writes an informational message to the Output pane.</summary>
        /// <param name="message">The message text.</param>
        public void Log(string message) => Write("INFO ", message);

        /// <summary>Writes an error message to the Output pane.</summary>
        /// <param name="message">The message text.</param>
        public void LogError(string message) => Write("ERROR", message);

        /// <summary>Writes an error message and exception details to the Output pane.</summary>
        /// <param name="message">The message text.</param>
        /// <param name="ex">The exception to include.</param>
        public void LogError(string message, Exception ex) => Write("ERROR", $"{message}{Environment.NewLine}  {ex}");

        private void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}{Environment.NewLine}";

            // Both OutputStringNoPump and OutputStringThreadSafe must be called on the UI thread.
            // FileAndForget is the VS SDK's recommended safe fire-and-forget pattern.
            _jtf.RunAsync(async () =>
            {
                await _jtf.SwitchToMainThreadAsync();

                if (_pane is IVsOutputWindowPaneNoPump noPump)
                {
                    noPump.OutputStringNoPump(line);
                }
                else
                {
                    _pane.OutputStringThreadSafe(line);
                }
            }).FileAndForget("vs/mcpserver/logger");
        }

        private static IVsOutputWindowPane GetOrCreatePane(IVsOutputWindow outputWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Guid paneGuid = new Guid("A9B8C3D4-E5F6-7890-AB12-CD34EF567890");

            int hr = outputWindow.GetPane(ref paneGuid, out IVsOutputWindowPane existingPane);
            if (ErrorHandler.Succeeded(hr) && existingPane != null)
            {
                return existingPane;
            }

            outputWindow.CreatePane(ref paneGuid, PaneName, fInitVisible: 1, fClearWithSolution: 0);
            outputWindow.GetPane(ref paneGuid, out IVsOutputWindowPane newPane);
            return newPane;
        }
    }
}
