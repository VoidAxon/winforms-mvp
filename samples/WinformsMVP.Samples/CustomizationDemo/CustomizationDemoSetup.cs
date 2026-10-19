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

            // Change detection for one model type. A third-party compare library would plug in the
            // same way through mvp.ChangeTracking.UseComparer(adapter), e.g.:
            //   sealed class CompareNetObjectsComparer : IObjectContentComparer
            //   { public bool ContentEquals(object a, object b) => new CompareLogic().Compare(a, b).AreEqual; }
            mvp.ChangeTracking.For(new ChangeTrackingPolicy<CustomizationDemoPresenter.Customer>(
                c => new CustomizationDemoPresenter.Customer { Name = c.Name, LastViewed = c.LastViewed },
                (a, b) => a.Name == b.Name));
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
