using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Logging;
using WinformsMVP.Samples.Tests.TestHelpers;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class WinFormsToastServiceTests
    {
        // Runs body on a fresh STA thread with a WinForms synchronization context, the way a UI
        // thread looks once the first control exists. Posted work is pumped with DoEvents.
        private static void OnUiThread(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                    body();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "UI thread timed out");
            if (failure != null) throw new Exception("UI thread failed", failure);
        }

        private static void PumpUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
        }

        [Fact]
        public void Show_WithoutDuration_UsesHostOptionsDuration()
        {
            OnUiThread(() =>
            {
                using (var svc = new WinFormsToastService(new ToastHostOptions { Duration = 4500 }))
                {
                    svc.Show("hello", ToastType.Info);

                    Assert.Equal(4500, svc.Manager.LiveToasts.Single().Duration);
                }
            });
        }

        [Fact]
        public void Show_PerCallDuration_Wins()
        {
            OnUiThread(() =>
            {
                using (var svc = new WinFormsToastService(new ToastHostOptions { Duration = 4500 }))
                {
                    svc.Show("hello", ToastType.Info, 1200);

                    Assert.Equal(1200, svc.Manager.LiveToasts.Single().Duration);
                }
            });
        }

        [Fact]
        public void Show_FromWorkerThread_CreatesToastOnUiThread()
        {
            OnUiThread(() =>
            {
                using (var svc = new WinFormsToastService(new ToastHostOptions()))
                {
                    int uiThread = Thread.CurrentThread.ManagedThreadId;
                    svc.Show("capture", ToastType.Info);   // first UI-thread call binds the dispatcher

                    Task.Run(() => svc.Show("from worker", ToastType.Info)).Wait();
                    PumpUntil(() => svc.Manager.LiveToasts.Count == 2);

                    Assert.Equal(2, svc.Manager.LiveToasts.Count);
                    Assert.All(svc.Manager.LiveToasts, t => Assert.Equal(uiThread, t.CreatedThreadId));
                }
            });
        }

        [Fact]
        public async Task Show_FromWorkerBeforeUiCapture_IsIgnoredAndLogged()
        {
            var logger = new CapturingLoggerFactory();
            using (var svc = new WinFormsToastService(new ToastHostOptions(), loggerFactory: logger))
            {
                await Task.Run(() => svc.Show("too early", ToastType.Info));

                Assert.Empty(svc.Manager.LiveToasts);
                Assert.Contains(logger.Logger.Entries, e => e.Level == LogLevel.Warning);
            }
        }

        [Fact]
        public void Show_CopiesFontBeforeReturning()
        {
            OnUiThread(() =>
            {
                using (var svc = new WinFormsToastService(new ToastHostOptions()))
                {
                    svc.Show("capture", ToastType.Info);

                    Task.Run(() =>
                    {
                        using (var font = new Font("Arial", 12))
                            svc.Show("font", ToastType.Info, new ToastOptions { Font = font });
                    }).Wait();   // the caller's font is disposed before the UI thread builds the toast
                    PumpUntil(() => svc.Manager.LiveToasts.Count == 2);

                    var toast = svc.Manager.LiveToasts.Last();
                    Assert.Equal(12f, toast.Font.Size);   // a disposed Font throws on Size
                }
            });
        }

        [Fact]
        public void MaxVisibleToasts_EvictsOldest()
        {
            OnUiThread(() =>
            {
                using (var svc = new WinFormsToastService(new ToastHostOptions { MaxVisibleToasts = 2 }))
                {
                    svc.Show("1", ToastType.Info);
                    svc.Show("2", ToastType.Info);
                    svc.Show("3", ToastType.Info);

                    Assert.Equal(new[] { "2", "3" }, svc.Manager.LiveToasts.Select(t => t.Message));
                }
            });
        }

        [Fact]
        public void ShowAnchored_ReplacesPreviousAnchored_AndDoesNotStack()
        {
            OnUiThread(() =>
            {
                using (var svc = new WinFormsToastService(new ToastHostOptions()))
                {
                    svc.ShowAnchored("a", ToastType.Info, new Point(100, 100));
                    var first = svc.Manager.Anchored;
                    svc.ShowAnchored("b", ToastType.Info, new Point(120, 120));

                    Assert.True(first.IsClosed);
                    Assert.Equal("b", svc.Manager.Anchored.Message);
                    Assert.Empty(svc.Manager.LiveToasts);
                }
            });
        }

        [Fact]
        public void TwoServices_HaveIndependentStacks()
        {
            OnUiThread(() =>
            {
                using (var a = new WinFormsToastService(new ToastHostOptions()))
                using (var b = new WinFormsToastService(new ToastHostOptions()))
                {
                    a.Show("a", ToastType.Info);

                    Assert.Single(a.Manager.LiveToasts);
                    Assert.Empty(b.Manager.LiveToasts);
                }
            });
        }

        [Fact]
        public void Dispose_ClosesLiveToasts_AndLaterShowIsIgnored()
        {
            OnUiThread(() =>
            {
                var svc = new WinFormsToastService(new ToastHostOptions());
                svc.Show("a", ToastType.Info);
                var toast = svc.Manager.LiveToasts.Single();

                svc.Dispose();
                svc.Show("b", ToastType.Info);

                Assert.True(toast.IsClosed);
                Assert.Empty(svc.Manager.LiveToasts);
            });
        }

        public static TheoryData<ToastHostOptions> InvalidOptions => new TheoryData<ToastHostOptions>
        {
            new ToastHostOptions { Size = new Size(0, 80) },
            new ToastHostOptions { Size = new Size(350, 0) },
            new ToastHostOptions { Duration = 0 },
            new ToastHostOptions { MinHeight = 300, MaxHeight = 200 },
            new ToastHostOptions { MaxVisibleToasts = 0 },
            new ToastHostOptions { Opacity = 0 },
            new ToastHostOptions { Opacity = 1.5 },
            new ToastHostOptions { Margin = -1 },
            new ToastHostOptions { Gap = -1 },
        };

        [Theory]
        [MemberData(nameof(InvalidOptions))]
        public void Ctor_InvalidOptions_Throws(ToastHostOptions options)
        {
            Assert.Throws<ArgumentException>(() => new WinFormsToastService(options));
        }

        [Fact]
        public void Ctor_SnapshotsOptions()
        {
            OnUiThread(() =>
            {
                var options = new ToastHostOptions { Duration = 4500 };
                using (var svc = new WinFormsToastService(options))
                {
                    options.Duration = 100;   // later edits do not reach the service
                    svc.Show("x", ToastType.Info);

                    Assert.Equal(4500, svc.Manager.LiveToasts.Single().Duration);
                }
            });
        }
    }
}
