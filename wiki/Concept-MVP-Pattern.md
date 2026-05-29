# MVP パターンとは

このページでは、本フレームワークの土台となる **MVP (Model-View-Presenter)** パターンの考え方を、なぜそうするのか・どう違うのかという観点から解説します。
API の詳細を知りたい方は、各 [リファレンス](Home) ページを参照してください。

---

## 一言で言うと

> **画面 (View) と業務ロジック (Presenter) を厳格に切り離し、Presenter から WinForms 型への依存をゼロにする。**

これが MVP の核心です。フレームワーク全体の設計は、すべてこの一文を実現するための仕組みです。

---

## 3 つの構成要素

```
    ┌───────────────────────────────────────────────────────────────┐
    │                        MVP Pattern                            │
    └───────────────────────────────────────────────────────────────┘

    ┌──────────────┐         ┌──────────────┐         ┌──────────────┐
    │              │         │              │         │              │
    │    Model     │ ◄────── │  Presenter   │ ──────► │     View     │
    │              │         │              │         │ (Interface)  │
    │  Data & BL   │         │  Use cases   │         │              │
    └──────────────┘         └──────────────┘         └──────────────┘
           ▲                        ▲                        ▲
           │                        │                        │
    ┌──────┴───────┐         ┌──────┴───────┐         ┌──────┴────────┐
    │ Repositories │         │   Services   │         │ Form/Control  │
    │ DTOs/Entities│         │  Validation  │         │ (WinForms UI) │
    └──────────────┘         └──────────────┘         └───────────────┘
```

### Model — 業務データと業務ルール

| | |
|---|---|
| **何を持つか** | エンティティ、DTO、ドメインロジック |
| **典型例** | `Customer`、`Order`、`UserProfile` |
| **依存しないもの** | UI、Forms、Controls |

### View — ユーザーインターフェイス

| | |
|---|---|
| **何を持つか** | Form や UserControl の見た目と入出力 |
| **典型例** | `UserEditorForm`、`CustomerListControl` |
| **公開するもの** | データプロパティ・イベントを持つ **インターフェイス** (`IXxxView`) |
| **公開しないもの** | `Button` / `TextBox` 等の WinForms 型 |
| **依存しないもの** | 業務ロジック、データ検証 |

### Presenter — ユースケースのオーケストレーター

| | |
|---|---|
| **何を持つか** | バリデーション → 業務処理 → 通知の流れ (ユースケースロジック) |
| **典型例** | `UserEditorPresenter`、`CustomerListPresenter` |
| **連携するもの** | View インターフェイス、Service、Model |
| **依存しないもの** | `Button` / `TextBox` / `MessageBox` 等の WinForms 型 |

---

## ユーザー操作の流れ

```
1. ユーザーがボタンをクリック
        ↓
2. View が ViewAction を発火 (または従来のイベント)
        ↓
3. Presenter のハンドラが呼ばれる
        ↓
4. Presenter が Model / Service を呼び出す
        ↓
5. Presenter が View インターフェイス経由で結果を反映
        ↓
6. View が更新されてユーザーに見える
```

Presenter は手順 5 で `Button.Enabled = false` のようなことは **書きません**。
代わりに `View.HasSelectedItem` 等のインターフェイスプロパティ越しに状態を制御し、UI への反映は Form の責任です。これによって Presenter は単体テスト可能になります。

---

## なぜ MVC ではなく MVP か (Supervising Controller)

このフレームワークは MVP の中でも **Supervising Controller** という変種を採用しています。MVC との違いはここです:

| 観点 | 伝統的 MVC | Supervising Controller MVP |
|------|-----------|--------------------------|
| **View の賢さ** | 単純 (Controller が全コントロール値を明示的にセット) | 賢い (データバインド・イベント処理を View 自身が担う) |
| **Controller の責務** | ユースケース + View ロジック | **ユースケースのみ** |
| **View ロジック (見せ方の詳細)** | Controller が指示 | View が自分で持つ |

**核心となる原則:**

> **Presenter handles use-case logic only, not view logic.**
>
> 現代の WinForms View はデータバインド・イベント処理に十分賢い。
> Presenter は「検証 → 保存 → 通知」という **業務の流れ** だけを書き、
> エラーをどう赤字で出すか・どのコントロールを使うか等の **UI の詳細** は View が持つ。

---

## 3 つの鉄則 (Three Iron Rules)

このフレームワークで「MVP に違反していないか」を判定する 3 つの絶対基準です。
[MVP 設計ルール](Design-Rules) 全 17 条の根幹となるルールでもあります。

| 鉄則 | 内容 |
|------|------|
| **1. View インターフェイスは UI 型を露出させない** | プロパティ・メソッド・イベント引数すべてに `System.Windows.Forms` 名前空間の型を含めない |
| **2. Presenter は UI 型を直接扱わない** | `MessageBox.Show()` 禁止。すべてのサービスは `IMessageService` 等の抽象経由 |
| **3. 依存方向は Presenter → View の片方向のみ** | View から Presenter の **業務メソッド** を呼ばない (参照保持・ライフサイクル呼び出しは可) |

具体例・違反例・許容範囲の詳細は [MVP 設計ルール](Design-Rules) を参照してください。本ページでは「なぜこの 3 つが必要か」を以下の比較で示します。

---

## 従来の WinForms との比較

### ❌ 従来の WinForms

```csharp
public class UserForm : Form
{
    private void btnSave_Click(object sender, EventArgs e)
    {
        // 全部 Form の中で混ざっている
        if (string.IsNullOrEmpty(txtName.Text))
        {
            MessageBox.Show("Name required!");   // UI と業務ロジックが密結合
            return;
        }

        var user = new User { Name = txtName.Text };
        _repository.Save(user);                  // データアクセスも混在
        MessageBox.Show("Saved!");
        Close();
    }
}
```

**問題点:**
- 単体テスト不可 (Form のインスタンスが必要)
- 業務ロジックを再利用できない
- `MessageBox` への密結合
- リポジトリをモックできない

### ✅ MVP

```csharp
// View インターフェイス (UI 型なし)
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
    ViewActionBinder ActionBinder { get; }
}

// Presenter (WinForms 知識ゼロ・テスト可能)
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private readonly IUserRepository _repository;

    public UserEditorPresenter(IUserRepository repository)
    {
        _repository = repository;
    }

    protected override void RegisterViewActions()
        => Dispatcher.Register(CommonActions.Save, OnSave);

    private void OnSave()
    {
        if (string.IsNullOrEmpty(View.UserName))
        {
            Messages.ShowError("Name required!");
            return;
        }

        _repository.Save(new User { Name = View.UserName, Email = View.Email });
        Messages.ShowInfo("Saved!");
    }
}
```

**得られるもの:**
- ✅ 完全に単体テスト可能
- ✅ 業務ロジックを View と独立して再利用できる
- ✅ サービスをモックして検証可能
- ✅ WinForms から独立した Presenter

---

## このパターンが解決するもの

| 問題 | MVP による解決 |
|------|-------------|
| 🧪 **テスタビリティ** | Presenter は WinForms 不要で単体テスト可能。View はインターフェイス越しにモックする |
| 🔧 **保守性** | 関心の分離により、影響範囲が局所化される |
| ♻️ **再利用性** | Presenter ロジックを異なる View で再利用可能 |
| 🎨 **柔軟性** | UI を変えても業務ロジックは無傷 |
| 👥 **協業** | デザイナーは View、開発者は Presenter で並行作業可能 |

---

## このフレームワークでの実装方針

Supervising Controller MVP の実現のため、本フレームワークは以下を提供します。
詳細は各リンク先を参照してください。

| 仕組み | 役割 | 詳細 |
|--------|------|----|
| `WindowPresenterBase<TView>` 系 | Presenter の基底クラス (4 種類) | [Presenter 基底クラス](Reference-Presenter-Base-Classes) |
| ViewAction システム | UI イベントを宣言的アクションに変換 | [ViewAction システム](Reference-ViewAction-System) |
| `IMessageService` / `IDialogProvider` / `IFileService` | UI 操作のサービス抽象 | [Platform Services](Reference-Platform-Services) |
| `WindowNavigator` | ウィンドウのライフサイクル管理 | [WindowNavigator](Reference-WindowNavigator) |
| `IRequestClose<TResult>` + `WindowClosingEventArgs` | 二方向ウィンドウクローズモデル | [ウィンドウクローズモデル](Concept-Window-Closing-Model) |

---

## 次のステップ

| 目的 | 読むべきページ |
|------|------------|
| フレームワーク全体の仕組みを知りたい | [アーキテクチャ概観](Concept-Architecture-Overview) |
| ウィンドウクローズの設計を理解したい | [ウィンドウクローズモデル](Concept-Window-Closing-Model) |
| 17 条の設計ルールをまとめて読みたい | [MVP 設計ルール](Design-Rules) |
| 実際にコードを書き始めたい | [はじめに (Getting Started)](Getting-Started) |
