using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CodeFormatter
{
    /// <summary>
    /// Helper class for creating configured AlignService instances
    /// </summary>
    internal static class AlignServiceFactory
    {
        /// <summary>
        /// Creates an AlignService with configured options from the package
        /// </summary>
        /// <param name="serviceProvider">The VS service provider</param>
        /// <returns>AlignService with configured options, or default if configuration cannot be retrieved</returns>
        public static AlignService CreateFromOptions(SVsServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var options = GetAlignOptions(serviceProvider);
                if (options != null)
                {
                    var settings = new AlignmentSettings(
                        options.MaxFileSizeBytes,
                        options.MaxAlignmentGap,
                        options.ConstructorParameterThreshold,
                        options.MethodParameterThreshold);

                    return new AlignService(settings);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("AlignServiceFactory", ex.ToString());
            }

            return new AlignService();
        }

        internal static AlignDialogPage GetAlignOptions(SVsServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                return null;

            var shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
            if (shell == null)
                return null;

            var packageGuid = new Guid(CodeFormatterPackage.PackageGuidString);
            if (shell.IsPackageLoaded(ref packageGuid, out IVsPackage pkg) != VSConstants.S_OK)
            {
                if (shell.LoadPackage(ref packageGuid, out pkg) != VSConstants.S_OK)
                    return null;
            }

            if (pkg is CodeFormatterPackage package)
            {
                return package.GetDialogPage(typeof(AlignDialogPage)) as AlignDialogPage;
            }

            return null;
        }
    }
}
