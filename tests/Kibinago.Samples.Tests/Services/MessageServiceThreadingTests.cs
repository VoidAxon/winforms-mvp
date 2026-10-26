using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kibinago.Services;
using Xunit;

namespace Kibinago.Samples.Tests.Services
{
    /// <summary>
    /// A message box asked for from a worker thread (code inside Task.Run) is shown on the UI thread,
    /// so it is modal to the application's windows; the worker waits for the answer.
    /// </summary>
    public class MessageServiceThreadingTests
    {
        private sealed class RecordingBackend : IMessageDialogBackend
        {
            public int ShownOnThread;
            public MessageDialogResult Answer = MessageDialogResult.Yes;

            public MessageDialogResult Show(MessageDialogRequest request)
            {
                ShownOnThread = Thread.CurrentThread.ManagedThreadId;
                return Answer;
            }
        }

        // Runs body on an STA thread with a shown (invisible) form, as a UI thread looks while the app runs.
        private static void OnUiThreadWithForm(Action<Form> body)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var form = new Form { ShowInTaskbar = false, Opacity = 0 })
                    {
                        form.Show();
                        body(form);
                    }
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "UI thread timed out");
            if (failure != null) throw new Exception("UI thread failed", failure);
        }

        // Keeps the UI thread processing messages (the worker's Invoke) until the task ends.
        private static T PumpUntilDone<T>(Task<T> task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            Assert.True(task.IsCompleted, "the worker did not get its answer");
            return task.Result;
        }

        [Fact]
        public void Confirm_FromWorkerThread_IsShownOnTheUiThreadAndAnswersTheWorker()
        {
            OnUiThreadWithForm(form =>
            {
                var backend = new RecordingBackend { Answer = MessageDialogResult.Yes };
                var messages = new MessageService(backend, new MessageDialogOptions()) { OpenFormLocator = () => form };

                bool answer = PumpUntilDone(Task.Run(() => messages.ConfirmYesNo("Delete?")));

                Assert.True(answer);
                Assert.Equal(Thread.CurrentThread.ManagedThreadId, backend.ShownOnThread);
            });
        }

        [Fact]
        public void Show_OnTheUiThread_IsShownDirectly()
        {
            OnUiThreadWithForm(form =>
            {
                var backend = new RecordingBackend();
                var messages = new MessageService(backend, new MessageDialogOptions()) { OpenFormLocator = () => form };

                messages.ShowInfo("Saved.");

                Assert.Equal(Thread.CurrentThread.ManagedThreadId, backend.ShownOnThread);
            });
        }

        [Fact]
        public void BackendFailure_FromWorkerThread_PropagatesToTheWorkerAndIsNotShownTwice()
        {
            OnUiThreadWithForm(form =>
            {
                int calls = 0;
                var backend = new ThrowingBackend(() => calls++);
                var messages = new MessageService(backend, new MessageDialogOptions()) { OpenFormLocator = () => form };

                var task = Task.Run(() => messages.ShowError("Boom"));
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (!task.IsCompleted && DateTime.UtcNow < deadline)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }

                var failure = Assert.Throws<AggregateException>(() => task.Wait());
                Assert.IsType<InvalidOperationException>(failure.InnerException);
                Assert.Equal(1, calls);
            });
        }

        private sealed class ThrowingBackend : IMessageDialogBackend
        {
            private readonly Action _onShow;
            public ThrowingBackend(Action onShow) { _onShow = onShow; }

            public MessageDialogResult Show(MessageDialogRequest request)
            {
                _onShow();
                throw new InvalidOperationException("backend failed");
            }
        }

        [Fact]
        public void Show_OnASecondUiThread_StaysOnThatThread()
        {
            // An app with two UI threads: a call made on the second one (inside its message loop) must not
            // be sent to the first thread's form, or the dialog would not be modal to the caller's window.
            OnUiThreadWithForm(firstUiForm =>
            {
                var backend = new RecordingBackend();
                var messages = new MessageService(backend, new MessageDialogOptions()) { OpenFormLocator = () => firstUiForm };
                int second = 0;

                var secondUi = new Thread(() =>
                {
                    second = Thread.CurrentThread.ManagedThreadId;
                    using (var form = new Form { ShowInTaskbar = false, Opacity = 0 })
                    {
                        form.Shown += (s, e) => { messages.ShowInfo("From the second window."); form.Close(); };
                        Application.Run(form);
                    }
                });
                secondUi.SetApartmentState(ApartmentState.STA);
                secondUi.Start();
                // Keep the first UI thread responsive: if the call were sent here, it would still finish
                // (and the assertion below would fail) instead of hanging the test.
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (secondUi.IsAlive && DateTime.UtcNow < deadline)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }

                Assert.False(secondUi.IsAlive, "the second UI thread did not finish");
                Assert.Equal(second, backend.ShownOnThread);
            });
        }

        [Fact]
        public void Show_FromWorkerThread_WithoutAnyForm_IsShownOnTheCallingThread()
        {
            // Before the first window opens (or after the last one closed) there is no UI thread to go to.
            var backend = new RecordingBackend();
            var messages = new MessageService(backend, new MessageDialogOptions()) { OpenFormLocator = () => null };
            int worker = 0;

            Task.Run(() =>
            {
                worker = Thread.CurrentThread.ManagedThreadId;
                messages.ShowWarning("No window yet.");
            }).Wait();

            Assert.Equal(worker, backend.ShownOnThread);
        }
    }
}
