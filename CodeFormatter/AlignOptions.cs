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
        [DisplayName("Enable Align")]
        [Description("Controls whether the alignment feature is enabled")]
        public bool EnableAlign { get; set; } = true;
    }
}
