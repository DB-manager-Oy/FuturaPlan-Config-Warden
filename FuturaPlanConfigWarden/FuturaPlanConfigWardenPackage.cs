using FuturaPlanConfigWarden.Windows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace FuturaPlanConfigWarden {

    [Guid(PackageGuidString)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideToolWindow(typeof(FuturaPlanConfigWardenWindow), Style = VsDockStyle.Tabbed, DockedWidth = 300, Window = "DocumentWell", Orientation = ToolWindowOrientation.Left)]
    [InstalledProductRegistration("FuturaPlan Config Warden", "Wardens your config", "1.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class FuturaPlanConfigWardenPackage : AsyncPackage {
        public const string PackageGuidString = "c3a52f66-040e-47b1-bd50-1a6a194ff72a";
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            await FuturaPlanConfigWardenCommand.InitializeAsync(this);
        }

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType) {
            return toolWindowType.Equals(Guid.Parse("b10c1161-4012-4ac3-a666-3c799898d878")) ? this : null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id) {
            return toolWindowType == typeof(FuturaPlanConfigWardenWindow) ? FuturaPlanConfigWardenWindow.Title : base.GetToolWindowTitle(toolWindowType, id);
        }

        protected override async Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken) {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var dte = await GetServiceAsync(typeof(EnvDTE.DTE));

            return new WindowState {
                DTE = (EnvDTE.DTE)dte
            };
        }
    }
}
