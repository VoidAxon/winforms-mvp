# チュートリアル: 最初のアプリを作る

このチュートリアルでは、**ToDo リストアプリ** を一から段階的に作りながら、フレームワークの主要機能を実際に体験します。
完了すると、以下の機能を持つ完結したアプリが手元に残ります。

- タスクの追加・編集・削除・完了切り替え
- 検証 (空のタスクを追加させない)
- 未保存変更の追跡と保存/破棄
- × ボタンで閉じる際のダーティ確認

> **前提条件**: [はじめに (Getting Started)](Getting-Started) で扱った基本概念 (View インターフェイス、Presenter、ViewAction) を理解していること。

> **ヒント**: プロジェクトは `dotnet new kibinago -n MyTodoApp` で作ると、`Program.Main` の登録処理まで揃った状態から始められます ([HowTo: テンプレートを使う](HowTo-Use-Templates))。

完成形のサンプルコードは [`samples/Kibinago.Samples/ToDoDemo/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/Kibinago.Samples/ToDoDemo) にあります。詰まったらそちらと見比べてください。

---

## 目次

- [全体像](#全体像)
- [Step 1: モデル (TodoItem) を作る](#step-1-モデル-todoitem-を作る)
- [Step 2: View インターフェイスを定義する](#step-2-view-インターフェイスを定義する)
- [Step 3: ActionKey を定義する](#step-3-actionkey-を定義する)
- [Step 4: Presenter の骨格を作る](#step-4-presenter-の骨格を作る)
- [Step 5: Form を実装する](#step-5-form-を実装する)
- [Step 6: タスクの追加・削除を実装する](#step-6-タスクの追加削除を実装する)
- [Step 7: ダーティ追跡を加える](#step-7-ダーティ追跡を加える)
- [Step 8: ウィンドウクローズ時の確認を加える](#step-8-ウィンドウクローズ時の確認を加える)
- [Step 9: テストを書く](#step-9-テストを書く)
- [次のステップ](#次のステップ)

---

## 全体像

最終的なディレクトリ構成:

```
MyTodoApp/
├── Program.cs                     起動
├── ToDoItem.cs                    モデル
├── ToDoActions.cs                 ActionKey
├── IToDoView.cs                   View インターフェイス
├── ToDoForm.cs                    Form 実装
└── ToDoPresenter.cs               Presenter
```

主要機能と関わるコンポーネントの対応:

| 機能 | 担当 |
|------|------|
| タスク追加 | ViewAction `AddTask` + Presenter |
| タスク削除 | ViewAction `DeleteTask` + Presenter (確認ダイアログあり) |
| 完了切り替え | ViewAction `ToggleComplete` + Presenter |
| 入力検証 | Presenter (`canExecute`) |
| ダーティ追跡 | `ChangeTracker<List<ToDoItem>>` + Presenter |
| ウィンドウクローズ確認 | `CanClose` override in Presenter |

---

## Step 1: モデル (TodoItem) を作る

業務データを保持するシンプルなクラスです。`Clone` や `Equals` は書きません。

```csharp
using System;

namespace MyTodoApp
{
    public class ToDoItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public bool IsCompleted { get; set; }
    }
}
```

ポイント:

- Step 7 の `ChangeTracker` は、スナップショットをリフレクションの深いコピーで撮り、内容 (フィールド) で比較するので、モデルに `Clone` / `Equals` は要らない。`ICloneable` / `Equals` があっても使わない (詳細: [ChangeTracker § 比較の解決順序](Reference-ChangeTracker#比較の解決順序))

---

## Step 2: View インターフェイスを定義する

UI 型は一切露出させません ([MVP 鉄則 1](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules))。

```csharp
using System;
using System.Collections.Generic;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace MyTodoApp
{
    public interface IToDoView : IWindowView
    {
        // 一覧表示
        IReadOnlyList<ToDoItem> Items { get; set; }
        ToDoItem SelectedItem { get; set; }

        // 入力フィールド
        string NewTaskTitle { get; set; }

        // 状態
        bool HasSelection { get; }
        bool HasInput { get; }
        bool HasUnsavedChanges { get; set; }

        // ActionBinder

        // 状態変化イベント (CanExecute 再評価用)
        event EventHandler SelectionChanged;
        event EventHandler InputChanged;
    }
}
```

---

## Step 3: ActionKey を定義する

```csharp
using Kibinago.ViewActions;

namespace MyTodoApp
{
    public static class ToDoActions
    {
        // 画面のスコープ名を 1 か所で付ける (キーは "ToDo.Add" など)
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("ToDo");

        public static readonly ViewAction Add            = Factory.Create(nameof(Add));
        public static readonly ViewAction Delete         = Factory.Create(nameof(Delete));
        public static readonly ViewAction ToggleComplete = Factory.Create(nameof(ToggleComplete));
        public static readonly ViewAction Save           = Factory.Create(nameof(Save));
    }
}
```

---

## Step 4: Presenter の骨格を作る

まず空の Presenter を作り、後で機能を足していきます。

```csharp
using System.Collections.Generic;
using Kibinago.Presenters;

namespace MyTodoApp
{
    public class ToDoPresenter : WindowPresenterBase<IToDoView>
    {
        private List<ToDoItem> _items;

        protected override void OnInitialize()
        {
            _items = LoadInitialItems();
            View.Items = _items;
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(ToDoActions.Add, OnAdd,
                canExecute: () => View.HasInput);

            Dispatcher.Register(ToDoActions.Delete, OnDelete,
                canExecute: () => View.HasSelection);

            Dispatcher.Register(ToDoActions.ToggleComplete, OnToggleComplete,
                canExecute: () => View.HasSelection);
        }

        protected override void OnViewAttached()
        {
            // 1 行で購読し、Presenter の破棄時に自動で解除される
            SubscribeViewEvent(h => View.SelectionChanged += h, h => View.SelectionChanged -= h, Dispatcher.RaiseCanExecuteChanged);
            SubscribeViewEvent(h => View.InputChanged += h, h => View.InputChanged -= h, Dispatcher.RaiseCanExecuteChanged);
        }

        private void OnAdd()            { /* TBD */ }
        private void OnDelete()         { /* TBD */ }
        private void OnToggleComplete() { /* TBD */ }

        private List<ToDoItem> LoadInitialItems()
            => new List<ToDoItem>
            {
                new ToDoItem { Title = "Read the framework docs", IsCompleted = true },
                new ToDoItem { Title = "Try the Tutorial",         IsCompleted = false },
            };
    }
}
```

ここまでで「View アタッチ → 初期データ表示 → ボタン Enabled の自動制御」が動く状態になります。

---

## Step 5: Form を実装する

UI の詳細はすべて Form の中に閉じ込めます。Presenter は `Button` も `ListBox` も知らないままです。

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace MyTodoApp
{
    public class ToDoForm : Form, IToDoView
    {
        private readonly ListBox _list;
        private readonly TextBox _newTaskInput;
        private readonly Button _btnAdd;
        private readonly Button _btnDelete;
        private readonly Button _btnToggle;
        private readonly ViewActionBinder _binder;

        private List<ToDoItem> _items = new List<ToDoItem>();

        public ToDoForm()
        {
            Text = "ToDo Demo";
            Size = new Size(560, 420);
            StartPosition = FormStartPosition.CenterScreen;

            _list = new ListBox
            {
                Location = new Point(12, 50),
                Size = new Size(520, 260),
            };
            _list.SelectedIndexChanged += (s, e) => SelectionChanged?.Invoke(this, EventArgs.Empty);

            _newTaskInput = new TextBox
            {
                Location = new Point(12, 12),
                Size = new Size(420, 24),
            };
            _newTaskInput.TextChanged += (s, e) => InputChanged?.Invoke(this, EventArgs.Empty);

            _btnAdd    = new Button { Text = "Add",            Location = new Point(442, 10),  Size = new Size(90, 28) };
            _btnDelete = new Button { Text = "Delete",         Location = new Point(12, 322),  Size = new Size(90, 28) };
            _btnToggle = new Button { Text = "Toggle Done",    Location = new Point(112, 322), Size = new Size(120, 28) };

            Controls.AddRange(new Control[] { _newTaskInput, _btnAdd, _list, _btnDelete, _btnToggle });

            _binder = new ViewActionBinder();
            _binder.Add(ToDoActions.Add,            _btnAdd);
            _binder.Add(ToDoActions.Delete,         _btnDelete);
            _binder.Add(ToDoActions.ToggleComplete, _btnToggle);
        }

        public IViewActionBinder ActionBinder => _binder;
        public event EventHandler SelectionChanged;
        public event EventHandler InputChanged;

        public IReadOnlyList<ToDoItem> Items
        {
            get => _items;
            set
            {
                _items = value?.ToList() ?? new List<ToDoItem>();
                _list.Items.Clear();
                foreach (var item in _items)
                    _list.Items.Add(FormatItem(item));
            }
        }

        public ToDoItem SelectedItem
        {
            get => _list.SelectedIndex >= 0 && _list.SelectedIndex < _items.Count
                ? _items[_list.SelectedIndex]
                : null;
            set
            {
                var idx = value == null ? -1 : _items.FindIndex(i => i.Id == value.Id);
                _list.SelectedIndex = idx;
            }
        }

        public string NewTaskTitle
        {
            get => _newTaskInput.Text;
            set => _newTaskInput.Text = value;
        }

        public bool HasSelection      => _list.SelectedIndex >= 0;
        public bool HasInput          => !string.IsNullOrWhiteSpace(_newTaskInput.Text);
        public bool HasUnsavedChanges { get; set; }

        private static string FormatItem(ToDoItem item)
            => $"[{(item.IsCompleted ? "x" : " ")}] {item.Title}";

        // IWindowView has no closing members — closing policy lives in the Presenter (CanClose).
    }
}
```

> **ポイント (Designer を使う場合):** この例はコードだけで UI を組み立てていますが、.NET 9 以降では、View インターフェイスを実装する public プロパティ (`Items` / `SelectedItem` / `NewTaskTitle` など) に `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]` を付けてください。付けないと WinForms アナライザー **WFO1000** が既定でビルドエラーにします (`Ctrl+.` →「プロジェクト内のすべて修正」で一括付与可)。`dotnet new mvp-window` が生成する Form は付与済みです ([HowTo: テンプレートを使う](HowTo-Use-Templates#designer-と-wfo1000))。

---

## Step 6: タスクの追加・削除を実装する

Presenter のハンドラを実装します。

```csharp
private void OnAdd()
{
    _items.Add(new ToDoItem { Title = View.NewTaskTitle.Trim() });
    View.Items = _items;             // 再表示
    View.NewTaskTitle = string.Empty;
}

private void OnDelete()
{
    if (!Messages.ConfirmYesNo("Delete this task?", "Confirm Delete"))
        return;

    var selected = View.SelectedItem;
    if (selected == null) return;

    _items.Remove(selected);
    View.Items = _items;
}

private void OnToggleComplete()
{
    var selected = View.SelectedItem;
    if (selected == null) return;

    selected.IsCompleted = !selected.IsCompleted;
    View.Items = _items;             // 再表示で見た目を更新
}
```

ここまで動かすと、追加・削除・完了切り替えができる ToDo アプリになっています。

---

## Step 7: ダーティ追跡を加える

`ChangeTracker<List<ToDoItem>>` で「未保存の変更があるか」を追跡します。

```csharp
using Kibinago.Common;

public class ToDoPresenter : WindowPresenterBase<IToDoView>
{
    private List<ToDoItem> _items;                    // データは Presenter が持つ
    private ChangeTracker<List<ToDoItem>> _tracker;  // トラッカーは変更の検出だけ

    protected override void OnInitialize()
    {
        _items = LoadInitialItems();
        _tracker = TrackChanges(_items);              // _items そのものを覚え、スナップショットを撮る
        View.Items = _items;
        UpdateDirtyState();
    }

    protected override void OnViewAttached()
    {
        SubscribeViewEvent(h => View.SelectionChanged += h, h => View.SelectionChanged -= h, Dispatcher.RaiseCanExecuteChanged);
        SubscribeViewEvent(h => View.InputChanged += h, h => View.InputChanged -= h, Dispatcher.RaiseCanExecuteChanged);
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(ToDoActions.Add, OnAdd,
            canExecute: () => View.HasInput);
        Dispatcher.Register(ToDoActions.Delete, OnDelete,
            canExecute: () => View.HasSelection);
        Dispatcher.Register(ToDoActions.ToggleComplete, OnToggleComplete,
            canExecute: () => View.HasSelection);
        Dispatcher.Register(ToDoActions.Save, OnSave,
            canExecute: () => _tracker != null && _tracker.IsChanged);  // ← 未保存があるときだけ Save 有効
    }

    private void OnAdd()
    {
        _items.Add(new ToDoItem { Title = View.NewTaskTitle.Trim() });  // _items をその場で編集
        View.Items = _items;
        View.NewTaskTitle = string.Empty;
        UpdateDirtyState();                            // tracker は通知しないので、ここで読む
    }

    // OnDelete / OnToggleComplete も同様に _items を編集してから UpdateDirtyState を呼ぶ

    private void OnSave()
    {
        SaveToStorage(_items);
        _tracker.AcceptChanges();                      // ← スナップショットを撮り直す → IsChanged == false
        UpdateDirtyState();
        Messages.ShowInfo("Saved successfully!", "Save");
    }

    private void UpdateDirtyState()
    {
        View.HasUnsavedChanges = _tracker.IsChanged;
        Dispatcher.RaiseCanExecuteChanged();
    }

    private void SaveToStorage(List<ToDoItem> items)
    {
        // 実際には JSON や DB に保存する。ここでは簡略化のため省略。
    }
}
```

> **重要**: tracker は `_items` そのもの (参照) を追跡しているので、`_items` をその場で編集すれば `IsChanged` にすぐ反映されます (追加・削除・項目の編集のどれも検出されます)。`_items = new List<ToDoItem>(...)` のように変数を差し替えた場合は、`_tracker = TrackChanges(_items)` で tracker を作り直してください。tracker は変更を通知しないため、編集のたびに `UpdateDirtyState()` で表示と `canExecute` を更新します。

Form 側で `Save` ボタンを追加 (Step 5 と同様の手順):

```csharp
private readonly Button _btnSave = new Button
{
    Text = "Save",
    Location = new Point(420, 322),
    Size = new Size(110, 28),
};
// ...
Controls.Add(_btnSave);
_binder.Add(ToDoActions.Save, _btnSave);
```

これで Save ボタンは未保存変更があるときだけ有効になります。

---

## Step 8: ウィンドウクローズ時の確認を加える

× ボタンのクローズ要求に対して、未保存があれば確認します。`CanClose` を override するだけです。Presenter の `RequestClose` (「閉じる」ボタンなど) も同じ `CanClose` を通ります。Forms にクローズコードを書く必要はありません。

```csharp
protected override bool CanClose(CloseReason reason)
{
    if (_tracker.IsChanged &&
        !Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm"))
    {
        return false;   // block the close
    }

    return true;
}
```

これでユーザーが × を押すと、未保存ならダイアログが出ます。「いいえ」を選ぶとクローズがブロックされます。
詳細は [Window Closing Model](Concept-Window-Closing-Model) を参照。

---

## Step 9: テストを書く

Presenter は WinForms に依存しないので、UI なしでテストできます。

```csharp
public class ToDoPresenterTests : IDisposable
{
    private readonly FakeServices _fakes = TestServices.CreateFakes();   // Kibinago.Testing パッケージ
    private readonly MockToDoView _view = new MockToDoView();
    private readonly PresenterTestHost _host;

    public ToDoPresenterTests()
    {
        // フェイクのフレームワークサービスを渡し、View のアタッチ + 実際の初期化を行う
        _host = PresenterTestHost.Start(_fakes.Provider, new ToDoPresenter(), _view);

        _fakes.Messages.ClearCalls();
    }

    public void Dispose() => _host.Dispose();   // Presenter を破棄 (Cleanup + 購読解除)。xUnit はテストごとに呼ぶ

    [Fact]
    public void Add_WithValidTitle_AddsItemAndClearsInput()
    {
        _view.NewTaskTitle = "Write tests";
        _view.RaiseInputChanged();

        _view.Binder.Dispatch(ToDoActions.Add);

        Assert.Contains(_view.Items, i => i.Title == "Write tests");
        Assert.Equal(string.Empty, _view.NewTaskTitle);
    }

    [Fact]
    public void Delete_WithoutSelection_HasCannotExecute()
    {
        _view.SelectedItem = null;
        _view.RaiseSelectionChanged();

        Assert.False(_view.Binder.CanDispatch(ToDoActions.Delete));
    }

    [Fact]
    public void Delete_ConfirmYes_RemovesItem()
    {
        _view.SelectedItem = _view.Items[0];
        _view.RaiseSelectionChanged();
        _fakes.Messages.EnqueueYesNo(true);   // 次の ConfirmYesNo に Yes と答える

        _view.Binder.Dispatch(ToDoActions.Delete);

        Assert.Contains(_fakes.Messages.Calls, m => m.Kind == MessageKind.YesNo);
        Assert.DoesNotContain(_view.Items, i => i.Title == "Read the framework docs");
    }

    [Fact]
    public void Save_AfterChange_ClearsDirtyAndShowsMessage()
    {
        _view.NewTaskTitle = "New";
        _view.RaiseInputChanged();
        _view.Binder.Dispatch(ToDoActions.Add);

        Assert.True(_view.HasUnsavedChanges);                            // dirty

        _view.Binder.Dispatch(ToDoActions.Save);

        Assert.False(_view.HasUnsavedChanges);                           // clean
        Assert.Contains(_fakes.Messages.Calls, m => m.Kind == MessageKind.Info);
    }
}
```

Mock View はインターフェイスを実装するだけのシンプルなクラスです (`public CapturingActionBinder Binder { get; } = new CapturingActionBinder(); public IViewActionBinder ActionBinder => Binder;` でアクションを Dispatch できるようにします)。詳細は [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) を参照。

---

## 完成形

これで以下の機能を持つアプリが完成しました。

- ✅ タスクの追加・削除・完了切り替え
- ✅ 入力なしで Add ボタン無効化 (`CanExecute`)
- ✅ 未選択で Delete/Toggle ボタン無効化
- ✅ 未保存変更を `ChangeTracker` で追跡
- ✅ × クリック時のダーティ確認
- ✅ Save ボタンが自動で Enabled / Disabled
- ✅ Presenter は WinForms に依存せず単体テスト可能

---

## 次のステップ

| 興味 | 読むべきページ |
|------|------------|
| ViewAction を深掘りしたい | [ViewAction システム](Reference-ViewAction-System) |
| 複数ウィンドウを扱うアプリにしたい | [WindowNavigator](Reference-WindowNavigator) |
| データを実ファイル/DB に保存したい | [Platform Services § ファイル・データベース・HTTP](Reference-Platform-Services#ファイルデータベースhttp) |
| 親子データの連動を扱いたい | [HowTo: マスター/詳細パターン](HowTo-Implement-Master-Detail) |
| 非同期処理 (大量データ読み込み等) を加えたい | [HowTo: 非同期処理を扱う](HowTo-Handle-Async-Operations) |
| 設計ルールを徹底的に理解したい | [MVP 設計ルール (全 17 条)](Design-Rules) |
| 既存の WinForms アプリを段階的に MVP 化したい | [HowTo: 従来の WinForms から移行する](HowTo-Migrate-From-Legacy-WinForms) |

完成形のコードは [`samples/Kibinago.Samples/ToDoDemo/`](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/Kibinago.Samples/ToDoDemo) を参照してください。

Happy coding!
