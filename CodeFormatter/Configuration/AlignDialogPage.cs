using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CodeFormatter
{
    /// <summary>
    /// Options page for the Code Align extension
    /// </summary>
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class AlignDialogPage : DialogPage
    {
        [LocCategory("CategoryGeneral")]
        [LocDisplayName("EnablePluginDisplayName")]
        [LocDescription("EnablePluginDescription")]
        public bool EnablePlugin { get; set; } = true;

        [LocCategory("CategoryGeneral")]
        [LocDisplayName("FormatOnSaveDisplayName")]
        [LocDescription("FormatOnSaveDescription")]
        public bool FormatOnSave { get; set; } = true;

        [LocCategory("CategoryAlignment")]
        [LocDisplayName("MaxAlignmentGapDisplayName")]
        [LocDescription("MaxAlignmentGapDescription")]
        public int MaxAlignmentGap { get; set; } = 10;

        [LocCategory("CategoryAlignment")]
        [LocDisplayName("MaxFileSizeDisplayName")]
        [LocDescription("MaxFileSizeDescription")]
        public int MaxFileSizeBytes { get; set; } = 1024 * 1024; // 1MB

        [LocCategory("CategoryParameters")]
        [LocDisplayName("ConstructorThresholdDisplayName")]
        [LocDescription("ConstructorThresholdDescription")]
        public int ConstructorParameterThreshold { get; set; } = 3;

        [LocCategory("CategoryParameters")]
        [LocDisplayName("MethodThresholdDisplayName")]
        [LocDescription("MethodThresholdDescription")]
        public int MethodParameterThreshold { get; set; } = 4;

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
        }
        
    }

    /// <summary>
    /// Provides localized category names
    /// </summary>
    internal class LocCategoryAttribute : CategoryAttribute
    {
        public LocCategoryAttribute(string resourceKey)
            : base(resourceKey)
        {
        }

        protected override string GetLocalizedString(string value)
        {
            string localized = Resources.ResourceManager.GetString(value);
            return string.IsNullOrEmpty(localized) ? value : localized;
        }
    }

    /// <summary>
    /// Provides localized display names for properties
    /// </summary>
    internal class LocDisplayNameAttribute : DisplayNameAttribute
    {
        private readonly string _resourceKey;

        public LocDisplayNameAttribute(string resourceKey)
            : base()
        {
            _resourceKey = resourceKey;
        }

        public override string DisplayName
        {
            get
            {
                string displayName = Resources.ResourceManager.GetString(_resourceKey);
                return string.IsNullOrEmpty(displayName) ? _resourceKey : displayName;
            }
        }
    }

    /// <summary>
    /// Provides localized descriptions for properties
    /// </summary>
    internal class LocDescriptionAttribute : DescriptionAttribute
    {
        private readonly string _resourceKey;

        public LocDescriptionAttribute(string resourceKey)
            : base()
        {
            _resourceKey = resourceKey;
        }

        public override string Description
        {
            get
            {
                string description = Resources.ResourceManager.GetString(_resourceKey);
                return string.IsNullOrEmpty(description) ? _resourceKey : description;
            }
        }
    }
}
