using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Settings;

namespace CodeAlign.Configuration
{
    internal static class AlignmentOptions
    {
        [VisualStudioContribution]
        public static SettingCategory CodeAlignCategory { get; } = new("codeAlign", "%CodeAlign.Settings.Category.CodeAlign%");

        [VisualStudioContribution]
        public static SettingCategory GeneralCategory { get; } = new("general", "%CodeAlign.Settings.Category.General%", CodeAlignCategory);

        [VisualStudioContribution]
        public static SettingCategory AlignmentCategory { get; } = new("alignment", "%CodeAlign.Settings.Category.Alignment%", CodeAlignCategory);

        [VisualStudioContribution]
        public static SettingCategory ParametersCategory { get; } = new("parameters", "%CodeAlign.Settings.Category.Parameters%", CodeAlignCategory);

        [VisualStudioContribution]
        public static Setting.Boolean EnablePlugin { get; } = new("enablePlugin", "%CodeAlign.Settings.EnablePlugin.DisplayName%", GeneralCategory, defaultValue: true);

        [VisualStudioContribution]
        public static Setting.Integer MaxAlignmentGap { get; } = new("maxAlignmentGap", "%CodeAlign.Settings.MaxAlignmentGap.DisplayName%", AlignmentCategory, defaultValue: 50);

        [VisualStudioContribution]
        public static Setting.Integer MaxFileSizeBytes { get; } = new("maxFileSizeBytes", "%CodeAlign.Settings.MaxFileSizeBytes.DisplayName%", AlignmentCategory, defaultValue: 1024 * 1024);

        [VisualStudioContribution]
        public static Setting.Integer ConstructorParameterThreshold { get; } = new("constructorParameterThreshold", "%CodeAlign.Settings.ConstructorParameterThreshold.DisplayName%", ParametersCategory, defaultValue: 3);

        [VisualStudioContribution]
        public static Setting.Integer MethodParameterThreshold { get; } = new("methodParameterThreshold", "%CodeAlign.Settings.MethodParameterThreshold.DisplayName%", ParametersCategory, defaultValue: 4);
    }
}
