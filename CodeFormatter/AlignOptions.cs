using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace CodeFormatter
{
    /// <summary>
    /// Options page for the Code Align extension
    /// </summary>
    public class AlignOptions : DialogPage
    {
        [Category("General")]
        [DisplayName("Enable Plugin")]
        [Description("Controls whether the plugin is active")]
        public bool EnablePlugin { get; set; } = true;

        [Category("General")]
        [DisplayName("Format On Save")]
        [Description("Automatically apply alignment formatting when saving files")]
        public bool FormatOnSave { get; set; } = true;

        [Category("General")]
        [DisplayName("Format Command Delay (ms)")]
        [Description("Delay in milliseconds to wait after a format command before applying alignment. Increase if other formatters run asynchronously.")]
        public int FormatCommandDelayMs { get; set; } = 100;
    }
}
