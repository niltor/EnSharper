using System;
using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace CodeFormatter
{
    /// <summary>
    /// Global singleton listener for Format Document keyboard shortcuts
    /// </summary>
    internal sealed class GlobalKeyboardShortcutListener : IDisposable
    {
        private static GlobalKeyboardShortcutListener instance;
        private static readonly object lockObject = new object();

        private readonly SVsServiceProvider serviceProvider;
        private EnvDTE.CommandEvents commandEvents;

        private GlobalKeyboardShortcutListener(SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.serviceProvider = serviceProvider;
            TryAttachToFormatCommand();
        }

        public static GlobalKeyboardShortcutListener GetOrCreate(SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            lock (lockObject)
            {
                if (instance == null)
                {
                    instance = new GlobalKeyboardShortcutListener(serviceProvider);
                    Logger.LogDebug("GlobalKeyboardShortcutListener", "Global instance created");
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
                    Logger.LogDebug("GlobalKeyboardShortcutListener", "DTE not available");
                    return;
                }

                var cmd = dte.Commands.Item("Edit.FormatDocument");
                if (cmd == null)
                {
                    Logger.LogDebug("GlobalKeyboardShortcutListener", "Edit.FormatDocument command not found");
                    return;
                }

                if (cmd.Bindings == null)
                {
                    Logger.LogDebug("GlobalKeyboardShortcutListener", "Edit.FormatDocument has no bindings");
                    return;
                }

                commandEvents = dte.Events.get_CommandEvents(cmd.Guid, cmd.ID);
                commandEvents.AfterExecute += OnAfterExecute;

                Logger.LogDebug("GlobalKeyboardShortcutListener", "Attached to Edit.FormatDocument CommandEvents");
            }
            catch (Exception ex)
            {
                Logger.LogError("GlobalKeyboardShortcutListener.TryAttachToFormatCommand", ex.ToString());
            }
        }

        private void OnAfterExecute(string guid, int id, object customIn, object customOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // Get active text view
                var textView = GetActiveTextView();
                if (textView == null)
                {
                    Logger.LogDebug("GlobalKeyboardShortcutListener", "No active text view found");
                    return;
                }

                Logger.LogDebug("GlobalKeyboardShortcutListener", "Applying alignment to active text view");

                var alignService = AlignServiceFactory.CreateFromOptions(serviceProvider);
                FormattingCoordinator.TryFormat(textView, serviceProvider, alignService);
                
                Logger.LogDebug("GlobalKeyboardShortcutListener", "Alignment applied successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("GlobalKeyboardShortcutListener.OnAfterExecute", ex.ToString());
            }
        }

        private IWpfTextView GetActiveTextView()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var componentModel = serviceProvider.GetService(typeof(Microsoft.VisualStudio.ComponentModelHost.SComponentModel))
                    as Microsoft.VisualStudio.ComponentModelHost.IComponentModel;
                if (componentModel == null)
                    return null;

                var editorAdapterFactory = componentModel.GetService<Microsoft.VisualStudio.Editor.IVsEditorAdaptersFactoryService>();
                if (editorAdapterFactory == null)
                    return null;

                var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager;
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
                Logger.LogError("GlobalKeyboardShortcutListener.GetActiveTextView", ex.ToString());
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
                    commandEvents.AfterExecute -= OnAfterExecute;
                    commandEvents = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("GlobalKeyboardShortcutListener.Dispose", ex.ToString());
            }
        }
    }
}
