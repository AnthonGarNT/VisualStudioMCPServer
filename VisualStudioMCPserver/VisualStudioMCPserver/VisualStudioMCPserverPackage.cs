using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace VisualStudioMCPserver
{
    /// <summary>
    /// VS Package that starts and stops the MCPServer.exe side-car process
    /// whenever a solution is opened or closed inside Visual Studio.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class VisualStudioMCPserverPackage : AsyncPackage
    {
        /// <summary>The GUID that uniquely identifies this package.</summary>
        public const string PackageGuidString = "e6ef9e43-b163-4ee0-811d-5dd444b2d20f";

        private DTE2 _dte;
        private SolutionEvents _solutionEvents;   // field keeps the COM reference alive (DTE uses weak refs)
        private System.Diagnostics.Process _serverProcess;
        private VsOutputLogger _logger;

        /// <inheritdoc/>
        protected override async Task InitializeAsync(CancellationToken cancellationToken,
                                                       IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Initialise the Output-window logger first so all subsequent steps can use it.
            IVsOutputWindow outputWindow = await GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            _logger = new VsOutputLogger(outputWindow, JoinableTaskFactory);

            _dte = (DTE2)await GetServiceAsync(typeof(DTE));
            if (_dte == null)
            {
                _logger.LogError("Failed to acquire DTE service — MCP server will not start.");
                return;
            }

            // Subscribe to solution events — the field reference prevents GC of the COM object.
            _solutionEvents = _dte.Events.SolutionEvents;
            _solutionEvents.Opened += OnSolutionOpened;
            _solutionEvents.AfterClosing += OnSolutionClosed;

            _logger.Log("Extension initialised. Waiting for a solution to open.");

            // If a solution is already open when the package loads, start right away.
            if (_dte.Solution != null && !string.IsNullOrEmpty(_dte.Solution.FullName))
            {
                StartServer(_dte.Solution.FullName);
            }
        }

        // ── Solution event handlers ──────────────────────────────────────────

        private void OnSolutionOpened()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string solutionPath = _dte?.Solution?.FullName ?? string.Empty;
            StartServer(solutionPath);
        }

        private void OnSolutionClosed()
        {
            StopServer();
        }

        // ── Server lifecycle ─────────────────────────────────────────────────

        private void StartServer(string solutionPath)
        {
            StopServer();   // kill any previously running instance first

            string serverExe = GetServerExePath();
            if (!File.Exists(serverExe))
            {
                _logger.LogError($"MCPServer executable not found at: {serverExe}");
                return;
            }

            int parentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = serverExe,
                Arguments = $"\"{solutionPath}\" {parentPid}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _serverProcess = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };
            _serverProcess.OutputDataReceived += (s, e) => { if (e.Data != null) _logger.Log(e.Data); };
            _serverProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) _logger.LogError(e.Data); };
            _serverProcess.Exited += (s, e) => _logger.Log("MCPServer process exited.");

            try
            {
                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();
                _logger.Log($"MCPServer started (PID {_serverProcess.Id}) for: {solutionPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start MCPServer process.", ex);
            }
        }

        private void StopServer()
        {
            if (_serverProcess == null)
            {
                return;
            }

            try
            {
                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill();

                    if (!_serverProcess.WaitForExit(3000))
                    {
                        _logger.LogError("MCPServer did not exit within the 3-second timeout.");
                    }
                    else
                    {
                        _logger.Log("MCPServer stopped.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while stopping MCPServer.", ex);
            }
            finally
            {
                _serverProcess.Dispose();
                _serverProcess = null;
            }
        }

        /// <summary>
        /// Resolves the full path to MCPServer.exe.
        /// The VSIX content items deploy the MCPServer build output into a <c>MCPServer\</c>
        /// sub-folder next to this extension's DLL for both Debug and Release configurations.
        /// </summary>
        private static string GetServerExePath()
        {
            string extensionDir = Path.GetDirectoryName(
                typeof(VisualStudioMCPserverPackage).Assembly.Location) ?? string.Empty;

            return Path.Combine(extensionDir, "MCPServer", "MCPServer.exe");
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopServer();
            }

            base.Dispose(disposing);
        }
    }
}
