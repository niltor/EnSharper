using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Diagnostics;

namespace CodeFormatter
{
    /// <summary>
    /// Text view creation listener for hooking into editor
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("CSharp")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class TextViewCreationListener : IWpfTextViewCreationListener
    {
        [Import]
        internal SVsServiceProvider ServiceProvider { get; set; }

        [Import]
        internal ITextDocumentFactoryService TextDocumentFactory { get; set; }

        public void TextViewCreated(IWpfTextView textView)
        {
            // Ensure we are on UI thread before interacting with VS services / editor
            ThreadHelper.ThrowIfNotOnUIThread();

            Logger.LogDebug("TextViewCreationListener", $"TextViewCreated for {textView.GetHashCode()}");

            // Initialize global listeners (singletons, only created once)
            GlobalDocumentSaveListener.GetOrCreate(ServiceProvider);
            GlobalKeyboardShortcutListener.GetOrCreate(ServiceProvider);

            // No cleanup needed for global listeners - they persist for the VS session
            Logger.LogDebug("TextViewCreationListener", $"TextView {textView.GetHashCode()} initialized");
        }
    }
}
