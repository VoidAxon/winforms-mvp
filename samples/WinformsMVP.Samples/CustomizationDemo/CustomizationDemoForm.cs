using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.CustomizationDemo
{
    public class CustomizationDemoForm : Form, ICustomizationDemoView
    {
        private readonly TextBox _name = new TextBox { Location = new Point(20, 72), Width = 300 };
        private readonly Label _status = new Label { Location = new Point(20, 220), Size = new Size(460, 40), ForeColor = Color.DimGray };
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public CustomizationDemoForm()
        {
            Text = "Framework Customization Demo";
            ClientSize = new Size(500, 280);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);

            Controls.Add(new Label
            {
                Text = "All customization is in CustomizationDemoSetup.Configure (one place). " +
                       "The presenter below is ordinary code.",
                Location = new Point(20, 16),
                Size = new Size(460, 40),
            });
            Controls.Add(_name);

            var info = AddButton("Info dialog", 20, 120);
            var confirm = AddButton("Confirm dialog", 170, 120);
            var toast = AddButton("Show toast", 320, 120);
            var check = AddButton("Check for changes", 20, 165, 200);
            Controls.Add(_status);

            _binder.Add(CustomizationDemoActions.Info, info);
            _binder.Add(CustomizationDemoActions.Confirm, confirm);
            _binder.Add(CustomizationDemoActions.Toast, toast);
            _binder.Add(CustomizationDemoActions.CheckChanges, check);
        }

        private Button AddButton(string text, int x, int y, int width = 140)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 32) };
            Controls.Add(button);
            return button;
        }

        public IViewActionBinder ActionBinder => _binder;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CustomerName
        {
            get => _name.Text;
            set => _name.Text = value;
        }

        public void ShowStatus(string text) => _status.Text = text;
    }
}
