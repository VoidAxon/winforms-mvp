# 変更履歴

このプロジェクトの注目すべき変更はすべてこのファイルに記録します。

形式は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に従い、バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) を採用します。

---

## [Unreleased]

## [1.0.0-preview.2] - 2026-06-07

### Added (追加)

- **Window closing redesign — `CanClose` Pull gate** — `WindowPresenterBaseCore<TView>` に `protected virtual bool CanClose(CloseReason reason)` override と非同期バリアント `protected virtual void CanClose(CloseReason reason, Action<bool> proceed)` を追加。Presenter のクローズポリシーが一箇所に集約される。
- **Push close は基底 `RequestClose` メソッドに一本化** — `WindowPresenterBaseCore<TView>` に `protected void RequestClose(InteractionStatus status = InteractionStatus.Ok)` (結果なし) と `protected void RequestClose<TResult>(TResult result, InteractionStatus status = InteractionStatus.Ok)` (型付き結果、`TResult` は引数推論) を追加。インターフェイス実装も拡張メソッドも不要になった。`IRequestClose<TResult>` マーカーインターフェイスおよび `RequestCloseExtensions` は削除。
- **`WindowCloseController`** — 1 Form につき 1 インスタンスの内部コントローラ。Push sink (`ICloseSink`)、Pull bridge (`FormClosing`)、結果収束 (`FormClosed`) を統合。`CloseRequestedBeforeShow` / `ConvergeWithoutShow` でクローズ前表示エッジケースに対応。
- **Adopted hosting** — `presenter.Connect(form)` / `Connect<TView, TResult>(form, onClosed)` / `Connect<TView, TParam>(form, param)` 拡張メソッド (`WindowPresenterConnectExtensions`)。シェルウィンドウ・レガシー Form 移行時にフレームワークのクローズ機構を接続できる。シングルオーナーポリシーを遵守。
- ドキュメント体系を Diátaxis フレームワークに沿って再構築。GitHub Wiki に日本語ドキュメントを公開
- `WinformsMVP.DependencyInjection` パッケージ — `Microsoft.Extensions.DependencyInjection` 連携用 (`net48`)
- `WinformsMVP.Logging` 名前空間 — M.E.L. 互換 API の自社ロギング抽象 (`net40` 対応)
- コミュニティファイル: `CONTRIBUTING.md` / `CODE_OF_CONDUCT.md` / `SECURITY.md` および `.github/` テンプレート

### Changed (変更)

- **`IWindowView` からクローズメンバーを削除** (BREAKING) — `IWindowView.Closing` イベント / `IWindowView.OnClosing` メソッドを削除。クローズ制御は Presenter の `CanClose` override に一本化。Forms にクローズ用ボイラープレートは不要になった。
- **ナビゲーションの `owner` 引数を `IWindowView` に変更** (BREAKING) — `IWindowNavigator` / `NavigationContext.ShowAsModal` 等の `owner` 引数を WinForms の `IWin32Window` から框架抽象の `IWindowView` に変更。presenter 向けナビゲーション API から WinForms 型が消えた。`WindowNavigator` が実行時に実ウィンドウへ解決し、ウィンドウ非対応のビューが渡された場合は `ArgumentException`(fail-fast)を投げる。
- `ChangeTracker<T>` を `where T : class` に緩和し、`ICloneable` 実装を任意化
- 比較・複製の戦略をフック化 (`ChangeTrackerDefaults.Cloner` / `Comparer`)
- 既定の比較器を per-closed-T でキャッシュ

### Removed (削除)

- **`WindowClosingEventArgs`** (BREAKING) — `IWindowView.Closing` イベントと共に削除。
- **`CloseRequestedEventArgs<TResult>`** (BREAKING) — `IRequestClose<TResult>.CloseRequested` イベントと共に削除。
- **`IRequestClose<TResult>.CloseRequested` イベント** (BREAKING) — 削除。代替: 基底 `RequestClose(result, status)` メソッド。
- **`IRequestClose<TResult>` マーカーインターフェイス** (BREAKING) — 削除。Presenter クラスへの実装宣言は不要になった。
- **`RequestCloseExtensions`** (BREAKING) — 削除。`RequestClose` は基底クラスのメソッドとして直接提供される。
- **`WindowClosingBridge`** (BREAKING) — 削除。`WindowCloseController` がブリッジ機能を統合。
- **`WindowCloseCoordinator`** (BREAKING) — 削除。`WindowCloseController` が _suppressGate フラグで同等の保証を提供。
- **`IWindowView` から `IsDisposed` / `Activate` と `IWin32Window` 基底を削除** (BREAKING) — これらはインターフェイス経由で消費されていなかった(`WindowNavigator` は具象 `Form` の同名メンバーを使う)。`IWindowView` は `: IActionableView` の純粋なマーカーとなり、WinForms 型を一切持たなくなった。
- `WinformsMVP.Logging.MicrosoftExtensions` アダプタパッケージ
  — メインパッケージから M.E.L. 依存を完全排除。M.E.L. 連携が必要な場合はアプリ側で ~30 行のアダプタを書く方式に変更
- `IRequestClose.CanClose()` メソッド (旧バージョンの遺物) — `CanClose(CloseReason)` override に置き換え
- 旧 root-level ドキュメント (`QUICKSTART.md` / `MVP-DESIGN-RULES.md` 等) と `docs/*.md` の主題ドキュメント類
  — 内容を GitHub Wiki へ移行

### Fixed (修正)

- `Equals` 未実装モデルが `ChangeTracker` で常に `IsChanged == true` になる問題

---

### Migration guide — window closing redesign

**Forms**: Remove the `IWindowView.Closing` boilerplate (the `_closing` field and explicit-interface add/remove/invoke). Forms now need no closing code at all.

**Presenters**:
- Replace `View.Closing += OnViewClosing` with `protected override bool CanClose(CloseReason reason)`.
- Replace `CloseRequested?.Invoke(this, new CloseRequestedEventArgs<TResult>(result, status))` and the `public event` declaration with `RequestClose(result, status)`. Remove `, IRequestClose<TResult>` from the class declaration — it is deleted. No extension method required.
- The `RaiseClose` helper pattern is no longer needed.

**Tests**:
- Pull: replace `view.RaiseClosing(new WindowClosingEventArgs(...))` with `((ICloseParticipant)presenter).CanCloseGate(reason, ok => ...)`.
- Push: replace `presenter.CloseRequested += (s,e) => captured = e` with a bound `ICloseSink` recording fake (`((ICloseParticipant)presenter).BindCloseSink(sink)`).

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
