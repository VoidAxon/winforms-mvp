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

このフレームワークは MVP の **Supervising Controller** という変種を採用しています。同じ GUI アーキテクチャの系譜にはいくつかの変種があるので、まず整理しておきます。

| 観点 | 古典的 GUI MVC (Smalltalk 系) | Passive View MVP | Supervising Controller MVP |
|------|-------|-------|-------|
| **View の賢さ** | 単純 — UI コントロールに直接アクセス | 完全に "dumb" — プロパティとメソッドだけ持つ | 賢い — 単純なデータバインドは View 内で完結 |
| **Controller / Presenter の責務** | ユースケース + View ロジック (両方) | あらゆる UI 更新 (最も単純な代入も含む) | **ユースケース + 複雑な View ロジック のみ** |
| **データバインド** | 手動 (Controller が setter を呼ぶ) | View 抽象越しに Presenter が全プロパティを書く | 単純な field-to-property は View が直接バインド、Presenter は触らない |
| **可測性** | △ Controller と View が密結合 | ◎ View 完全モック可 | ○ View 抽象越しに複雑ロジックはテスト可 (単純バインドはテスト対象外) |
| **ボイラープレート** | 多い | 多い (代入だけのためのインターフェイスメソッドが大量) | 中程度 |

> 💡 「MVC」と一口に言っても **Smalltalk MVC・Web MVC (Rails 等)・iOS MVC** など語義が大きく異なります。ここで比較しているのは **GUI リッチクライアント** に適用される Smalltalk 起源の系譜です。Web MVC とは別物として読んでください。

### 核心となる原則

> **Presenter handles use-case logic only, not view logic.**
>
> 現代の WinForms View はデータバインド・イベント処理に十分賢いので、
> Presenter は「検証 → 保存 → 通知」という **業務の流れ** だけを書きます。
> エラーをどう赤字で出すか・どのコントロールを使うか等の **UI の見た目の詳細** は View が持ちます。

ただしこれは「Presenter が View を一切触らない」という意味では **ありません**。Presenter は **View 抽象 (`IXxxView` インターフェイス)** を通じて、**複雑な UI 操作** (条件付き表示、動的な有効/無効化、エラーメッセージ表示等) を指示します。**触らないのは具体的なコントロール (Button・TextBox 等)** であって、View そのものではありません。

| 例 | 担当 | 理由 |
|---|---|---|
| `txtName.Text` ↔ `Model.Name` の単純な双方向バインド | View | データバインドで十分 |
| 入力エラー時に該当フィールドを赤くハイライト | View | 「どう赤くするか」は UI 詳細 |
| 「入力された名前が DB に存在しない」というドメイン検証結果 | Presenter → `View.ShowValidationError(...)` | 業務判断 |
| 「未保存変更がある時に保存ボタンを有効化」 | Presenter → `canExecute: () => _changeTracker.IsChanged` | 業務状態の判定 |

### 使い分けの目安 (フォームごと・フォーム内、どちらで混ぜても OK)

Passive View と Supervising Controller は **対立する 2 つの選択** ではなく、**同じ MVP の連続スペクトラム** 上の 2 つの目印です。Martin Fowler 自身も "the boundaries are fluid" と述べており、同じプロジェクト内でフォームごとに違うスタイルを採ること、さらには同じフォーム内で両者を混在させることも、まったく問題ありません (実務では「基本フィールドはバインド、検証エラーや活性制御は明示メソッド」というハイブリッドが最も多い)。

このフレームワークの API (データバインド・ChangeTracker・ViewAction の `canExecute` 等) は **デフォルトの "recipe"** としては Supervising Controller 寄りに設計されています。理由は、WinForms のデータバインド機構が十分成熟しており、単純な field-to-property を Presenter に書くのは記述量の割に得るものが少ないためです。テスト容易性が重要な箇所だけ局所的に Passive View 寄りに倒す方が、トータルの開発コストが低いという判断です。

Passive View 寄りに書くことも可能で、必要に応じて `View.SetXxx(...)` 形式のメソッドを追加し、Presenter から明示的に呼ぶスタイルに切り替えられます。

判断の目安:

| そのフォームで重視したいこと | 寄せる先 | 理由 |
|---|---|---|
| Presenter 単体で複雑な業務ロジックを完全にテストしたい | Passive View 寄り | UI 副作用も全部 View 抽象越しなら、テストで挙動が完全に観測できる |
| クロスフィールド検証・条件付き表示・段階的ウィザード | Passive View 寄り | View ロジックが複雑なほど Presenter で書いた方がテスト容易 |
| データバインドで済む単純なフォーム | Supervising Controller 寄り | `BindingSource` で済む話を Presenter に書くのは過剰 |
| 一覧表示・詳細表示・読み取り専用画面 | Supervising Controller 寄り | UI 更新を全部 setter にする意味が薄い |

> ⚠️ **避けたいパターン**: 同じフォーム内で **原則なく** 両スタイルが混在 —「ある項目はバインド、似た別項目は setter」のような一貫性のない書き方は保守性が下がります。**「混ぜる」と「無秩序に混ぜる」は別物** であり、チーム内で判断基準を明文化しておくことが重要です。

つまり「**モデル名は語彙であってドグマではない**」が本フレームワークの立場です。Passive View と Supervising Controller は、コードレビューや設計議論で「このフォームはどっち寄りにする?」を相手に伝えるための共通言語として使ってください。

> 📚 詳しくは [Martin Fowler — GUI Architectures](https://martinfowler.com/eaaDev/uiArchs.html) (Supervising Controller / Passive View の原典) を参照してください。

---

## 3 つの鉄則 (Three Iron Rules)

このフレームワークで「MVP に違反していないか」を判定する 3 つの絶対基準です。
[MVP 設計ルール](Design-Rules) 全 17 条の根幹となるルールでもあります。

| 鉄則 | 内容 |
|------|------|
| **1. View インターフェイスは UI 型を露出させない** | プロパティ・メソッド・イベント引数すべてに `System.Windows.Forms` 名前空間の型を含めない |
| **2. Presenter は UI 型を直接扱わない** | `MessageBox.Show()` 禁止。すべてのサービスは `IMessageService` 等の抽象経由 |
| **3. View は Presenter の業務メソッドを直接呼ばない** | ユーザー操作は ViewAction (アクションキー) かイベントで Presenter に仲介する。`presenter.Save()` のような業務メソッドの直接呼び出しは禁止。理想は View が Presenter を一切参照しないこと (ViewAction システムがこの方向を仲介するため成立する設計)。レガシー統合では参照保持・ライフサイクル配線までは妥協として許容するが、業務メソッド呼び出しは不可 |

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
        => Dispatcher.Register(StandardActions.Save, OnSave);

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
| `RequestClose` / `CanClose` override | 二方向ウィンドウクローズモデル (Pull ゲート / Push result) | [ウィンドウクローズモデル](Concept-Window-Closing-Model) |

---

## 次のステップ

| 目的 | 読むべきページ |
|------|------------|
| フレームワーク全体の仕組みを知りたい | [アーキテクチャ概観](Concept-Architecture-Overview) |
| ウィンドウクローズの設計を理解したい | [ウィンドウクローズモデル](Concept-Window-Closing-Model) |
| 17 条の設計ルールをまとめて読みたい | [MVP 設計ルール](Design-Rules) |
| 実際にコードを書き始めたい | [はじめに (Getting Started)](Getting-Started) |
