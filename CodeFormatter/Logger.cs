using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.Shell;

namespace CodeFormatter
{
    /// <summary>
    /// Provides logging functionality for the CodeFormatter extension
    /// </summary>
    /// <remarks>
    /// This logger writes to both the Visual Studio ActivityLog and a local file.
    /// It handles thread-safety automatically, using JTF when necessary.
    /// </remarks>
    internal static class Logger
    {
        private static readonly object sync = new object();
        private static readonly string logPath = Path.Combine(Path.GetTempPath(), "CodeFormatter.log");

        /// <summary>
        /// Logs an informational message to both ActivityLog and file
        /// </summary>
        /// <param name="source">The source component generating the log</param>
        /// <param name="message">The message to log</param>
        public static void LogInfo(string source, string message)
        {
            // File logging doesn't require UI thread
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Info] {source}: {message}");
                lock (sync)
                {
                    File.AppendAllText(logPath, $"{DateTime.Now:O} [Info] {source}: {message}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch
            {
                // swallow file IO errors
            }

            // ActivityLog requires UI thread - use JTF for thread-safe access
            if (ThreadHelper.CheckAccess())
            {
                try
                {
                    ActivityLog.LogInformation(source, message);
                }
                catch
                {
                    // ignore activity log errors
                }
            }
            else
            {
                // If not on UI thread, schedule ActivityLog call on UI thread
                // Using fire-and-forget pattern - exceptions are caught inside the async lambda
                try
                {
                    _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        try
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            ActivityLog.LogInformation(source, message);
                        }
                        catch
                        {
                            // ignore activity log errors
                        }
                    });
                }
                catch
                {
                    // If even starting the async operation fails, log to debug only
                    System.Diagnostics.Debug.WriteLine($"Failed to queue ActivityLog.LogInformation for: {source}");
                }
            }
        }

        /// <summary>
        /// Logs an error message to both ActivityLog and file
        /// </summary>
        /// <param name="source">The source component generating the log</param>
        /// <param name="message">The error message to log</param>
        public static void LogError(string source, string message)
        {
            // File logging doesn't require UI thread
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Error] {source}: {message}");
                lock (sync)
                {
                    File.AppendAllText(logPath, $"{DateTime.Now:O} [Error] {source}: {message}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch
            {
                // swallow file IO errors
            }

            // ActivityLog requires UI thread - use JTF for thread-safe access
            if (ThreadHelper.CheckAccess())
            {
                try
                {
                    ActivityLog.LogError(source, message);
                }
                catch
                {
                    // ignore activity log errors
                }
            }
            else
            {
                // If not on UI thread, schedule ActivityLog call on UI thread
                // Using fire-and-forget pattern - exceptions are caught inside the async lambda
                try
                {
                    _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        try
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            ActivityLog.LogError(source, message);
                        }
                        catch
                        {
                            // ignore activity log errors
                        }
                    });
                }
                catch
                {
                    // If even starting the async operation fails, log to debug only
                    System.Diagnostics.Debug.WriteLine($"Failed to queue ActivityLog.LogError for: {source}");
                }
            }
        }

        /// <summary>
        /// Logs a debug message to Debug output and file
        /// </summary>
        /// <param name="source">The source component generating the log</param>
        /// <param name="message">The debug message to log</param>
        public static void LogDebug(string source, string message)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Debug] {source}: {message}");
                lock (sync)
                {
                    File.AppendAllText(logPath, $"{DateTime.Now:O} [Debug] {source}: {message}{Environment.NewLine}", Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Gets the path to the log file
        /// </summary>
        public static string LogFilePath => logPath;
    }
}
