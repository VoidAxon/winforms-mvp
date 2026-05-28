# FAQ — よくある質問

このページは、フレームワークについてよく聞かれる質問とその答えを集めたものです。
特定のエラーメッセージへの対処法は [トラブルシューティング](Troubleshooting)、用語の定義は [用語集](Glossary) を参照してください。

---

## 目次

- [はじめての方向け](#はじめての方向け)
- [Presenter について](#presenter-について)
- [ViewAction について](#viewaction-について)
- [サービス・依存性注入について](#サービス依存性注入について)
- [ウィンドウとクローズ動作について](#ウィンドウとクローズ動作について)
- [テストについて](#テストについて)
- [互換性・対応バージョンについて](#互換性対応バージョンについて)

---

## はじめての方向け

### Q. 何から読めばよいですか?

A. 以下の順番がおすすめです。

1. [はじめに (Getting Started)](Getting-Started) — 5 分で動くサンプルを作る
2. [MVP パターンとは](Concept-MVP-Pattern) — なぜ MVP か
3. [アーキテクチャ概観](Concept-Architecture-Overview) — フレームワーク全体像

その後、興味のある [リファレンス](Home) ページに進んでください。

### Q. WPF を使えばよいのでは?

A. 状況次第です。新規プロジェクトで自由に選べるなら WPF / Avalonia / MAUI も候補ですが、以下のようなケースでは WinForms + 本フレームワークが選択肢になります。

- 既存の WinForms 資産を活かしながら近代化したい
- `net40` / `net48` ターゲットが必要
- デザイナ機能 (フォームデザイナでのドラッグ&ドロップ) を維持したい
- WPF/Avalonia の学習コストを避けたい

### Q. このフレームワークは何 KB ですか?

A. メインパッケージ (`WinformsMVP`) は **外部依存ゼロ**、コア DLL は ~100 KB 程度です。`net40` と `net48` をマルチターゲットしています。

---

## Presenter について

### Q. `WindowPresenterBase` と `ControlPresenterBase` の使い分けは?

A. View が `Form` なら `WindowPresenterBase`、`UserControl` なら `ControlPresenterBase` です。
さらにパラメータの有無で 4 種類に分かれます。詳しくは [Presenter 基底クラス](Reference-Presenter-Base-Classes) を参照。

### Q. Presenter から `MessageBox.Show()` を呼んでもよいですか?

A. いいえ。`MessageBox.Show()` は WinForms 型なので、[MVP の鉄則 2](Concept-MVP-Pattern#3-つの鉄則-three-iron-rules) に違反します。代わりに `Messages.ShowInfo()` 等を使ってください。

### Q. Presenter の中で `await` してもよいですか?

A. はい。`async void` の ViewAction ハンドラがフレームワークでサポートされる標準パターンです。詳しくは [HowTo: 非同期処理を扱う](HowTo-Handle-Async-Operations) を参照。

### Q. Presenter を `Dispose` するタイミングは?

A.

- **Form 系**: `WindowNavigator` が Form 閉じ時に自動で `Cleanup()` を呼びます
- **UserControl 系**: UserControl が `Dispose` されると Presenter も自動で `Cleanup()` を呼びます

`Cleanup()` を override してイベント購読の解除等を行ってください。

---

## ViewAction について

### Q. ボタンとアクションをどう紐付けますか?

A. View の `ActionBinder` プロパティに `_binder.Add(action, button)` で登録します。Presenter 側は `Dispatcher.Register(action, handler)` でハンドラを登録します。詳しくは [ViewAction システム](Reference-ViewAction-System) または [Getting Started § 3](Getting-Started) を参照。

### Q. `CanExecute` で Save ボタンを自動的にグレーアウトしたい

A.

```csharp
Dispatcher.Register(CommonActions.Save, OnSave,
    canExecute: () => View.HasUnsavedChanges);
```

これでフレームワークが自動的にボタンの `Enabled` プロパティを切り替えます。状態が変わったとき (例: TextBox の TextChanged) は `Dispatcher.RaiseCanExecuteChanged()` を呼んでください。

### Q. 同じアクションに複数のボタン (ツールバー + メニュー) を紐付けたい

A. `Add` を複数回呼ぶか、`AddRange` を使います。

```csharp
_binder.Add(CommonActions.Save, _saveButton, _saveMenuItem);
```

### Q. アクションにパラメータを渡せますか?

A. はい。`Register<T>` と `Dispatch<T>` を使います。

```csharp
Dispatcher.Register<string>(MyActions.Open, OnOpen);
Dispatcher.Dispatch(MyActions.Open, "/path/to/file");
```

詳しくは [ViewAction システム § パラメータ付きアクション](Reference-ViewAction-System#パラメータ付きアクション) を参照。

---

## サービス・依存性注入について

### Q. `IMessageService` を使うのに DI コンテナは必要ですか?

A. 不要です。Presenter からは `Messages` プロパティで直接アクセスできます (`PlatformServices.Default` 経由)。

業務サービス (`IUserRepository` 等) を Presenter に注入したい場合だけ、コンストラクタ注入 (+ 任意で DI コンテナ) を使います。詳しくは [Dependency Injection](Reference-DependencyInjection) を参照。

### Q. `Microsoft.Extensions.DependencyInjection` を使えますか?

A. はい、オプションで `WinformsMVP.DependencyInjection` パッケージを併用すれば統合できます (`net48` 以降)。詳しくは [Dependency Injection § M.E.DI との統合](Reference-DependencyInjection#medi-との統合) を参照。

### Q. ロギングはどう設定しますか?

A. 3 通りあります。

1. **何もしない** — デフォルトの `NullLoggerFactory` で出力なし
2. **`DebugLoggerFactory` を使う** — VS の Debug ウィンドウに出力。`net40` 対応
3. **M.E.L. アダプタ経由で本格的なロギング** — App Insights / Seq / Serilog 等を接続。`net48` 専用

詳しくは [Logging](Reference-Logging) を参照。

---

## ウィンドウとクローズ動作について

### Q. ダーティチェック (未保存の変更があるとき確認ダイアログ) を実装したい

A. `IWindowView.Closing` イベントを Presenter で購読して `args.Cancel = true` で閉じるのをブロックします。詳しくは [HowTo: ウィンドウクローズを扱う](HowTo-Handle-Window-Closing) を参照。

### Q. ダイアログから業務結果を返したい

A. Presenter に `IRequestClose<TResult>` を実装し、`CloseRequested` イベントを発火します。呼び出し側は `Navigator.For(presenter).ShowAsModal<TResult>()` で `InteractionResult<TResult>` を受け取ります。詳しくは [ウィンドウクローズモデル](Concept-Window-Closing-Model) を参照。

### Q. `ShowAsModal` の戻り値で 3 つの型引数を毎回書くのが面倒です

A. **Fluent API** を使うと `TPresenter` と `TParam` が型推論されます。

```csharp
// 3 引数形式
Navigator.ShowWindowAsModal<MyPresenter, MyParam, MyResult>(presenter, param);

// Fluent 形式
Navigator.For(presenter).WithParam(param).ShowAsModal<MyResult>();
```

詳しくは [WindowNavigator § Fluent API](Reference-WindowNavigator#fluent-api-tpresenter-型推論) を参照。

---

## テストについて

### Q. Presenter をテストするのに WinForms ランタイムは必要ですか?

A. 不要です。Presenter は WinForms に依存しないので、UI スレッドなしで直接インスタンス化してテストできます。

### Q. `MessageBox` の確認ダイアログをテストできますか?

A. はい。`MockMessageService.ConfirmYesNoResult = true/false` で戻り値を制御できます。

```csharp
_platform.MessageService.ConfirmYesNoResult = false;   // No を選択
_presenter.Dispatcher.Dispatch(CommonActions.Delete);
Assert.False(_view.WasDeleted);
```

詳しくは [HowTo: Presenter をテストする](HowTo-Test-A-Presenter) を参照。

### Q. テストで View を実体化する必要がありますか?

A. 不要です。`MockXxxView` を実装してインターフェイス越しにテストします。サンプルは `src/WinformsMVP.Samples.Tests/Mocks/` を参照。

---

## 互換性・対応バージョンについて

### Q. どの .NET バージョンをサポートしていますか?

A.

- メインパッケージ (`WinformsMVP`) — `net40` および `net48` のマルチターゲット
- オプションパッケージ (`WinformsMVP.DependencyInjection`) — `net48` のみ

### Q. .NET 5/6/7/8 で使えますか?

A. 現時点では .NET Framework 専用です。.NET (Core 系) 対応は将来的に検討予定です。

### Q. Visual Studio のフォームデザイナは使えますか?

A. はい。`Form` / `UserControl` を継承しているので、通常通りデザイナで編集できます。`InitializeActionBindings()` メソッドは `InitializeComponent()` の後に呼んでください。

### Q. WPF アプリの一部に組み込めますか?

A. 同じプロセス内で WinForms と WPF を混在させること自体は可能ですが、本フレームワークは WinForms 専用です。WPF 側は MVVM パターンで別途実装してください。

---

## それ以外の質問

ここに載っていない質問がある場合は、[GitHub Issues](https://github.com/VoidAxon/winforms-mvp/issues) でお気軽にお尋ねください。
