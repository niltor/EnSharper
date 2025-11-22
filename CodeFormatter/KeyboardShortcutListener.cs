using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;

namespace CodeFormatter
{
    /// <summary>
    /// Listens to keyboard shortcuts for the Format Document command and applies alignment formatting
    /// </summary>
    /// <remarks>
    /// This class subscribes to DTE CommandEvents to detect when the user triggers Format Document
    /// via keyboard shortcuts, then applies alignment formatting after a configurable delay.
    /// </remarks>
    internal sealed class KeyboardShortcutListener : IDisposable
    {
        private readonly IWpfTextView textView;
        private readonly SVsServiceProvider serviceProvider;
        private EnvDTE.CommandEvents commandEvents;

        /// <summary>
        /// Initializes a new instance of the KeyboardShortcutListener class
        /// </summary>
        /// <param name="textView">The WPF text view to monitor and format</param>
        /// <param name="serviceProvider">The VS service provider for accessing VS services</param>
        public KeyboardShortcutListener(IWpfTextView textView, SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.textView = textView ?? throw new ArgumentNullException(nameof(textView));
            this.serviceProvider = serviceProvider;

            TryAttachToFormatCommand();
        }

        /// <summary>
        /// Attempts to attach to the Edit.FormatDocument command to monitor keyboard shortcuts
        /// </summary>
        private void TryAttachToFormatCommand()
        {
            try
            {
                var dte = serviceProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte == null)
                {
                    Logger.LogDebug("KeyboardShortcutListener", "DTE not available");
                    return;
                }

                var cmd = dte.Commands.Item("Edit.FormatDocument");
                if (cmd == null)
                {
                    Logger.LogDebug("KeyboardShortcutListener", "Edit.FormatDocument command not found");
                    return;
                }

                // Only attach if the command has key bindings (so we approximate keyboard-trigger intent)
                if (cmd.Bindings == null)
                {
                    Logger.LogDebug("KeyboardShortcutListener", "Edit.FormatDocument has no bindings; listener will not attach");
                    return;
                }

                // Subscribe to AfterExecute for this specific command
                commandEvents = dte.Events.get_CommandEvents(cmd.Guid, cmd.ID);
                commandEvents.AfterExecute += OnAfterExecute;

                Logger.LogDebug("KeyboardShortcutListener", "Attached to Edit.FormatDocument CommandEvents.AfterExecute");
            }
            catch (Exception ex)
            {
                Logger.LogError("KeyboardShortcutListener.TryAttachToFormatCommand", ex.ToString());
            }
        }

        /// <summary>
        /// Event handler called after the Format Document command executes
        /// </summary>
        /// <param name="guid">The command GUID</param>
        /// <param name="id">The command ID</param>
        /// <param name="customIn">Custom input parameter</param>
        /// <param name="customOut">Custom output parameter</param>
        private void OnAfterExecute(string guid, int id, object customIn, object customOut)
        {
            // This event handler is invoked on the UI thread by DTE
            // We need to ensure we're on the UI thread for VS service access
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // Get delay configuration while on UI thread (VSTHRD010 fix)
                int delayMs = 100;
                try
                {
                    var shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
                    if (shell != null)
                    {
                        var packageGuid = new Guid(CodeFormatterPackage.PackageGuidString);
                        if (shell.IsPackageLoaded(ref packageGuid, out IVsPackage pkg) == VSConstants.S_OK && pkg is CodeFormatterPackage package)
                        {
                            var opts = package.GetDialogPage(typeof(AlignOptions)) as AlignOptions;
                            if (opts != null)
                                delayMs = Math.Max(0, opts.FormatCommandDelayMs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("KeyboardShortcutListener.OnAfterExecute", $"Error getting delay configuration: {ex.Message}");
                }

                // Schedule async work using JoinableTaskFactory
                var jtf = ThreadHelper.JoinableTaskFactory;
                _ = jtf.RunAsync(async () =>
                {
                    try
                    {
                        // Delay on background thread to avoid blocking UI
                        await Task.Delay(delayMs).ConfigureAwait(false);
                        
                        // Switch back to UI thread before calling AlignmentHelper (VSTHRD010 fix)
                        await jtf.SwitchToMainThreadAsync();
                        
                        // Create AlignService with configured options
                        var alignService = AlignServiceFactory.CreateFromOptions(serviceProvider);
                        
                        AlignmentHelper.ApplyAlignment(textView, serviceProvider, alignService, checkFormatOnSave: false);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("KeyboardShortcutListener.OnAfterExecute", ex.ToString());
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("KeyboardShortcutListener.OnAfterExecute", ex.ToString());
            }
        }

        public void Dispose()
        {
            // Note: COM event unsubscription can be called from any thread
            // but we should be safe here as we're just removing a handler
            try
            {
                if (commandEvents != null)
                {
                    commandEvents.AfterExecute -= OnAfterExecute;
                    commandEvents = null;
                }
            }
            catch (Exception ex)
            {
                // Log errors during cleanup, but don't throw
                Logger.LogError("KeyboardShortcutListener.Dispose", $"Error during cleanup: {ex.Message}");
            }
        }
    }
}
