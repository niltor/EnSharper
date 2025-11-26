namespace CodeAlign.Configuration
{
    /// <summary>
    /// Encapsulates the alignment configuration that is shared between the formatter and processors.
    /// </summary>
    internal sealed class AlignmentSettings
    {
        public static AlignmentSettings Default { get; } = new AlignmentSettings(true, 1024 * 1024, 50, 3, 4);

        public AlignmentSettings(
            bool isEnable,
            int maxFileSizeBytes,
            int maxAlignmentGap,
            int constructorParameterThreshold,
            int methodParameterThreshold)
        {
            IsEnabled = isEnable;
            MaxFileSizeBytes = maxFileSizeBytes > 0 ? maxFileSizeBytes : 0;
            MaxAlignmentGap = Math.Max(0, maxAlignmentGap);
            ConstructorParameterThreshold = Math.Max(2, constructorParameterThreshold);
            MethodParameterThreshold = Math.Max(2, methodParameterThreshold);
        }

        public int MaxFileSizeBytes { get; }

        public int MaxAlignmentGap { get; }

        public int ConstructorParameterThreshold { get; }

        public int MethodParameterThreshold { get; }

        public bool HasFileSizeLimit => MaxFileSizeBytes > 0;

        public bool IsEnabled { get; set; }

        public bool IsWithinFileSizeLimit(int textLength)
            => !HasFileSizeLimit || textLength <= MaxFileSizeBytes;
    }
}
