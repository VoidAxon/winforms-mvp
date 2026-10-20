using System;
using System.Collections.Generic;
using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class MessageServiceTests
    {
        // Records requests and answers with a preset result; no window is shown.
        internal sealed class FakeBackend : IMessageDialogBackend
        {
            public List<MessageDialogRequest> Requests { get; } = new List<MessageDialogRequest>();
            public MessageDialogResult Answer { get; set; } = MessageDialogResult.Ok;

            public MessageDialogResult Show(MessageDialogRequest request)
            {
                Requests.Add(request);
                return Answer;
            }
        }

        private static MessageService Create(FakeBackend backend, string caption = "MyApp")
            => new MessageService(backend, new MessageDialogOptions { DefaultCaption = caption });

        [Fact]
        public void ShowInfo_EmptyCaption_UsesDefaultCaption()
        {
            var backend = new FakeBackend();

            Create(backend).ShowInfo("hello");

            var r = Assert.Single(backend.Requests);
            Assert.Equal("hello", r.Text);
            Assert.Equal("MyApp", r.Caption);
            Assert.Equal(MessageDialogButtons.Ok, r.Buttons);
            Assert.Equal(MessageDialogIcon.Information, r.Icon);
            Assert.Null(r.Location);
        }

        [Fact]
        public void ExplicitCaption_Wins()
        {
            var backend = new FakeBackend();

            Create(backend).ShowError("boom", "Import");

            Assert.Equal("Import", backend.Requests[0].Caption);
            Assert.Equal(MessageDialogIcon.Error, backend.Requests[0].Icon);
        }

        [Fact]
        public void ShowWarning_UsesWarningIcon()
        {
            var backend = new FakeBackend();

            Create(backend).ShowWarning("careful");

            Assert.Equal(MessageDialogIcon.Warning, backend.Requests[0].Icon);
        }

        [Theory]
        [InlineData(MessageDialogResult.Yes, ConfirmResult.Yes)]
        [InlineData(MessageDialogResult.No, ConfirmResult.No)]
        [InlineData(MessageDialogResult.Cancel, ConfirmResult.Cancel)]
        [InlineData(MessageDialogResult.None, ConfirmResult.Cancel)]
        public void ConfirmYesNoCancel_MapsResults(MessageDialogResult answer, ConfirmResult expected)
        {
            var backend = new FakeBackend { Answer = answer };

            Assert.Equal(expected, Create(backend).ConfirmYesNoCancel("save?"));
            Assert.Equal(MessageDialogButtons.YesNoCancel, backend.Requests[0].Buttons);
            Assert.Equal(MessageDialogIcon.Question, backend.Requests[0].Icon);
        }

        [Theory]
        [InlineData(MessageDialogResult.Ok, true)]
        [InlineData(MessageDialogResult.Cancel, false)]
        [InlineData(MessageDialogResult.None, false)]
        public void ConfirmOkCancel_ReturnsTrueOnlyForOk(MessageDialogResult answer, bool expected)
        {
            var backend = new FakeBackend { Answer = answer };

            Assert.Equal(expected, Create(backend).ConfirmOkCancel("go?"));
            Assert.Equal(MessageDialogButtons.OkCancel, backend.Requests[0].Buttons);
        }

        [Theory]
        [InlineData(MessageDialogResult.Yes, true)]
        [InlineData(MessageDialogResult.No, false)]
        public void ConfirmYesNo_ReturnsTrueOnlyForYes(MessageDialogResult answer, bool expected)
        {
            var backend = new FakeBackend { Answer = answer };

            Assert.Equal(expected, Create(backend).ConfirmYesNo("sure?"));
            Assert.Equal(MessageDialogButtons.YesNo, backend.Requests[0].Buttons);
        }

        [Fact]
        public void Ctor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new MessageService(null, new MessageDialogOptions()));
            Assert.Throws<ArgumentNullException>(() => new MessageService(new FakeBackend(), null));
        }

        [Fact]
        public void Ctor_SnapshotsOptions()
        {
            var backend = new FakeBackend();
            var options = new MessageDialogOptions { DefaultCaption = "Before" };
            var svc = new MessageService(backend, options);

            options.DefaultCaption = "After";
            svc.ShowInfo("x");

            Assert.Equal("Before", backend.Requests[0].Caption);
        }
    }

    public class NativeMessageDialogBackendMappingTests
    {
        [Fact]
        public void CancelTryContinue_RoundTripsThroughTheRequestEnums()
        {
#if NETFRAMEWORK
            // .NET Framework's MessageBox has no CancelTryContinue: the native backend refuses it
            // instead of silently showing a different button set.
            Assert.Throws<NotSupportedException>(() =>
                NativeMessageDialogBackend.ToWinForms(MessageDialogButtons.CancelTryContinue));
#else
            Assert.Equal(MessageDialogButtons.CancelTryContinue,
                NativeMessageDialogBackend.FromWinForms(System.Windows.Forms.MessageBoxButtons.CancelTryContinue));
            Assert.Equal(System.Windows.Forms.MessageBoxButtons.CancelTryContinue,
                NativeMessageDialogBackend.ToWinForms(MessageDialogButtons.CancelTryContinue));
            Assert.Equal(MessageDialogResult.TryAgain, NativeMessageDialogBackend.FromWinForms(System.Windows.Forms.DialogResult.TryAgain));
            Assert.Equal(MessageDialogResult.Continue, NativeMessageDialogBackend.FromWinForms(System.Windows.Forms.DialogResult.Continue));
            Assert.Equal(System.Windows.Forms.DialogResult.TryAgain, NativeMessageDialogBackend.ToWinForms(MessageDialogResult.TryAgain));
            Assert.Equal(System.Windows.Forms.DialogResult.Continue, NativeMessageDialogBackend.ToWinForms(MessageDialogResult.Continue));
#endif
        }
    }

    [Collection("ServiceLocator")]
    public class AnchoredMessageBoxRoutingTests : IDisposable
    {
        public AnchoredMessageBoxRoutingTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        [Fact]
        public void AnchoredMessageBox_RoutesThroughRegisteredBackend()
        {
            var backend = new MessageServiceTests.FakeBackend { Answer = MessageDialogResult.Yes };
            ServiceLocator.Configure(r =>
            {
                r.RegisterInstance<IMessageDialogBackend>(backend);
                r.RegisterInstance(new MessageDialogOptions { DefaultCaption = "MyApp" });
            });

            bool yes = AnchoredMessageBox.ConfirmYesNo("q", new Point(10, 20));

            Assert.True(yes);
            var r0 = Assert.Single(backend.Requests);
            Assert.Equal(new Point(10, 20), r0.Location);
            Assert.Equal("MyApp", r0.Caption);
            Assert.Equal(MessageDialogButtons.YesNo, r0.Buttons);
        }

        [Fact]
        public void AnchoredMessageBox_Show_MapsWinFormsEnumsBothWays()
        {
            var backend = new MessageServiceTests.FakeBackend { Answer = MessageDialogResult.Retry };
            ServiceLocator.Configure(r => r.RegisterInstance<IMessageDialogBackend>(backend));

            var result = AnchoredMessageBox.Show("t", "c", System.Windows.Forms.MessageBoxButtons.RetryCancel,
                System.Windows.Forms.MessageBoxIcon.Exclamation, new Point(1, 2));

            Assert.Equal(System.Windows.Forms.DialogResult.Retry, result);
            Assert.Equal(MessageDialogButtons.RetryCancel, backend.Requests[0].Buttons);
            Assert.Equal(MessageDialogIcon.Warning, backend.Requests[0].Icon);
        }
    }
}
