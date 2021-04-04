using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace FuturaPlanConfigWarden.Windows {

    [Guid("b10c1161-4012-4ac3-a666-3c799898d878")]
    public class FuturaPlanConfigWardenWindow : ToolWindowPane {

        public const string Title = "Config warden";

        public FuturaPlanConfigWardenWindow(WindowState state) : base(null) {
            Caption = Title;
            BitmapImageMoniker = KnownMonikers.ImageIcon;

            Content = new FuturaPlanConfigWardenControl(state);
        }
    }
}
