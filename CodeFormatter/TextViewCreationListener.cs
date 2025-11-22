using Microsoft.VisualStudio.Shell;
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



        public void TextViewCreated(IWpfTextView textView)
        {
            // Ensure we are on UI thread before interacting with VS services / editor
            ThreadHelper.ThrowIfNotOnUIThread();

            // Hook up the format command filter
            FormatCommandFilter.AddFilterToView(textView, ServiceProvider);

            // Hook up the save listener
            var saveListener = DocumentSaveListener.Create(textView, ServiceProvider);

            // Store the listener in the text view properties so it doesn't get garbage collected
            textView.Properties.GetOrCreateSingletonProperty(
                typeof(DocumentSaveListener),
                () => saveListener
            );

            // Clean up when the text view is closed
            textView.Closed += (sender, args) =>
            {
                // Closed is raised on the UI thread, but assert to satisfy analyzers
                ThreadHelper.ThrowIfNotOnUIThread();


                if (textView.Properties.TryGetProperty(typeof(DocumentSaveListener), out DocumentSaveListener listener))
                {
                    listener.Dispose();
                    textView.Properties.RemoveProperty(typeof(DocumentSaveListener));
                }
            };
        }
    }
}
