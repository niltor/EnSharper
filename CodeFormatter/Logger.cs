using System;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.Shell;

namespace CodeFormatter
{
    internal static class Logger
    {
        private static readonly object sync = new object();
        private static readonly string logPath = Path.Combine(Path.GetTempPath(), "CodeFormatter.log");

        public static void LogInfo(string source, string message)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                ActivityLog.LogInformation(source, message);
            }
            catch
            {
                // ignore activity log errors
            }

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
        }

        public static void LogError(string source, string message)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                ActivityLog.LogError(source, message);
            }
            catch
            {
            }

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
            }
        }

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

        public static string LogFilePath => logPath;
    }
}
