using System.Drawing;
using System.Windows.Forms;
using ScintillaNET;

namespace Kibinago.Samples.Shell
{
    /// <summary>
    /// Read-only C# viewer for the コード tab: Scintilla (Scintilla5.NET) with the C/C++ lexer, C# keywords
    /// and Visual Studio's light colors, line numbers. (A view's layout is in its .Designer.cs, which the
    /// catalog does not list, so the view's file shows only its MVP part.)
    /// </summary>
    public sealed class SourceViewer : Scintilla
    {
        private const string Keywords =
            "abstract as base bool break byte case catch char checked class const continue decimal default delegate do " +
            "double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface " +
            "internal is lock long namespace new null object operator out override params private protected public readonly " +
            "ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong " +
            "unchecked unsafe ushort using virtual void volatile while " +
            "add and async await by descending dynamic equals from get global group init into join let nameof not on or " +
            "orderby partial record remove select set value var when where with yield";

        // Framework and BCL types the samples use most: colored like types in the IDE.
        private const string Types =
            "Action Func Task EventHandler EventArgs IDisposable Exception InvalidOperationException CancellationToken " +
            "IReadOnlyList List Dictionary IEnumerable Form UserControl Control Button Label TextBox ListBox " +
            "ViewAction ViewActionBinder IViewActionBinder IViewBase IActionableView IWindowView " +
            "PresenterBase WindowPresenterBase ControlPresenterBase InteractionResult InteractionStatus " +
            "IMessageService IToastService IWindowNavigator IEventAggregator ChangeTracker ValidationState MvpApplication ServiceLocator";

        public SourceViewer()
        {
            Technology = Technology.DirectWrite;   // font fallback for Japanese in strings and comments
            BorderStyle = ScintillaNET.BorderStyle.None;
            WrapMode = WrapMode.None;
            TabWidth = 4;
            IndentationGuides = IndentView.LookBoth;
            CaretLineBackColor = Color.FromArgb(242, 246, 252);

            StyleResetDefault();
            Styles[Style.Default].Font = "Consolas";
            Styles[Style.Default].SizeF = 10.5f;
            Styles[Style.Default].ForeColor = Color.Black;
            Styles[Style.Default].BackColor = Color.White;
            StyleClearAll();

            LexerName = "cpp";   // Lexilla's C-family lexer handles C# with C# keywords
            SetKeywords(0, Keywords);
            SetKeywords(1, Types);
            SetProperty("lexer.cpp.verbatim.strings.allow.escapes", "0");

            Styles[Style.Cpp.Comment].ForeColor = Color.FromArgb(0, 128, 0);
            Styles[Style.Cpp.CommentLine].ForeColor = Color.FromArgb(0, 128, 0);
            Styles[Style.Cpp.CommentDoc].ForeColor = Color.FromArgb(0, 128, 0);
            Styles[Style.Cpp.CommentLineDoc].ForeColor = Color.FromArgb(128, 128, 128);
            Styles[Style.Cpp.Number].ForeColor = Color.FromArgb(9, 134, 88);
            Styles[Style.Cpp.Word].ForeColor = Color.FromArgb(0, 0, 255);
            Styles[Style.Cpp.Word2].ForeColor = Color.FromArgb(43, 145, 175);
            Styles[Style.Cpp.String].ForeColor = Color.FromArgb(163, 21, 21);
            Styles[Style.Cpp.Character].ForeColor = Color.FromArgb(163, 21, 21);
            Styles[Style.Cpp.Verbatim].ForeColor = Color.FromArgb(163, 21, 21);
            Styles[Style.Cpp.StringEol].ForeColor = Color.FromArgb(163, 21, 21);
            Styles[Style.Cpp.Preprocessor].ForeColor = Color.FromArgb(128, 128, 128);
            Styles[Style.Cpp.Operator].ForeColor = Color.FromArgb(64, 64, 64);

            Styles[Style.LineNumber].ForeColor = Color.FromArgb(43, 145, 175);
            Styles[Style.LineNumber].BackColor = Color.White;
            Margins[0].Type = MarginType.Number;
            Margins[0].Width = 48;

            ReadOnly = true;
        }

        /// <summary>Replaces the text (the control stays read-only for the user).</summary>
        public void ShowText(string text)
        {
            ReadOnly = false;
            Text = text ?? "";
            ReadOnly = true;
            EmptyUndoBuffer();
            GotoPosition(0);
        }
    }
}
