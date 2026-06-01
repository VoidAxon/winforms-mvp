using System;
using System.Drawing;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.MessageBoxDemo
{
    public static class MessageBoxDemoActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("MessageBoxDemo");

        public static readonly ViewAction ShowAtTopLeft = Factory.Create("ShowAtTopLeft");
        public static readonly ViewAction ShowAtTopRight = Factory.Create("ShowAtTopRight");
        public static readonly ViewAction ShowAtBottomLeft = Factory.Create("ShowAtBottomLeft");
        public static readonly ViewAction ShowAtBottomRight = Factory.Create("ShowAtBottomRight");
        public static readonly ViewAction ShowAtMouse = Factory.Create("ShowAtMouse");
        public static readonly ViewAction ShowCentered = Factory.Create("ShowCentered");
        public static readonly ViewAction ShowConfirmAtMouse = Factory.Create("ShowConfirmAtMouse");
    }

    public class MessageBoxDemoPresenter : WindowPresenterBase<IMessageBoxDemoView>
    {
        protected override void OnViewAttached()
        {
            // Nothing to do
        }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(MessageBoxDemoActions.ShowAtTopLeft, OnShowAtTopLeft);
            _dispatcher.Register(MessageBoxDemoActions.ShowAtTopRight, OnShowAtTopRight);
            _dispatcher.Register(MessageBoxDemoActions.ShowAtBottomLeft, OnShowAtBottomLeft);
            _dispatcher.Register(MessageBoxDemoActions.ShowAtBottomRight, OnShowAtBottomRight);
            _dispatcher.Register(MessageBoxDemoActions.ShowAtMouse, OnShowAtMouse);
            _dispatcher.Register(MessageBoxDemoActions.ShowCentered, OnShowCentered);
            _dispatcher.Register(MessageBoxDemoActions.ShowConfirmAtMouse, OnShowConfirmAtMouse);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Nothing to initialize
        }

        private void OnShowAtTopLeft()
        {
            var location = new Point(100, 100);
            Messages.ShowInfoAt(
                "This is a positioned MessageBox at Top-Left!\n\n" +
                "Notice:\n" +
                "✓ Native Windows MessageBox appearance\n" +
                "✓ System icons\n" +
                "✓ System theme (Dark Mode support)\n" +
                "✓ Keyboard shortcuts work (Enter, Esc)",
                location,
                "Top-Left Position");
        }

        private void OnShowAtTopRight()
        {
            var location = new Point(1000, 100);
            Messages.ShowWarningAt(
                "This is a WARNING at Top-Right!\n\n" +
                "Using native MessageBox with positioning via CBT Hook.",
                location,
                "Top-Right Position");
        }

        private void OnShowAtBottomLeft()
        {
            var location = new Point(100, 600);
            Messages.ShowErrorAt(
                "This is an ERROR at Bottom-Left!\n\n" +
                "System error icon and theme are automatically applied.",
                location,
                "Bottom-Left Position");
        }

        private void OnShowAtBottomRight()
        {
            var location = new Point(1000, 600);
            Messages.ShowInfoAt(
                "This is at Bottom-Right!\n\n" +
                "Perfect positioning using Windows API.",
                location,
                "Bottom-Right Position");
        }

        private void OnShowAtMouse()
        {
            var location = new Point(500, 400);
            Messages.ShowInfoAt(
                "This MessageBox appears at a specific position!\n\n" +
                "Demonstrates custom positioning capability.",
                location,
                "Custom Position");
        }

        private void OnShowCentered()
        {
            // Standard centered MessageBox (for comparison)
            Messages.ShowInfo(
                "This is a STANDARD MessageBox (centered).\n\n" +
                "Compare this with the positioned ones!\n\n" +
                "Notice it always appears in the center of the screen.",
                "Standard Centered");
        }

        private void OnShowConfirmAtMouse()
        {
            var location = new Point(400, 300);
            bool confirmed = Messages.ConfirmYesNoAt(
                "Do you want to proceed?\n\n" +
                "This is a Yes/No confirmation dialog at a specific position.\n\n" +
                "Try pressing Y (Yes) or N (No) on keyboard!",
                location,
                "Confirm Action");

            if (confirmed)
            {
                Messages.ShowInfoAt(
                    "You clicked YES! ✓",
                    new Point(450, 350),
                    "Confirmed");
            }
            else
            {
                Messages.ShowWarningAt(
                    "You clicked NO! ✗",
                    new Point(450, 350),
                    "Cancelled");
            }
        }
    }
}
