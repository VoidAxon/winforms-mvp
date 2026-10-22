using WinformsMVP.Common;
using WinformsMVP.Logging;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;
using WinformsMVP.Testing;
using Xunit;

namespace WinformsMVP.Samples.Tests.Testing
{
    public class TestServicesFakesTests
    {
        private static readonly ViewAction Export = ViewAction.Create("FakesTests.Export");

        private sealed class ExportPresenter : WindowPresenterBase<IFixtureView>
        {
            public int Picked;

            protected override void OnViewAttached() { }

            protected override void RegisterViewActions() => Dispatcher.Register(Export, OnExport);

            private void OnExport()
            {
                if (!Messages.ConfirmYesNo("Export?")) return;
                var target = Dialogs.ShowSaveFileDialog();
                if (!target.IsOk) return;
                Files.WriteAllText(target.Value, "id\r\n1");
                Picked = Navigator.ShowWindowAsModal<FixturePresenter, int>(new FixturePresenter()).Value;
                Logger.LogInformation("Exported to {Path}", target.Value);
                Toasts.Show("Exported", ToastType.Success);
            }
        }

        [Fact]
        public void CreateFakes_RegistersEveryFake()
        {
            var fakes = TestServices.CreateFakes();
            var sp = fakes.Provider;

            Assert.Same(fakes.Messages, sp.GetService(typeof(IMessageService)));
            Assert.Same(fakes.Toasts, sp.GetService(typeof(IToastService)));
            Assert.Same(fakes.Navigator, sp.GetService(typeof(IWindowNavigator)));
            Assert.Same(fakes.Dialogs, sp.GetService(typeof(IDialogProvider)));
            Assert.Same(fakes.Files, sp.GetService(typeof(IFileService)));
            Assert.Same(fakes.Logs, sp.GetService(typeof(ILoggerFactory)));
        }

        [Fact]
        public void APresenterUsesTheFakes_EndToEnd()
        {
            var fakes = TestServices.CreateFakes();
            fakes.Messages.EnqueueYesNo(true);
            fakes.Dialogs.EnqueueSaveFile(InteractionResult<string>.Ok(@"C:\test\out.csv"));
            fakes.Navigator.RespondTo<FixturePresenter, int>(_ => InteractionResult<int>.Ok(3));
            var view = new FixtureView();
            var presenter = new ExportPresenter();

            using (PresenterTestHost.Start(fakes.Provider, presenter, view))
                view.Binder.Dispatch(Export);

            Assert.Equal("id\r\n1", fakes.Files.ReadAllText(@"C:\test\out.csv"));
            Assert.Equal(3, presenter.Picked);
            Assert.Equal("Exported", Assert.Single(fakes.Toasts.Calls).Text);
            Assert.Contains(fakes.Logs.Entries, e => e.Message == @"Exported to C:\test\out.csv");
        }

        [Fact]
        public void ReplacingAServiceBeforeStart_IsWhatThePresenterUses()
        {
            var fakes = TestServices.CreateFakes();
            var mine = new FakeToastService();
            fakes.Provider.RegisterInstance<IToastService>(mine);
            fakes.Messages.AnswerUnscripted(yes: true);
            fakes.Dialogs.SaveFileHandler = _ => InteractionResult<string>.Ok("out.csv");
            fakes.Navigator.RespondTo<FixturePresenter, int>(_ => InteractionResult<int>.Ok(1));
            var view = new FixtureView();

            using (PresenterTestHost.Start(fakes.Provider, new ExportPresenter(), view))
                view.Binder.Dispatch(Export);

            Assert.Single(mine.Calls);
            Assert.Empty(fakes.Toasts.Calls);
        }
    }
}
