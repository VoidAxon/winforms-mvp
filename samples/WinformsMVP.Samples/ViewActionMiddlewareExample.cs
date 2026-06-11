using System;
using System.Diagnostics;
using WinformsMVP.Logging;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples
{
    // ============================================================================
    //  ViewAction Middleware Example
    // ----------------------------------------------------------------------------
    //  Demonstrates three real-world middleware that wrap every Dispatch on a
    //  presenter (or globally across an application):
    //
    //   1. AuditMiddleware       — record who attempted what, succeed or fail
    //   2. PerformanceMiddleware — warn when an action exceeds a latency budget
    //   3. ErrorDialogMiddleware — translate handler exceptions into UX
    //
    //  See CLAUDE.md "Dispatch Middleware Pipeline (Advanced)" for the conceptual
    //  introduction. This file is the runnable counterpart.
    //
    //  Simple users do NOT need any of this. The framework's fast path stays
    //  zero-overhead until something calls Dispatcher.Use(...).
    // ============================================================================

    /// <summary>
    /// Sink that an audit middleware writes audit entries to. A real implementation
    /// would persist to a database, an audit log file, or a SIEM system.
    /// </summary>
    public interface IAuditSink
    {
        void Record(AuditEntry entry);
    }

    /// <summary>One audit record per Dispatch.</summary>
    public sealed class AuditEntry
    {
        public string User { get; set; }
        public string Action { get; set; }
        public DateTime TimestampUtc { get; set; }
        public bool Succeeded { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Middleware that records every dispatch — successful, failed, or short-circuited —
    /// to an audit sink. Place this at the global (outermost) layer so no dispatch
    /// can avoid being audited.
    /// </summary>
    public sealed class AuditMiddleware : IDispatchMiddleware
    {
        private readonly IAuditSink _sink;
        private readonly Func<string> _userResolver;

        public AuditMiddleware(IAuditSink sink, Func<string> userResolver)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _userResolver = userResolver ?? throw new ArgumentNullException(nameof(userResolver));
        }

        public void Invoke(DispatchContext context, DispatchDelegate next)
        {
            var entry = new AuditEntry
            {
                User = _userResolver(),
                Action = context.Action.ToString(),
                TimestampUtc = DateTime.UtcNow
            };

            try
            {
                next(context);
            }
            finally
            {
                entry.Succeeded = context.HandlerExecuted && context.Exception == null;
                entry.Error = context.Exception?.Message;
                _sink.Record(entry);
            }
        }
    }

    /// <summary>
    /// Middleware that measures handler latency and logs a warning when any individual
    /// dispatch exceeds the configured threshold. Useful for catching accidentally
    /// slow handlers (synchronous DB calls, large CSV exports, etc.).
    /// </summary>
    public sealed class PerformanceMiddleware : IDispatchMiddleware
    {
        private readonly ILogger _logger;
        private readonly int _slowThresholdMs;

        public PerformanceMiddleware(ILogger logger, int slowThresholdMs = 100)
        {
            _logger = logger ?? NullLogger.Instance;
            _slowThresholdMs = slowThresholdMs;
        }

        public void Invoke(DispatchContext context, DispatchDelegate next)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                next(context);
            }
            finally
            {
                sw.Stop();
                if (sw.ElapsedMilliseconds > _slowThresholdMs)
                {
                    _logger.LogWarning(
                        "Slow dispatch: {Action} took {ElapsedMs}ms (threshold {ThresholdMs}ms)",
                        context.Action,
                        sw.ElapsedMilliseconds,
                        _slowThresholdMs);
                }
            }
        }
    }

    /// <summary>
    /// Middleware that replaces the framework's built-in safety-net catch with a
    /// richer user-facing strategy: validation errors → warning dialog, everything
    /// else → error dialog. The exception is recorded on the context so outer
    /// middleware (e.g. <see cref="AuditMiddleware"/>) still sees that a failure
    /// occurred.
    /// </summary>
    /// <remarks>
    /// Place this AFTER <see cref="AuditMiddleware"/> in the registration order
    /// (i.e. inside it) so the audit middleware's <c>finally</c> can still observe
    /// <see cref="DispatchContext.Exception"/> after this one catches.
    /// </remarks>
    public sealed class ErrorDialogMiddleware : IDispatchMiddleware
    {
        private readonly Services.IMessageService _messages;
        private readonly ILogger _logger;

        public ErrorDialogMiddleware(Services.IMessageService messages, ILogger logger)
        {
            _messages = messages ?? throw new ArgumentNullException(nameof(messages));
            _logger = logger ?? NullLogger.Instance;
        }

        public void Invoke(DispatchContext context, DispatchDelegate next)
        {
            try
            {
                next(context);
            }
            catch (ArgumentException ex)
            {
                // Treat ArgumentException as user input validation.
                _messages.ShowWarning(ex.Message, "入力エラー");
                context.Exception = ex;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatch {Action} failed", context.Action);
                _messages.ShowError(
                    $"操作に失敗しました: {ex.Message}",
                    "エラー");
                context.Exception = ex;
            }
        }
    }

    // ============================================================================
    //  Wiring example (composition root)
    // ----------------------------------------------------------------------------
    //  In Program.Main or a composition root, wire the middleware globally so it
    //  applies to every presenter:
    //
    //      var loggerFactory = new DebugLoggerFactory();
    //      ServiceLocator.Configure(reg =>
    //      {
    //          reg.RegisterInstance<ILoggerFactory>(loggerFactory);
    //          reg.RegisterInstance<IDispatcherConfigurer>(new ActionDispatcherConfigurer(d => d
    //              .Use(new AuditMiddleware(auditSink, () => CurrentUser.Name))
    //              .Use(new ErrorDialogMiddleware(
    //                       ServiceLocator.Current.Resolve<IMessageService>(),
    //                       loggerFactory.CreateLogger("dispatch")))
    //              .Use(new PerformanceMiddleware(
    //                       loggerFactory.CreateLogger("dispatch"),
    //                       slowThresholdMs: 200))));
    //      });
    //
    //  Or per-presenter inside RegisterViewActions:
    //
    //      protected override void RegisterViewActions()
    //      {
    //          _dispatcher.Use(new PerformanceMiddleware(Logger, slowThresholdMs: 50));
    //          _dispatcher.Register(CommonActions.Save, OnSave);
    //      }
    //
    //  Inline lambdas also work for one-off cases:
    //
    //      _dispatcher.Use((ctx, next) =>
    //      {
    //          if (_isReadOnly && IsDestructive(ctx.Action)) return;  // short-circuit
    //          next(ctx);
    //      });
    // ============================================================================
}
