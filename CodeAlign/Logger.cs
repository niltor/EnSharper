using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;

namespace CodeAlign
{
    /// <summary>
    /// Provides logging that writes to the VS Output window pane and ActivityLog.
    /// </summary>
    internal static class Logger
    {
        private static readonly Guid OutputPaneGuid = new Guid("F81EB4E0-A886-4EC0-9C85-3C7B5B9C6C9A");
        private const string PaneTitle = "Code Align";
        private static IVsOutputWindowPane outputPane;
        private static bool initializationAttempted = false;

        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (initializationAttempted)
            {
                Debug.WriteLine($"[Logger] Initialization already attempted. outputPane is {(outputPane == null ? "null" : "available")}");
                return;
            }

            initializationAttempted = true;

            try
            {
                var outputWindow = serviceProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow == null)
                {
                    Debug.WriteLine("[Logger] Failed to get SVsOutputWindow service");
                    return;
                }

                // Use local variable for ref parameter
                Guid paneGuid = OutputPaneGuid;

                // Try to get existing pane first
                int hr = outputWindow.GetPane(ref paneGuid, out outputPane);

                if (hr != 0 || outputPane == null)
                {
                    // Create new pane
                    paneGuid = OutputPaneGuid;
                    hr = outputWindow.CreatePane(ref paneGuid, PaneTitle, fInitVisible: 1, fClearWithSolution: 0);
                    if (hr != 0)
                    {
                        Debug.WriteLine($"[Logger] Failed to create output pane. HRESULT: {hr}");
                        return;
                    }

                    // Get the newly created pane
                    paneGuid = OutputPaneGuid;
                    hr = outputWindow.GetPane(ref paneGuid, out outputPane);
                    if (hr != 0 || outputPane == null)
                    {
                        Debug.WriteLine($"[Logger] Failed to get output pane after creation. HRESULT: {hr}");
                        return;
                    }
                }

                // Activate the pane to make it visible
                outputPane?.Activate();

                Debug.WriteLine("[Logger] Successfully initialized output pane");
                LogInfo("Logger", "Code Align logger initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logger] Exception during initialization: {ex}");
            }
        }

        public static void LogInfo(string source, string message)
            => LogInternal(LogLevel.Info, source, message);

        public static void LogError(string source, string message)
            => LogInternal(LogLevel.Error, source, message);

        public static void LogDebug(string source, string message)
            => LogInternal(LogLevel.Debug, source, message);

        private static void LogInternal(LogLevel level, string source, string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = "[Debug]";
            switch (level)
            {
                case LogLevel.Info:
                    prefix = "[Info]";
                    break;
                case LogLevel.Error:
                    prefix = "❌ [Error]";
                    break;
            }

            var formatted = $"{timestamp} {prefix} {source}: {message}";

            // Always write to Debug output (visible in debugger)
            Debug.WriteLine(formatted);

            // Write to Output pane (visible to users)
            WriteToOutputPane(formatted);
        }

        private static void WriteToOutputPane(string text)
        {
            if (outputPane == null)
            {
                // Output pane not available yet - only debug output will work
                return;
            }

            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    outputPane.OutputStringThreadSafe(text + Environment.NewLine);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logger] Error writing to output pane: {ex.Message}");
            }
        }

        private enum LogLevel
        {
            Debug,
            Info,
            Error
        }
    }
}
