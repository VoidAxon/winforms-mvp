using System;
using WinformsMVP.Logging;
using WinformsMVP.Testing;
using Xunit;

namespace WinformsMVP.Samples.Tests.Testing
{
    public class RecordingLoggerFactoryTests
    {
        [Fact]
        public void LogError_RecordsLevelCategoryFormattedMessageAndException()
        {
            var logs = new RecordingLoggerFactory();
            var logger = logs.CreateLogger(typeof(RecordingLoggerFactoryTests));
            var failure = new InvalidOperationException("boom");
            var args = new object[] { 7 };

            Assert.True(logger.IsEnabled(LogLevel.Trace));
            logger.LogError(failure, "Order {OrderId} failed", args);
            args[0] = 8;   // the entry keeps a copy

            var entry = Assert.Single(logs.Entries);
            Assert.Equal(typeof(RecordingLoggerFactoryTests).FullName, entry.Category);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Same(failure, entry.Exception);
            Assert.Equal("Order 7 failed", entry.Message);
            Assert.Equal("Order {OrderId} failed", entry.Template);
            Assert.Equal(7, Assert.Single(entry.Args));

            logs.Clear();
            Assert.Empty(logs.Entries);
        }

        [Fact]
        public void LoggersShareOneHistory_InOrder()
        {
            var logs = new RecordingLoggerFactory();
            logs.CreateLogger("A").LogInformation("one");
            logs.CreateLogger("B").LogWarning("two");

            Assert.Equal(new[] { "A", "B" }, new[] { logs.Entries[0].Category, logs.Entries[1].Category });
        }
    }
}
