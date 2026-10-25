using System.Drawing;
using System.Windows.Forms;
using Kibinago.Services;

namespace Kibinago.Samples.CustomizationDemo
{
    /// <summary>
    /// A company-styled message box. Implementing <see cref="IMessageDialogBackend"/> — one method —
    /// is all it takes to restyle every message box in the application.
    /// </summary>
    public sealed class CompanyStyleDialogBackend : IMessageDialogBackend
    {
        public MessageDialogResult Show(MessageDialogRequest request)
        {
            using (var font = new Font("Segoe UI", 10f))
            using (var form = new Form
            {
                Text = request.Caption,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                StartPosition = request.Location.HasValue ? FormStartPosition.Manual : FormStartPosition.CenterParent,
                ClientSize = new Size(380, 150),
                BackColor = Color.White,
                Font = font,
            })
            {
                if (request.Location.HasValue) form.Location = request.Location.Value;

                form.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.FromArgb(0, 120, 110) });
                form.Controls.Add(new Label
                {
                    Text = request.Text,
                    Location = new Point(20, 24),
                    Size = new Size(340, 60),
                });

                var buttons = ButtonsFor(request.Buttons);
                int x = form.ClientSize.Width - 20;
                for (int i = buttons.Length - 1; i >= 0; i--)
                {
                    var (text, result) = buttons[i];
                    var button = new Button { Text = text, Size = new Size(90, 30), Tag = result, FlatStyle = FlatStyle.Flat };
                    x -= button.Width;
                    button.Location = new Point(x, 100);
                    x -= 10;
                    button.Click += (s, e) => { form.Tag = button.Tag; form.Close(); };
                    form.Controls.Add(button);
                }

                form.ShowDialog(request.Owner);
                return form.Tag is MessageDialogResult chosen ? chosen : MessageDialogResult.Cancel;
            }
        }

        private static (string, MessageDialogResult)[] ButtonsFor(MessageDialogButtons buttons)
        {
            switch (buttons)
            {
                case MessageDialogButtons.OkCancel: return new[] { ("OK", MessageDialogResult.Ok), ("Cancel", MessageDialogResult.Cancel) };
                case MessageDialogButtons.YesNo: return new[] { ("Yes", MessageDialogResult.Yes), ("No", MessageDialogResult.No) };
                case MessageDialogButtons.YesNoCancel: return new[] { ("Yes", MessageDialogResult.Yes), ("No", MessageDialogResult.No), ("Cancel", MessageDialogResult.Cancel) };
                case MessageDialogButtons.RetryCancel: return new[] { ("Retry", MessageDialogResult.Retry), ("Cancel", MessageDialogResult.Cancel) };
                case MessageDialogButtons.AbortRetryIgnore: return new[] { ("Abort", MessageDialogResult.Abort), ("Retry", MessageDialogResult.Retry), ("Ignore", MessageDialogResult.Ignore) };
                case MessageDialogButtons.CancelTryContinue: return new[] { ("Cancel", MessageDialogResult.Cancel), ("Try again", MessageDialogResult.TryAgain), ("Continue", MessageDialogResult.Continue) };
                default: return new[] { ("OK", MessageDialogResult.Ok) };
            }
        }
    }
}
