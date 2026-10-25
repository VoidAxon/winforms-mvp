using System;
using System.Collections.Generic;
using Kibinago.Common;
using Kibinago.Services;

namespace Kibinago.Testing
{
    /// <summary>The kind of message-service call a <see cref="MessageCall"/> records.</summary>
    public enum MessageKind
    {
        Info,
        Warning,
        Error,
        YesNo,
        OkCancel,
        YesNoCancel
    }

    /// <summary>One recorded <see cref="IMessageService"/> call.</summary>
    public sealed class MessageCall
    {
        /// <summary>Creates a record.</summary>
        public MessageCall(MessageKind kind, string text, string caption)
        {
            Kind = kind;
            Text = text;
            Caption = caption;
        }

        /// <summary>Which method was called.</summary>
        public MessageKind Kind { get; }

        /// <summary>The message text.</summary>
        public string Text { get; }

        /// <summary>The caption as passed (empty when the caller used the default).</summary>
        public string Caption { get; }

        /// <summary>True for <see cref="MessageKind.YesNo"/>, <see cref="MessageKind.OkCancel"/> and <see cref="MessageKind.YesNoCancel"/>.</summary>
        public bool IsConfirmation => Kind == MessageKind.YesNo || Kind == MessageKind.OkCancel || Kind == MessageKind.YesNoCancel;

        /// <inheritdoc />
        public override string ToString() => $"{Kind}: {Text}";
    }

    /// <summary>
    /// <see cref="IMessageService"/> fake: records every call and answers confirmations from
    /// scripts. A confirmation's answer comes from, in order: the queue for its kind
    /// (<see cref="EnqueueYesNo"/> ...), then its handler (<see cref="YesNoHandler"/> ...), then the
    /// unscripted default — an <see cref="UnscriptedInteractionException"/> unless
    /// <see cref="AnswerUnscripted"/> was called.
    /// </summary>
    /// <remarks>
    /// <code>
    /// fakes.Messages.EnqueueYesNo(true);                        // the next ConfirmYesNo answers Yes
    /// fakes.Messages.YesNoHandler = m => m.Text.Contains("Delete");
    /// Assert.Contains(fakes.Messages.Calls, m => m.Kind == MessageKind.Info &amp;&amp; m.Text == "Saved");
    /// </code>
    /// Methods are virtual: override one to change a single behavior and keep the rest.
    /// </remarks>
    public class FakeMessageService : IMessageService
    {
        private readonly object _gate = new object();
        private readonly List<MessageCall> _calls = new List<MessageCall>();
        private readonly Queue<bool> _yesNo = new Queue<bool>();
        private readonly Queue<bool> _okCancel = new Queue<bool>();
        private readonly Queue<ConfirmResult> _yesNoCancel = new Queue<ConfirmResult>();
        private bool? _unscriptedYes;

        /// <summary>Every call so far, in order (a snapshot).</summary>
        public IReadOnlyList<MessageCall> Calls
        {
            get { lock (_gate) return _calls.ToArray(); }
        }

        /// <summary>Answers <see cref="ConfirmYesNo"/> when its queue is empty.</summary>
        public Func<MessageCall, bool> YesNoHandler { get; set; }

        /// <summary>Answers <see cref="ConfirmOkCancel"/> when its queue is empty.</summary>
        public Func<MessageCall, bool> OkCancelHandler { get; set; }

        /// <summary>Answers <see cref="ConfirmYesNoCancel"/> when its queue is empty.</summary>
        public Func<MessageCall, ConfirmResult> YesNoCancelHandler { get; set; }

        /// <summary>Queues answers for the next <see cref="ConfirmYesNo"/> calls (true = Yes).</summary>
        public void EnqueueYesNo(params bool[] answers) => Enqueue(_yesNo, answers);

        /// <summary>Queues answers for the next <see cref="ConfirmOkCancel"/> calls (true = OK).</summary>
        public void EnqueueOkCancel(params bool[] answers) => Enqueue(_okCancel, answers);

        /// <summary>Queues answers for the next <see cref="ConfirmYesNoCancel"/> calls.</summary>
        public void EnqueueYesNoCancel(params ConfirmResult[] answers) => Enqueue(_yesNoCancel, answers);

        /// <summary>
        /// Answers every unscripted confirmation instead of throwing: Yes/OK when
        /// <paramref name="yes"/> is true, otherwise No/Cancel (<see cref="ConfirmResult.No"/> for
        /// Yes/No/Cancel). For tests that do not care about the questions.
        /// </summary>
        public void AnswerUnscripted(bool yes)
        {
            lock (_gate) _unscriptedYes = yes;
        }

        /// <summary>Forgets the recorded calls; scripts stay.</summary>
        public void ClearCalls()
        {
            lock (_gate) _calls.Clear();
        }

        /// <summary>Forgets queued answers, handlers and the unscripted default; recorded calls stay.</summary>
        public void ResetScripts()
        {
            lock (_gate)
            {
                _yesNo.Clear();
                _okCancel.Clear();
                _yesNoCancel.Clear();
                _unscriptedYes = null;
            }
            YesNoHandler = null;
            OkCancelHandler = null;
            YesNoCancelHandler = null;
        }

        /// <inheritdoc />
        public virtual void ShowInfo(string text, string caption = "") => Record(MessageKind.Info, text, caption);

        /// <inheritdoc />
        public virtual void ShowWarning(string text, string caption = "") => Record(MessageKind.Warning, text, caption);

        /// <inheritdoc />
        public virtual void ShowError(string text, string caption = "") => Record(MessageKind.Error, text, caption);

        /// <inheritdoc />
        public virtual bool ConfirmYesNo(string text, string caption = "")
            => Answer(Record(MessageKind.YesNo, text, caption), _yesNo, YesNoHandler, yes => yes);

        /// <inheritdoc />
        public virtual bool ConfirmOkCancel(string text, string caption = "")
            => Answer(Record(MessageKind.OkCancel, text, caption), _okCancel, OkCancelHandler, yes => yes);

        /// <inheritdoc />
        public virtual ConfirmResult ConfirmYesNoCancel(string text, string caption = "")
            => Answer(Record(MessageKind.YesNoCancel, text, caption), _yesNoCancel, YesNoCancelHandler,
                      yes => yes ? ConfirmResult.Yes : ConfirmResult.No);

        private MessageCall Record(MessageKind kind, string text, string caption)
        {
            var call = new MessageCall(kind, text, caption);
            lock (_gate) _calls.Add(call);
            return call;
        }

        private T Answer<T>(MessageCall call, Queue<T> queue, Func<MessageCall, T> handler, Func<bool, T> unscripted)
        {
            bool? fallback;
            lock (_gate)
            {
                if (queue.Count > 0) return queue.Dequeue();
                fallback = _unscriptedYes;
            }
            if (handler != null) return handler(call);
            if (fallback.HasValue) return unscripted(fallback.Value);
            throw new UnscriptedInteractionException(
                $"Unscripted {call.Kind} confirmation: \"{call.Text}\". Script it with " +
                $"Enqueue{call.Kind}(...) or {call.Kind}Handler, or call AnswerUnscripted(yes: ...).");
        }

        private void Enqueue<T>(Queue<T> queue, T[] answers)
        {
            if (answers == null) throw new ArgumentNullException(nameof(answers));
            lock (_gate)
            {
                foreach (var answer in answers) queue.Enqueue(answer);
            }
        }
    }
}
