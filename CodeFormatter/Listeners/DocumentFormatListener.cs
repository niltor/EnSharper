using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CodeFormatter
{
    /// <summary>
    /// Global singleton listener for Format Document keyboard shortcuts.
    /// Intercepts BeforeExecute to apply IDE formatting + custom alignment in single pass,
    /// then cancels the IDE's default formatting to avoid double editing.
    /// </summary>
    internal sealed class DocumentFormatListener : IDisposable
    {
        private static DocumentFormatListener instance;
        private static readonly object lockObject = new object();

        private readonly SVsServiceProvider serviceProvider;
        private EnvDTE.CommandEvents commandEvents;
        private string lastFormattedContent = null;

        private DocumentFormatListener(SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.serviceProvider = serviceProvider;
            TryAttachToFormatCommand();
        }

        public static DocumentFormatListener GetOrCreate(SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            lock (lockObject)
            {
                if (instance == null)
                {
                    instance = new DocumentFormatListener(serviceProvider);
                    Logger.LogDebug("DocumentFormatListener", "Global instance created");
                }
                return instance;
            }
        }

        private void TryAttachToFormatCommand()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var dte = serviceProvider.GetService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte == null)
                {
                    Logger.LogDebug("DocumentFormatListener", "DTE not available");
                    return;
                }

                var cmd = dte.Commands.Item("Edit.FormatDocument");
                if (cmd == null)
                {
                    Logger.LogDebug(
                        "DocumentFormatListener",
                        "Edit.FormatDocument command not found"
                    );
                    return;
                }

                if (cmd.Bindings == null)
                {
                    Logger.LogDebug(
                        "DocumentFormatListener",
                        "Edit.FormatDocument has no bindings"
                    );
                    return;
                }

                commandEvents = dte.Events.get_CommandEvents(cmd.Guid, cmd.ID);
                commandEvents.BeforeExecute += OnBeforeExecute;

                Logger.LogDebug(
                    "DocumentFormatListener",
                    "Attached to Edit.FormatDocument CommandEvents (BeforeExecute)"
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    "DocumentFormatListener.TryAttachToFormatCommand",
                    ex.ToString()
                );
            }
        }

        private void OnBeforeExecute(string guid, int id, object customIn, object customOut, ref bool cancelDefault)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            cancelDefault = true;
            try
            {
                var textView = GetActiveTextView();
                if (textView == null)
                {
                    Logger.LogDebug("DocumentFormatListener", "No active text view found");
                    return;
                }

                var textBuffer = textView.TextBuffer;
                if (textBuffer == null)
                {
                    return;
                }

                var contentBeforeFormat = textBuffer.CurrentSnapshot.GetText();

                if (lastFormattedContent == contentBeforeFormat)
                {
                    Logger.LogDebug(
                        "DocumentFormatListener",
                        "Content unchanged since last format - skipping"
                    );
                    return;
                }

                Logger.LogDebug(
                    "DocumentFormatListener",
                    "Intercepting Format Document: applying IDE formatting + custom alignment in SINGLE PASS"
                );

                // Apply both IDE formatting + custom alignment in one edit
                var alignService = AlignServiceFactory.CreateFromOptions(serviceProvider);
                bool formatted = FormattingCoordinator.TryFormat(
                    textView,
                    serviceProvider,
                    alignService,
                    includeIDFormatting: true
                );

                if (formatted)
                {
                    lastFormattedContent = textBuffer.CurrentSnapshot.GetText();
                    Logger.LogDebug(
                        "DocumentFormatListener",
                        "Combined formatting applied successfully in single text edit"
                    );

                    // Cancel the IDE's default formatting since we've already handled it
                    Logger.LogDebug(
                        "DocumentFormatListener",
                        "IDE default formatting cancelled (CancelDefault=true)"
                    );
                }
                else
                {
                    lastFormattedContent = contentBeforeFormat;
                    Logger.LogDebug(
                        "DocumentFormatListener",
                        "No formatting changes needed"
                    );

                    // Allow IDE to proceed with default formatting
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("DocumentFormatListener.OnBeforeExecute", ex.ToString());
                // On error, allow IDE to proceed with default formatting
            }
        }

        private IWpfTextView GetActiveTextView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var componentModel =
                    serviceProvider.GetService(
                        typeof(Microsoft.VisualStudio.ComponentModelHost.SComponentModel)
                    ) as Microsoft.VisualStudio.ComponentModelHost.IComponentModel;
                if (componentModel == null)
                    return null;

                var editorAdapterFactory =
                    componentModel.GetService<Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService>();
                if (editorAdapterFactory == null)
                    return null;

                var textManager =
                    serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
                if (textManager == null)
                    return null;

                IVsTextView vsTextView;
                int hr = textManager.GetActiveView(1, null, out vsTextView);
                if (hr != VSConstants.S_OK || vsTextView == null)
                    return null;

                return editorAdapterFactory.GetWpfTextView(vsTextView);
            }
            catch (Exception ex)
            {
                Logger.LogError("DocumentFormatListener.GetActiveTextView", ex.ToString());
                return null;
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
                    commandEvents = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("DocumentFormatListener.Dispose", ex.ToString());
            }
        }
    }
}
