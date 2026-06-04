# チュートリアル: 最初のアプリを作る

このチュートリアルでは、**ToDo リストアプリ** を一から段階的に作りながら、フレームワークの主要機能を実際に体験します。
完了すると、以下の機能を持つ完結したアプリが手元に残ります。

- タスクの追加・編集・削除・完了切り替え
- 検証 (空のタスクを追加させない)
- 未保存変更の追跡と保存/破棄
- × ボタンで閉じる際のダーティ確認

> **前提条件**: [はじめに (Getting Started)](Getting-Started) で扱った基本概念 (View インターフェイス、Presenter、ViewAction) を理解していること。

完成形のサンプルコードは `samples/WinformsMVP.Samples/ToDoDemo/` にあります。詰まったらそちらと見比べてください。

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
| ウィンドウクローズ確認 | `View.Closing` + Presenter |

---

## Step 1: モデル (TodoItem) を作る

業務データを保持するシンプルなクラスです。`ICloneable` も実装しておくと、後の ChangeTracker で深いコピーが速くなります。

```csharp
using System;

namespace MyTodoApp
{
    public class ToDoItem : ICloneable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public bool IsCompleted { get; set; }

        public object Clone()
            => new ToDoItem { Id = Id, Title = Title, IsCompleted = IsCompleted };

        public override bool Equals(object obj)
            => obj is ToDoItem other
               && Id == other.Id
               && Title == other.Title
               && IsCompleted == other.IsCompleted;

        public override int GetHashCode()
            => (Id, Title, IsCompleted).GetHashCode();
    }
}
```

ポイント:

- `Equals` を override しないと `ChangeTracker` が参照比較になってしまう (詳細: [ChangeTracker § 比較の解決順序](Reference-ChangeTracker#比較の解決順序))
- `Clone()` 内で値型・`string` はそのままコピー (浅いコピーで OK)

---

## Step 2: View インターフェイスを定義する

UI 型は一切露出させません ([MVP 鉄則 1](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules))。

```csharp
using System;
using System.Collections.Generic;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;

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
        ViewActionBinder ActionBinder { get; }

        // 状態変化イベント (CanExecute 再評価用)
        event EventHandler SelectionChanged;
        event EventHandler InputChanged;
    }
}
```

---

## Step 3: ActionKey を定義する

```csharp
using WinformsMVP.MVP.ViewActions;

namespace MyTodoApp
{
    public static class ToDoActions
    {
        public static readonly ViewAction Add            = ViewAction.Create("ToDo.Add");
        public static readonly ViewAction Delete         = ViewAction.Create("ToDo.Delete");
        public static readonly ViewAction ToggleComplete = ViewAction.Create("ToDo.ToggleComplete");
        public static readonly ViewAction Save           = ViewAction.Create("ToDo.Save");
    }
}
```

---

## Step 4: Presenter の骨格を作る

まず空の Presenter を作り、後で機能を足していきます。

```csharp
using System.Collections.Generic;
using WinformsMVP.MVP.Presenters;

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
            View.SelectionChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
            View.InputChanged     += (s, e) => Dispatcher.RaiseCanExecuteChanged();
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
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

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

        public ViewActionBinder ActionBinder => _binder;
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

        // IWindowView.Closing は明示的実装 (後の Step 8 で使う)
        private EventHandler<WindowClosingEventArgs> _closing;
        event EventHandler<WindowClosingEventArgs> IWindowView.Closing
        {
            add => _closing += value;
            remove => _closing -= value;
        }
        void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);
    }
}
```

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
using WinformsMVP.Common;

public class ToDoPresenter : WindowPresenterBase<IToDoView>
{
    private ChangeTracker<List<ToDoItem>> _tracker;

    protected override void OnInitialize()
    {
        var items = LoadInitialItems();
        _tracker = new ChangeTracker<List<ToDoItem>>(items);
        View.Items = _tracker.CurrentValue;
        UpdateDirtyState();

        _tracker.IsChangedChanged += (s, e) =>
        {
            UpdateDirtyState();
            Dispatcher.RaiseCanExecuteChanged();
        };
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
            canExecute: () => _tracker.IsChanged);     // ← 未保存があるときだけ Save 有効
    }

    private void OnAdd()
    {
        var newList = _tracker.CurrentValue.ToList();
        newList.Add(new ToDoItem { Title = View.NewTaskTitle.Trim() });
        _tracker.UpdateCurrentValue(newList);
        View.Items = _tracker.CurrentValue;
        View.NewTaskTitle = string.Empty;
    }

    // OnDelete / OnToggleComplete も同様に _tracker.UpdateCurrentValue を呼ぶ

    private void OnSave()
    {
        SaveToStorage(_tracker.CurrentValue);
        _tracker.AcceptChanges();                       // ← 確定
        Messages.ShowInfo("Saved successfully!", "Save");
    }

    private void UpdateDirtyState()
        => View.HasUnsavedChanges = _tracker.IsChanged;

    private void SaveToStorage(List<ToDoItem> items)
    {
        // 実際には JSON や DB に保存する。ここでは簡略化のため省略。
    }
}
```

> **重要**: `_items` を直接変更するのではなく、新しいリストを作って `_tracker.UpdateCurrentValue(newList)` を呼びます。これで `ChangeTracker` が変更を検知できます。

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

Pull 方向 (× ボタン) のクローズ要求に対して、未保存があれば確認します。

```csharp
protected override void OnViewAttached()
{
    View.SelectionChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
    View.InputChanged     += (s, e) => Dispatcher.RaiseCanExecuteChanged();
    View.Closing          += OnViewClosing;
}

private void OnViewClosing(object sender, WindowClosingEventArgs args)
{
    if (args.Reason == CloseReason.SystemShutdown ||
        args.Reason == CloseReason.TaskManager)
        return;

    if (_tracker.IsChanged &&
        !Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm"))
    {
        args.Cancel = true;
    }
}
```

これでユーザーが × を押すと、未保存ならダイアログが出ます。「いいえ」を選ぶとクローズがブロックされます。
詳細は [ウィンドウクローズモデル](Concept-Window-Closing-Model) を参照。

---

## Step 9: テストを書く

Presenter は WinForms に依存しないので、UI なしでテストできます。

```csharp
public class ToDoPresenterTests
{
    private readonly MockPlatformServices _platform;
    private readonly MockToDoView _view;
    private readonly ToDoPresenter _presenter;

    public ToDoPresenterTests()
    {
        _platform = new MockPlatformServices();
        _view = new MockToDoView();
        _presenter = new ToDoPresenter().WithPlatformServices(_platform);

        _presenter.AttachView(_view);
        _presenter.Initialize();

        _platform.Reset();
    }

    [Fact]
    public void Add_WithValidTitle_AddsItemAndClearsInput()
    {
        _view.NewTaskTitle = "Write tests";
        _view.RaiseInputChanged();

        _presenter.Dispatcher.Dispatch(ToDoActions.Add);

        Assert.Contains(_view.Items, i => i.Title == "Write tests");
        Assert.Equal(string.Empty, _view.NewTaskTitle);
    }

    [Fact]
    public void Delete_WithoutSelection_HasCannotExecute()
    {
        _view.SelectedItem = null;
        _view.RaiseSelectionChanged();

        Assert.False(_presenter.Dispatcher.CanDispatch(ToDoActions.Delete));
    }

    [Fact]
    public void Delete_ConfirmYes_RemovesItem()
    {
        _view.SelectedItem = _view.Items[0];
        _view.RaiseSelectionChanged();
        _platform.MessageService.ConfirmYesNoResult = true;

        _presenter.Dispatcher.Dispatch(ToDoActions.Delete);

        Assert.True(_platform.MessageService.ConfirmDialogShown);
        Assert.DoesNotContain(_view.Items, i => i.Title == "Read the framework docs");
    }

    [Fact]
    public void Save_AfterChange_ClearsDirtyAndShowsMessage()
    {
        _view.NewTaskTitle = "New";
        _view.RaiseInputChanged();
        _presenter.Dispatcher.Dispatch(ToDoActions.Add);

        Assert.True(_view.HasUnsavedChanges);                            // dirty

        _presenter.Dispatcher.Dispatch(ToDoActions.Save);

        Assert.False(_view.HasUnsavedChanges);                           // clean
        Assert.True(_platform.MessageService.InfoMessageShown);
    }
}
```

Mock View はインターフェイスを実装するだけのシンプルなクラスです。詳細は [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) を参照。

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
| データを実ファイル/DB に保存したい | [Platform Services](Reference-Platform-Services) (`IFileService`) |
| 親子データの連動を扱いたい | [HowTo: マスター/詳細パターン](HowTo-Implement-Master-Detail) |
| 非同期処理 (大量データ読み込み等) を加えたい | [HowTo: 非同期処理を扱う](HowTo-Handle-Async-Operations) |
| 設計ルールを徹底的に理解したい | [MVP 設計ルール (全 17 条)](Design-Rules) |
| 既存の WinForms アプリを段階的に MVP 化したい | [HowTo: 従来の WinForms から移行する](HowTo-Migrate-From-Legacy-WinForms) |

完成形のコードは `samples/WinformsMVP.Samples/ToDoDemo/` を参照してください。

Happy coding!
