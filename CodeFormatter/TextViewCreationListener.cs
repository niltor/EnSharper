using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

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
            try
            {
                System.Diagnostics.Debug.WriteLine("[CodeFormatter] TextViewCreated - Starting initialization");
                ActivityLog.LogInformation("CodeFormatter.TextViewCreationListener", "TextViewCreated called - initializing extension components");

                // Hook up the format command filter
                System.Diagnostics.Debug.WriteLine("[CodeFormatter] Adding format command filter");
                FormatCommandFilter.AddFilterToView(textView, ServiceProvider);

                // Hook up the save listener
                System.Diagnostics.Debug.WriteLine("[CodeFormatter] Creating document save listener");
                var saveListener = DocumentSaveListener.Create(textView, ServiceProvider);

                // Store the listener in the text view properties so it doesn't get garbage collected
                textView.Properties.GetOrCreateSingletonProperty(
                    typeof(DocumentSaveListener),
                    () => saveListener
                );

                // Clean up when the text view is closed
                textView.Closed += (sender, args) =>
                {
                    System.Diagnostics.Debug.WriteLine("[CodeFormatter] Text view closed - cleaning up");
                    if (textView.Properties.TryGetProperty(typeof(DocumentSaveListener), out DocumentSaveListener listener))
                    {
                        listener.Dispose();
                        textView.Properties.RemoveProperty(typeof(DocumentSaveListener));
                    }
                };

                System.Diagnostics.Debug.WriteLine("[CodeFormatter] TextViewCreated - Initialization complete");
                ActivityLog.LogInformation("CodeFormatter.TextViewCreationListener", "Extension components initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodeFormatter] ERROR in TextViewCreated: {ex}");
                ActivityLog.LogError("CodeFormatter.TextViewCreationListener", $"Error initializing extension: {ex}");
            }
        }
    }
}
