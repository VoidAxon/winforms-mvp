using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ScintillaNET;

namespace Kibinago.Samples.Shell
{
    /// <summary>
    /// Read-only C# viewer for the コード tab: Scintilla (Scintilla5.NET) with the C/C++ lexer, C# keywords
    /// and Visual Studio's light colors, line numbers. A view's layout code (InitializeComponent and the
    /// like) starts folded, so the MVP part reads first; the ⊞ in the margin opens it.
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

        // Methods that only build the UI: folded when a file is shown.
        private static readonly Regex LayoutMethod = new Regex(
            @"^\s*(private|protected|public|internal)\s+(override\s+)?void\s+(InitializeComponent|SetupUI|LayoutControls|InitializeChildControls|Create[A-Z]\w*)\s*\(",
            RegexOptions.Compiled);

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

            // Folding: a ⊞ / ⊟ margin, click to open or close.
            SetProperty("fold", "1");
            SetProperty("fold.compact", "0");
            Margins[2].Type = MarginType.Symbol;
            Margins[2].Mask = Marker.MaskFolders;
            Margins[2].Sensitive = true;
            Margins[2].Width = 16;
            SetFoldMarginColor(true, Color.White);
            Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
            Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;
            Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlusConnected;
            Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinusConnected;
            Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;
            for (var i = Marker.FolderEnd; i <= Marker.FolderOpen; i++)
            {
                Markers[i].SetForeColor(Color.White);
                Markers[i].SetBackColor(Color.Gray);
            }
            AutomaticFold = AutomaticFold.Show | AutomaticFold.Click | AutomaticFold.Change;
            FoldDisplayTextSetStyle(FoldDisplayText.Boxed);
            Styles[Style.FoldDisplayText].ForeColor = Color.Gray;

            ReadOnly = true;
        }

        /// <summary>Replaces the text (the control stays read-only for the user).</summary>
        public void ShowText(string text)
        {
            ReadOnly = false;
            Text = text ?? "";
            ReadOnly = true;
            EmptyUndoBuffer();
            FoldLayoutMethods();
            GotoPosition(0);
        }

        // Folds each layout method at its opening brace (the line after the signature in this code base).
        private void FoldLayoutMethods()
        {
            Colorize(0, -1);   // fold levels come from the lexer
            for (var i = 0; i < Lines.Count; i++)
            {
                if (!LayoutMethod.IsMatch(Lines[i].Text))
                    continue;
                for (var j = i; j < Lines.Count && j <= i + 2; j++)
                {
                    if ((Lines[j].FoldLevelFlags & FoldLevelFlags.Header) == 0)
                        continue;
                    Lines[j].FoldLine(FoldAction.Contract);
                    Lines[j].ToggleFoldShowText(" … UI の組み立て (左の ⊞ で開く) ");
                    break;
                }
            }
        }
    }
}
