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

        [Category("Alignment")]
        [DisplayName("Maximum Alignment Gap")]
        [Description("Maximum number of spaces to add for alignment (prevents excessive spacing). Set to 0 for unlimited.")]
        public int MaxAlignmentGap { get; set; } = 50;

        [Category("Alignment")]
        [DisplayName("Constructor Parameter Threshold")]
        [Description("Align constructor parameters when count is greater than or equal to this threshold (default: 3)")]
        public int ConstructorParameterThreshold { get; set; } = 3;

        [Category("Alignment")]
        [DisplayName("Method Parameter Threshold")]
        [Description("Align method parameters when count is greater than or equal to this threshold (default: 4)")]
        public int MethodParameterThreshold { get; set; } = 4;

        [Category("Performance")]
        [DisplayName("Maximum File Size (bytes)")]
        [Description("Maximum file size in bytes to process for alignment. Files larger than this will be skipped.")]
        public int MaxFileSizeBytes { get; set; } = 1024 * 1024; // 1MB
    }
}
