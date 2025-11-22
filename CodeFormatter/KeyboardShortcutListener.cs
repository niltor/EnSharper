using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;

namespace CodeFormatter
{
    internal sealed class KeyboardShortcutListener : IDisposable
    {
        private readonly IWpfTextView textView;
        private readonly SVsServiceProvider serviceProvider;
        private EnvDTE.CommandEvents commandEvents;

        public KeyboardShortcutListener(IWpfTextView textView, SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.textView = textView ?? throw new ArgumentNullException(nameof(textView));
            this.serviceProvider = serviceProvider;

            TryAttachToFormatCommand();
        }

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

        private void OnAfterExecute(string guid, int id, object customIn, object customOut)
        {
            try
            {
                // Schedule delayed alignment using configured option
                int delayMs = 100;
                try
                {
                    var shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
                    if (shell != null && shell.IsPackageLoaded(new Guid(CodeFormatterPackage.PackageGuidString), out IVsPackage pkg) == VSConstants.S_OK && pkg is CodeFormatterPackage package)
                    {
                        var opts = package.GetDialogPage(typeof(AlignOptions)) as AlignOptions;
                        if (opts != null)
                            delayMs = Math.Max(0, opts.FormatCommandDelayMs);
                    }
                }
                catch { }

                var jtf = ThreadHelper.JoinableTaskFactory;
                _ = jtf.RunAsync(async () =>
                {
                    try
                    {
                        await Task.Delay(delayMs).ConfigureAwait(false);
                        await jtf.SwitchToMainThreadAsync();
                        AlignmentHelper.ApplyAlignment(textView, serviceProvider, new AlignService(), checkFormatOnSave: false);
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
            try
            {
                if (commandEvents != null)
                {
                    commandEvents.AfterExecute -= OnAfterExecute;
                    commandEvents = null;
                }
            }
            catch { }
        }
    }
}
