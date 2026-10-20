using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Configuration;

namespace WinformsMVP.Samples.CustomizationDemo
{
    /// <summary>
    /// Everything this demo customizes, in one place — what an application writes in Program.cs:
    /// <code>MvpApplication.Configure(framework: CustomizationDemoSetup.Configure, services: ...)</code>
    /// </summary>
    public static class CustomizationDemoSetup
    {
        public static void Configure(WinformsMvpBuilder mvp)
        {
            // Every message box (presenter Messages and AnchoredMessageBox) uses this dialog.
            mvp.Messages.UseDialogs(new CompanyStyleDialogBackend());
            mvp.Messages.DefaultCaption = "Contoso CRM";

            // Toast defaults and an app-wide painter.
            mvp.Toasts.Duration = 4500;
            mvp.Toasts.Position = ToastPosition.TopRight;
            mvp.Toasts.UseRenderer(new BrandToastRenderer());

            // A third-party compare library plugs in through mvp.ChangeTracking.UseComparer(adapter);
            // the adapter maps the tracker's ignores onto the library's settings, e.g. CompareNETObjects:
            //   sealed class CompareNetObjectsComparer : IObjectContentComparer
            //   {
            //       public bool ContentEquals(object a, object b, ChangeTrackingIgnores ignores)
            //       {
            //           var config = new ComparisonConfig();
            //           foreach (var m in ignores.Members)     config.MembersToIgnore.Add(m.Owner.Name + "." + m.Name);
            //           foreach (var n in ignores.MemberNames) config.MembersToIgnore.Add(n);
            //           foreach (var t in ignores.Types)       config.TypesToIgnore.Add(t);
            //           return new CompareLogic(config).Compare(a, b).AreEqual;
            //       }
            //   }
        }
    }

    /// <summary>A minimal app-wide toast painter: dark card with a brand-colored stripe.</summary>
    public sealed class BrandToastRenderer : ToastRenderer
    {
        private static readonly Color Brand = Color.FromArgb(0, 120, 110);

        public override void Render(ToastRenderContext context)
        {
            var g = context.Graphics;
            var bounds = context.Bounds;
            using (var background = new SolidBrush(Color.FromArgb(32, 36, 40)))
            using (var stripe = new SolidBrush(Brand))
            {
                g.FillRectangle(background, bounds);
                g.FillRectangle(stripe, bounds.Left, bounds.Top, 6, bounds.Height);
            }
            var textArea = new Rectangle(bounds.Left + 18, bounds.Top + 8, bounds.Width - 30, bounds.Height - 16);
            System.Windows.Forms.TextRenderer.DrawText(g, context.Message, context.Font, textArea, Color.White,
                System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.WordBreak);
        }
    }
}
