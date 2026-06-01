# コントリビューションガイド

WinForms MVP Framework への貢献を検討いただきありがとうございます。
本ドキュメントは Issue / Pull Request の出し方、コード規約、開発フローをまとめたものです。

---

## 目次

- [事前準備](#事前準備)
- [Issue を出すとき](#issue-を出すとき)
- [Pull Request を出すとき](#pull-request-を出すとき)
- [ブランチ戦略](#ブランチ戦略)
- [コード規約](#コード規約)
- [ドキュメント変更時](#ドキュメント変更時)
- [Wiki への貢献](#wiki-への貢献)
- [行為規範](#行為規範)
- [質問・相談](#質問相談)

---

## 事前準備

### 必要なもの

- .NET Framework 4.8 SDK (および任意で 4.0 ターゲットのため Microsoft Build Tools)
- Visual Studio 2019 以降 / JetBrains Rider / VS Code
- Git

### ビルドとテスト

```bash
# クローンしてビルド
git clone https://github.com/VoidAxon/winforms-mvp.git
cd winforms-mvp
dotnet build src/winforms-mvp.sln

# テスト実行
dotnet test src/WindowsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj

# サンプルアプリ実行
dotnet run --project src/WinformsMVP.Samples/WinformsMVP.Samples.csproj
```

すべてのテストが緑になることを確認してください。

---

## Issue を出すとき

### バグ報告

[Bug report テンプレート](.github/ISSUE_TEMPLATE/bug_report.md) を使って以下を記載してください:

1. 再現手順 (最小サンプルコードがあると助かります)
2. 期待される動作
3. 実際の動作
4. 環境 (.NET Framework のバージョン、OS、IDE)
5. スタックトレース (該当する場合)

### 機能リクエスト

[Feature request テンプレート](.github/ISSUE_TEMPLATE/feature_request.md) を使ってください:

1. 解決したい問題
2. 提案する API のスケッチ
3. なぜそれが現状の API で実現できないか
4. 代替案 (検討した他のアプローチ)

### 質問

質問は Issue ではなく [GitHub Discussions](https://github.com/VoidAxon/winforms-mvp/discussions) を推奨します (有効化されている場合)。

---

## Pull Request を出すとき

### 一般的なフロー

1. リポジトリを fork する
2. feature ブランチを切る (命名は [ブランチ戦略](#ブランチ戦略) 参照)
3. 変更を加える
4. テストを書く (バグ修正なら回帰テスト、新機能なら動作確認テスト)
5. `dotnet build` と `dotnet test` がローカルで通ることを確認
6. CHANGELOG.md の `[Unreleased]` セクションにエントリ追加
7. Pull Request を作成

### PR チェックリスト

`.github/PULL_REQUEST_TEMPLATE.md` に沿って:

- [ ] 関連 Issue がある場合は本文で言及 (`Closes #123` 等)
- [ ] 既存テストがすべて通ること
- [ ] 新規コードへのテスト追加 (該当する場合)
- [ ] パブリック API 変更は XML ドキュメントコメントを記載
- [ ] 動作変更時は CHANGELOG.md を更新
- [ ] 関連する Wiki ドキュメントの更新 (該当する場合)

### コミットメッセージ

Conventional Commits 形式を推奨します。

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

`type` の例:

| type | 用途 |
|------|------|
| `feat` | 新機能 |
| `fix` | バグ修正 |
| `docs` | ドキュメント |
| `test` | テスト追加・修正 |
| `refactor` | リファクタリング (動作変更なし) |
| `perf` | パフォーマンス改善 |
| `chore` | ビルド・補助ツール等 |

例:

```
feat(viewaction): Add Bulk binding via AddRange extension

Allow registering many controls to many actions in a single call.

Closes #42
```

---

## ブランチ戦略

| ブランチ | 用途 |
|---------|------|
| `master` | 安定版 |
| `feature/<topic>` | 新機能 (例: `feature/event-aggregator-weak-ref`) |
| `fix/<topic>` | バグ修正 (例: `fix/changetracker-null-comparer`) |
| `docs/<topic>` | ドキュメント (例: `docs/wiki-migration`) |
| `refactor/<topic>` | リファクタリング |

---

## コード規約

### 言語

- **ソースコード内のコメント・XML ドキュメント**: 英語
- **コミットメッセージ**: 英語または日本語
- **Issue / PR の本文**: 日本語または英語

### スタイル

- C# 7.3 互換 (`net40` ターゲットを壊さない)
- 4 スペースインデント
- 行末セミコロン必須
- `var` は型が右辺から明らかな場合のみ使う
- `private readonly` フィールドは `_` プレフィックス: `private readonly ILogger _logger;`

### MVP 設計ルール遵守

[MVP 設計ルール (全 17 条)](https://github.com/VoidAxon/winforms-mvp/wiki/Design-Rules) に従ってください。特に:

- View インターフェイスに UI 型 (`Button`、`DialogResult` 等) を露出させない
- Presenter から `MessageBox.Show()` を直接呼ばない
- Presenter の公開メンバーをコンストラクタとインターフェイス契約に限定

---

## ドキュメント変更時

### XML ドキュメントコメント

パブリック API には XML ドキュメントを記載してください。

```csharp
/// <summary>
/// Registers a handler for the given action with an optional CanExecute predicate.
/// </summary>
/// <param name="action">The action key.</param>
/// <param name="handler">The handler to invoke when dispatched.</param>
/// <param name="canExecute">Predicate returning true when the action is executable. Default = always.</param>
public void Register(ViewAction action, Action handler, Func<bool> canExecute = null)
```

### Wiki への変更が必要なケース

API 変更・新機能追加では、`wiki/` 配下の該当ページも更新してください。
詳細は [Wiki への貢献](#wiki-への貢献) 節を参照。

---

## Wiki への貢献

Wiki ソースは本リポジトリの `wiki/` ディレクトリにあります。**直接 GitHub Wiki を編集しないでください** (上書きされます)。

### 編集フロー

1. `wiki/*.md` を編集
2. `wiki/deploy-wiki.ps1` (Windows) または `wiki/deploy-wiki.sh` (Linux/Mac) を実行して GitHub Wiki へ反映
3. 通常のコード変更と同じ Pull Request に含める

詳細は `wiki/README.md` および `wiki/DEPLOY.md` を参照。

### Wiki ページの命名規約

- `PascalCase-With-Hyphens.md` 形式
- カテゴリ別接頭辞: `Concept-` / `Reference-` / `HowTo-`
- 例: `Reference-EventAggregator.md`、`HowTo-Handle-Errors.md`

---

## 行為規範

このプロジェクトのコントリビューターは [行為規範 (Code of Conduct)](CODE_OF_CONDUCT.md) の遵守に同意したものとみなされます。

---

## 質問・相談

- **使い方の質問**: [GitHub Discussions](https://github.com/VoidAxon/winforms-mvp/discussions) または [FAQ](https://github.com/VoidAxon/winforms-mvp/wiki/FAQ)
- **バグの可能性**: GitHub Issues
- **設計に関する議論**: Discussion または Issue (Type: discussion)
- **セキュリティ脆弱性**: [SECURITY.md](SECURITY.md) のとおり、公開 Issue ではなく報告手順に従う

ご質問・ご提案をお待ちしています。
