using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Settings;

namespace CodeAlign.Configuration
{
    internal static class AlignmentOptions
    {
        [VisualStudioContribution]
        public static SettingCategory CodeAlignCategory { get; } = new("codeAlign", "Code Align");

        [VisualStudioContribution]
        public static SettingCategory GeneralCategory { get; } = new("general", "General", CodeAlignCategory);

        [VisualStudioContribution]
        public static SettingCategory AlignmentCategory { get; } = new("alignment", "Alignment", CodeAlignCategory);

        [VisualStudioContribution]
        public static SettingCategory ParametersCategory { get; } = new("parameters", "Parameters", CodeAlignCategory);

        [VisualStudioContribution]
        public static Setting.Boolean EnablePlugin { get; } = new("enablePlugin", "Enable Plugin", GeneralCategory, defaultValue: true);

        [VisualStudioContribution]
        public static Setting.Integer MaxAlignmentGap { get; } = new("maxAlignmentGap", "Max Alignment Gap", AlignmentCategory, defaultValue: 50);

        [VisualStudioContribution]
        public static Setting.Integer MaxFileSizeBytes { get; } = new("maxFileSizeBytes", "Max File Size (bytes)", AlignmentCategory, defaultValue: 1024 * 1024);

        [VisualStudioContribution]
        public static Setting.Integer ConstructorParameterThreshold { get; } = new("constructorParameterThreshold", "Constructor Parameter Threshold", ParametersCategory, defaultValue: 3);

        [VisualStudioContribution]
        public static Setting.Integer MethodParameterThreshold { get; } = new("methodParameterThreshold", "Method Parameter Threshold", ParametersCategory, defaultValue: 4);
    }
}
