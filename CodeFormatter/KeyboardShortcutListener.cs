using System;
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
    /// via keyboard shortcuts, then cancels the default command and applies Roslyn + custom alignment.
    /// </remarks>
    internal sealed class KeyboardShortcutListener : IDisposable
    {
        private readonly IWpfTextView textView;
        private readonly SVsServiceProvider serviceProvider;
        private EnvDTE.CommandEvents commandEvents;
        private bool isProcessing;

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
            ThreadHelper.ThrowIfNotOnUIThread();
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

                // Only attach if the command has key bindings
                if (cmd.Bindings == null)
                {
                    Logger.LogDebug("KeyboardShortcutListener", "Edit.FormatDocument has no bindings; listener will not attach");
                    return;
                }

                // Subscribe to BeforeExecute/AfterExecute to intercept the command
                commandEvents = dte.Events.get_CommandEvents(cmd.Guid, cmd.ID);
                commandEvents.BeforeExecute += OnBeforeExecute;
                commandEvents.AfterExecute += OnAfterExecute;

                Logger.LogDebug("KeyboardShortcutListener", "Attached to Edit.FormatDocument CommandEvents.BeforeExecute/AfterExecute");
            }
            catch (Exception ex)
            {
                Logger.LogError("KeyboardShortcutListener.TryAttachToFormatCommand", ex.ToString());
            }
        }

        /// <summary>
        /// Event handler called before the Format Document command executes
        /// </summary>
        private void OnBeforeExecute(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Prevent re-entry if we're already processing
            if (isProcessing)
                return;

            isProcessing = true;
        }

        private void OnAfterExecute(string guid, int id, object customIn, object customOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!isProcessing)
                return;

            try
            {
                if (textView.Properties.TryGetProperty(FormatCommandFilter.AlignmentAppliedKey, out object marker))
                {
                    textView.Properties.RemoveProperty(FormatCommandFilter.AlignmentAppliedKey);
                    if (marker is bool handled && handled)
                    {
                        return;
                    }
                }

                Logger.LogDebug("KeyboardShortcutListener.OnAfterExecute", "Applying alignment after Format Document command");

                var alignService = AlignServiceFactory.CreateFromOptions(serviceProvider);
                AlignmentHelper.ApplyAlignment(textView, serviceProvider, alignService, checkFormatOnSave: false, alignOnly: true);
            }
            catch (Exception ex)
            {
                Logger.LogError("KeyboardShortcutListener.OnAfterExecute", ex.ToString());
            }
            finally
            {
                isProcessing = false;
            }
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (commandEvents != null)
                {
                    commandEvents.BeforeExecute -= OnBeforeExecute;
                    commandEvents.AfterExecute -= OnAfterExecute;
                    commandEvents = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("KeyboardShortcutListener.Dispose", $"Error during cleanup: {ex.Message}");
            }
        }
    }
}
