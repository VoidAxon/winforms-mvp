# 変更履歴

このプロジェクトの注目すべき変更はすべてこのファイルに記録します。

形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に従い、バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) を採用します。

---

## [Unreleased]

### Added (追加)

- ドキュメント体系を Diátaxis フレームワークに沿って再構築。GitHub Wiki に 27 ページの日本語ドキュメントを公開
- `WinformsMVP.DependencyInjection` パッケージ — `Microsoft.Extensions.DependencyInjection` 連携用 (`net48`)
- `WinformsMVP.Logging` 名前空間 — M.E.L. 互換 API の自社ロギング抽象 (`net40` 対応)
- コミュニティファイル: `CONTRIBUTING.md` / `CODE_OF_CONDUCT.md` / `SECURITY.md` および `.github/` テンプレート

### Changed (変更)

- `ChangeTracker<T>` を `where T : class` に緩和し、`ICloneable` 実装を任意化
- 比較・複製の戦略をフック化 (`ChangeTrackerDefaults.Cloner` / `Comparer`)
- 既定の比較器を per-closed-T でキャッシュ

### Removed (削除)

- `WinformsMVP.Logging.MicrosoftExtensions` アダプタパッケージ
  — メインパッケージから M.E.L. 依存を完全排除。M.E.L. 連携が必要な場合はアプリ側で ~30 行のアダプタを書く方式に変更
- `IRequestClose.CanClose()` メソッド — Push/Pull 二方向イベントモデルへ移行
- 旧 root-level ドキュメント (`QUICKSTART.md` / `MVP-DESIGN-RULES.md` 等) と `docs/*.md` の主題ドキュメント類
  — 内容を GitHub Wiki へ移行

### Fixed (修正)

- `Equals` 未実装モデルが `ChangeTracker` で常に `IsChanged == true` になる問題

---

## 形式について

このファイルは [Keep a Changelog v1.1.0](https://keepachangelog.com/ja/1.1.0/) のガイドラインに従います。

セクションの意味:

- **Added** — 新機能
- **Changed** — 既存機能の変更
- **Deprecated** — 近々削除される機能
- **Removed** — 削除された機能
- **Fixed** — バグ修正
- **Security** — 脆弱性関連

リリース後は `[Unreleased]` セクションの内容を新しいバージョンセクションに移動し、`[Unreleased]` を空にしてください。
