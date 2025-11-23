using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace CodeFormatter
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(CodeFormatterPackage.PackageGuidString)]
    [ProvideOptionPage(typeof(AlignOptions), "Code Align", "General", 0, 0, true, SupportsProfiles = true)]
    public sealed class CodeFormatterPackage : AsyncPackage
    {
        /// <summary>
        /// CodeFormatterPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "d48d6a6a-75ee-4ea1-a815-2f1d6e6083f9";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        }

        #endregion
    }
}
