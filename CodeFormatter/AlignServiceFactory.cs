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
                var shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
                if (shell != null)
                {
                    var packageGuid = new Guid(CodeFormatterPackage.PackageGuidString);
                    if (shell.IsPackageLoaded(ref packageGuid, out IVsPackage pkg) == VSConstants.S_OK 
                        && pkg is CodeFormatterPackage package)
                    {
                        var opts = package.GetDialogPage(typeof(AlignOptions)) as AlignOptions;
                        if (opts != null)
                        {
                            return new AlignService(
                                opts.MaxFileSizeBytes,
                                opts.MaxAlignmentGap,
                                opts.ConstructorParameterThreshold,
                                opts.MethodParameterThreshold
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug("AlignServiceFactory", $"Failed to retrieve configuration, using defaults: {ex.Message}");
            }
            
            // Fall back to default configuration
            return new AlignService();
        }
    }
}
