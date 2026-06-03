WinForms アプリケーションのための **Model-View-Presenter** フレームワークの公式ドキュメントです。

> **このフレームワークが初めての方は、まず [はじめに (Getting Started)](Getting-Started) からどうぞ。**
> 5 分で動くサンプルを作りながら、フレームワークの全体像を掴めます。

---

## ドキュメント構成

このフレームワークのドキュメントは、用途に応じて 4 つのカテゴリに分かれています ([Diátaxis フレームワーク](https://diataxis.fr/) に基づく構成)。

| カテゴリ | こんなときに | 例 |
|---------|------------|----|
| **チュートリアル** | 学びたい | 「最初の MVP アプリを作る」 |
| **ハウツーガイド** | 課題を解決したい | 「Presenter 間で通信するには」 |
| **リファレンス** | 仕様を調べたい | 「`WindowNavigator` の全 API」 |
| **コンセプト** | 設計思想を理解したい | 「なぜ MVP か、どう違うか」 |

---

## チュートリアル (Tutorials)

実際に動かしながら学ぶ、初心者向けの段階的ガイドです。

- [はじめに (Getting Started)](Getting-Started) — 5 分で動かす最小サンプル
- [チュートリアル: 最初のアプリを作る](Tutorial-Building-Your-First-App) — Hello World から完結したアプリへ

---

## コンセプト (Concepts)

フレームワークが「なぜそうなっているか」を解説します。

- [MVP パターンとは](Concept-MVP-Pattern) — MVC との違い、Supervising Controller パターン
- [アーキテクチャ概観](Concept-Architecture-Overview) — フレームワーク全体図、コンポーネントの関係
- [ウィンドウクローズモデル](Concept-Window-Closing-Model) — Push / Pull 二方向の設計思想
- [Presenter の責務と肥大化の防止](Concept-Presenter-Responsibilities) — 痩せた監督、Fat Presenter の防ぎ方

---

## リファレンス (Reference)

各機能の詳細な仕様と API です。

- [Presenter 基底クラス](Reference-Presenter-Base-Classes) — Window / Control × パラメータ有無の 4 種類
- [ViewAction システム](Reference-ViewAction-System) — Dispatcher / Binder / Middleware の完全リファレンス
- [WindowNavigator](Reference-WindowNavigator) — Modal / 非 Modal、Fluent API、`IRequestClose<TResult>`
- [ViewMappingRegister](Reference-ViewMappingRegister) — View 自動登録、Factory パターン
- [Platform Services](Reference-Platform-Services) — `IMessageService` / `IDialogProvider` / `IFileService`
- [Data Binding 拡張メソッド](Reference-Data-Binding) — View 側で UI コントロールをモデルに紐付ける拡張メソッド群
- [Logging](Reference-Logging) — 自社抽象、`net40` 対応、M.E.L. 連携
- [ChangeTracker](Reference-ChangeTracker) — 編集/キャンセル、深いコピー、フック差し込み
- [EventAggregator](Reference-EventAggregator) — 弱参照 pub/sub、UI スレッド自動マーシャリング
- [Dependency Injection](Reference-DependencyInjection) — 3 つの DI パターン、複数プロジェクト構成

---

## ハウツーガイド (How-To)

特定の課題に対する、目的別レシピです。

- [Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) — 4 つの選択肢と使い分け
- [フォーム入力を検証する](HowTo-Validate-Form-Input) — フィールド/クロスフィールド/パターン検証
- [非同期処理を扱う](HowTo-Handle-Async-Operations) — async/await、`IProgress<T>`、キャンセル
- [マスター/詳細パターンを実装する](HowTo-Implement-Master-Detail) — 親子データの連動
- [Presenter をテストする](HowTo-Test-A-Presenter) — モック、テストパターン
- [従来の WinForms から移行する](HowTo-Migrate-From-Legacy-WinForms) — 段階的なリファクタリング
- [エラー処理戦略](HowTo-Handle-Errors) — `IMessageService` / `InteractionResult<T>` / グローバルハンドラ
- [リリースする](HowTo-Release) — タグ駆動で GitHub Packages へ発行 + GitHub Release 作成

---

## 設計ルール

- [MVP 設計ルール (全 17 条)](Design-Rules) — 命名規約、責務分離、Tell-Don't-Ask、インターフェイス設計

---

## その他

- [FAQ](FAQ) — よくある質問
- [トラブルシューティング](Troubleshooting) — エラーメッセージ別の対処法
- [用語集](Glossary) — フレームワーク内で使われる用語の定義

---

## 外部リンク

- [GitHub リポジトリ](https://github.com/VoidAxon/winforms-mvp) — ソースコード
- [サンプルコード](https://github.com/VoidAxon/winforms-mvp/tree/master/samples/WinformsMVP.Samples) — 全サンプル
- [テストプロジェクト](https://github.com/VoidAxon/winforms-mvp/tree/master/tests/WinformsMVP.Samples.Tests) — xUnit テスト

---

## よくある最初の疑問

**Q: 何から読めばよいですか?**
A: [はじめに (Getting Started)](Getting-Started) → [MVP パターンとは](Concept-MVP-Pattern) → [アーキテクチャ概観](Concept-Architecture-Overview) の順で読むと、土台ができます。

**Q: ViewAction って何ですか?**
A: WPF の `ICommand` を WinForms に持ち込んだ仕組みです。ボタンクリックを宣言的にアクションへバインドし、`CanExecute` で自動的に有効/無効を切り替えます。詳しくは [ViewAction システム](Reference-ViewAction-System) を参照してください。

**Q: Presenter から `MessageBox.Show()` を呼んでもよいですか?**
A: いいえ。Presenter は WinForms 型に依存してはいけません。代わりに `Messages.ShowInfo()` 等のサービス抽象を使ってください。詳しくは [Platform Services](Reference-Platform-Services) を参照してください。

**Q: `WindowPresenterBase` と `ControlPresenterBase` の使い分けは?**
A: Form を扱うなら `WindowPresenterBase`、UserControl を扱うなら `ControlPresenterBase` です。それぞれパラメータ付きバージョンもあります。詳しくは [Presenter 基底クラス](Reference-Presenter-Base-Classes) を参照してください。
