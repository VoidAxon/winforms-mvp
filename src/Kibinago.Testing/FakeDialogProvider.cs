using System;
using System.Collections.Generic;
using System.Drawing;
using Kibinago.Common;
using Kibinago.Services;

namespace Kibinago.Testing
{
    /// <summary>The dialog a <see cref="DialogCall"/> records.</summary>
    public enum DialogKind
    {
        /// <summary><see cref="IDialogProvider.ShowOpenFileDialog"/>.</summary>
        OpenFile,
        /// <summary><see cref="IDialogProvider.ShowOpenMultipleFilesDialog"/>.</summary>
        OpenMultipleFiles,
        /// <summary><see cref="IDialogProvider.ShowSaveFileDialog"/>.</summary>
        SaveFile,
        /// <summary><see cref="IDialogProvider.ShowFolderBrowserDialog"/>.</summary>
        FolderBrowser,
        /// <summary><see cref="IDialogProvider.ShowColorDialog"/>.</summary>
        Color
    }

    /// <summary>One recorded <see cref="IDialogProvider"/> call.</summary>
    public sealed class DialogCall
    {
        /// <summary>Creates a record.</summary>
        public DialogCall(DialogKind kind, object options)
        {
            Kind = kind;
            Options = options;
        }

        /// <summary>Which dialog was requested.</summary>
        public DialogKind Kind { get; }

        /// <summary>The options object as passed (null when the caller passed none).</summary>
        public object Options { get; }

        /// <inheritdoc />
        public override string ToString() => Kind.ToString();
    }

    /// <summary>
    /// <see cref="IDialogProvider"/> fake: records every request and answers from scripts. An answer
    /// comes from, in order: the queue for that dialog (<see cref="EnqueueSaveFile"/> ...), then its
    /// handler (<see cref="SaveFileHandler"/> ...), then the unscripted default — an
    /// <see cref="UnscriptedInteractionException"/> unless <see cref="CancelUnscripted"/> was called.
    /// No native behavior is simulated (filters, extension insertion, file checks).
    /// </summary>
    /// <remarks>
    /// <code>
    /// fakes.Dialogs.EnqueueSaveFile(InteractionResult&lt;string&gt;.Ok(@"C:\test\out.csv"));
    /// fakes.Dialogs.OpenFileHandler = o => InteractionResult&lt;string&gt;.Ok(@"C:\test\in.csv");
    /// </code>
    /// </remarks>
    public class FakeDialogProvider : IDialogProvider
    {
        private readonly object _gate = new object();
        private readonly List<DialogCall> _calls = new List<DialogCall>();
        private readonly Queue<InteractionResult<string>> _openFile = new Queue<InteractionResult<string>>();
        private readonly Queue<InteractionResult<string[]>> _openMultiple = new Queue<InteractionResult<string[]>>();
        private readonly Queue<InteractionResult<string>> _saveFile = new Queue<InteractionResult<string>>();
        private readonly Queue<InteractionResult<string>> _folder = new Queue<InteractionResult<string>>();
        private readonly Queue<InteractionResult<Color>> _color = new Queue<InteractionResult<Color>>();
        private bool _cancelUnscripted;

        /// <summary>Every request so far, in order (a snapshot).</summary>
        public IReadOnlyList<DialogCall> Calls
        {
            get { lock (_gate) return _calls.ToArray(); }
        }

        /// <summary>Answers <see cref="ShowOpenFileDialog"/> when its queue is empty.</summary>
        public Func<OpenFileDialogOptions, InteractionResult<string>> OpenFileHandler { get; set; }

        /// <summary>Answers <see cref="ShowOpenMultipleFilesDialog"/> when its queue is empty.</summary>
        public Func<OpenFileDialogOptions, InteractionResult<string[]>> OpenMultipleFilesHandler { get; set; }

        /// <summary>Answers <see cref="ShowSaveFileDialog"/> when its queue is empty.</summary>
        public Func<SaveFileDialogOptions, InteractionResult<string>> SaveFileHandler { get; set; }

        /// <summary>Answers <see cref="ShowFolderBrowserDialog"/> when its queue is empty.</summary>
        public Func<FolderBrowserDialogOptions, InteractionResult<string>> FolderBrowserHandler { get; set; }

        /// <summary>Answers <see cref="ShowColorDialog"/> when its queue is empty.</summary>
        public Func<ColorDialogOptions, InteractionResult<Color>> ColorHandler { get; set; }

        /// <summary>Queues the answer for the next open-file dialog.</summary>
        public void EnqueueOpenFile(InteractionResult<string> result) => Enqueue(_openFile, result);

        /// <summary>Queues the answer for the next multiple-files dialog.</summary>
        public void EnqueueOpenMultipleFiles(InteractionResult<string[]> result) => Enqueue(_openMultiple, result);

        /// <summary>Queues the answer for the next save-file dialog.</summary>
        public void EnqueueSaveFile(InteractionResult<string> result) => Enqueue(_saveFile, result);

        /// <summary>Queues the answer for the next folder-browser dialog.</summary>
        public void EnqueueFolderBrowser(InteractionResult<string> result) => Enqueue(_folder, result);

        /// <summary>Queues the answer for the next color dialog.</summary>
        public void EnqueueColor(InteractionResult<Color> result) => Enqueue(_color, result);

        /// <summary>Answers every unscripted dialog with Cancel instead of throwing.</summary>
        public void CancelUnscripted()
        {
            lock (_gate) _cancelUnscripted = true;
        }

        /// <summary>Forgets the recorded requests; scripts stay.</summary>
        public void ClearCalls()
        {
            lock (_gate) _calls.Clear();
        }

        /// <inheritdoc />
        public virtual InteractionResult<string> ShowOpenFileDialog(OpenFileDialogOptions options = null)
            => Answer(DialogKind.OpenFile, options, _openFile, OpenFileHandler, InteractionResult<string>.Cancel);

        /// <inheritdoc />
        public virtual InteractionResult<string[]> ShowOpenMultipleFilesDialog(OpenFileDialogOptions options = null)
            => Answer(DialogKind.OpenMultipleFiles, options, _openMultiple, OpenMultipleFilesHandler, InteractionResult<string[]>.Cancel);

        /// <inheritdoc />
        public virtual InteractionResult<string> ShowSaveFileDialog(SaveFileDialogOptions options = null)
            => Answer(DialogKind.SaveFile, options, _saveFile, SaveFileHandler, InteractionResult<string>.Cancel);

        /// <inheritdoc />
        public virtual InteractionResult<string> ShowFolderBrowserDialog(FolderBrowserDialogOptions options = null)
            => Answer(DialogKind.FolderBrowser, options, _folder, FolderBrowserHandler, InteractionResult<string>.Cancel);

        /// <inheritdoc />
        public virtual InteractionResult<Color> ShowColorDialog(ColorDialogOptions options = null)
            => Answer(DialogKind.Color, options, _color, ColorHandler, InteractionResult<Color>.Cancel);

        private TResult Answer<TOptions, TResult>(DialogKind kind, TOptions options, Queue<TResult> queue,
            Func<TOptions, TResult> handler, Func<TResult> cancel)
        {
            bool cancelUnscripted;
            lock (_gate)
            {
                _calls.Add(new DialogCall(kind, options));
                if (queue.Count > 0) return queue.Dequeue();
                cancelUnscripted = _cancelUnscripted;
            }
            if (handler != null) return handler(options);
            if (cancelUnscripted) return cancel();
            throw new UnscriptedInteractionException(
                $"Unscripted {kind} dialog. Script it with Enqueue{kind}(...) or {kind}Handler, or call CancelUnscripted().");
        }

        private void Enqueue<T>(Queue<T> queue, T result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            lock (_gate) queue.Enqueue(result);
        }
    }
}
