using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace VisualStudioMCPserver
{
    /// <summary>
    /// VS Package that starts / stops the MCPServer.exe side-car process
    /// whenever a solution is opened or closed.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class VisualStudioMCPserverPackage : AsyncPackage
    {
        public const string PackageGuidString = "e6ef9e43-b163-4ee0-811d-5dd444b2d20f";

        private DTE2 _dte;
        private SolutionEvents _solutionEvents;   // field keeps the COM reference alive (DTE uses weak refs)
        private System.Diagnostics.Process _serverProcess;

        protected override async Task InitializeAsync(CancellationToken cancellationToken,
                                                       IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _dte = (DTE2)await GetServiceAsync(typeof(DTE));
            if (_dte == null)
            {
                throw new Exception("Failed to get DTE service.");
            }

            // Subscribe to solution events – keep the reference in a field!
            _solutionEvents = _dte.Events.SolutionEvents;
            _solutionEvents.Opened += OnSolutionOpened;
            _solutionEvents.AfterClosing += OnSolutionClosed;

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
            var solutionPath = _dte != null && _dte.Solution != null
                ? _dte.Solution.FullName
                : string.Empty;
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

            var serverExe = GetServerExePath();
            if (!File.Exists(serverExe))
            {
                Debug.WriteLine("[MCPServer] Executable not found: " + serverExe);
                return;
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = serverExe,
                Arguments = $"\"{solutionPath}\" {System.Diagnostics.Process.GetCurrentProcess().Id}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _serverProcess = new System.Diagnostics.Process();
            _serverProcess.StartInfo = psi;
            _serverProcess.EnableRaisingEvents = true;
            _serverProcess.OutputDataReceived += (s, e) => Debug.WriteLine("[MCPServer] " + e.Data);
            _serverProcess.ErrorDataReceived += (s, e) => Debug.WriteLine("[MCPServer ERR] " + e.Data);
            _serverProcess.Exited += (s, e) => Debug.WriteLine("[MCPServer] Process exited.");

            try
            {
                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MCPServer] Failed to start process: " + ex.Message);
            }

            Debug.WriteLine("[MCPServer] Started (PID " + _serverProcess.Id + ") for: " + solutionPath);
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
                        Debug.WriteLine("[MCPServer] Warning: Process did not exit within the expected time limit.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MCPServer] Error stopping process: " + ex.Message);
            }
            finally
            {
                _serverProcess.Dispose();
                _serverProcess = null;
            }
        }

        /// <summary>
        /// Resolves the path to MCPServer.exe.
        /// The post-build event copies the MCPServer build output into a MCPServer\
        /// sub-folder next to this extension's DLL, for both Debug and Release.
        /// </summary>
        private static string GetServerExePath()
        {
            var extensionDir = Path.GetDirectoryName(
                typeof(VisualStudioMCPserverPackage).Assembly.Location) ?? string.Empty;

            return Path.Combine(extensionDir, "MCPServer", "MCPServer.exe");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopServer();
                _serverProcess = null;
            }

            base.Dispose(disposing);
        }
    }
}
