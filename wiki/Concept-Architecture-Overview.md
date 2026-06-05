# アーキテクチャ概観

このページでは、本フレームワークの **全体像** と、各コンポーネントの位置関係を俯瞰します。
MVP パターン自体の思想は [MVP パターンとは](Concept-MVP-Pattern) を、各コンポーネントの API 詳細は [リファレンス](Home) を参照してください。

---

## 全体図

```
┌─────────────────────────────────────────────────────────────────┐
│                       User Interaction                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  View Layer (Forms / UserControls)                              │
│   • UI の表示・ユーザー入力の捕捉                                 │
│   • IView インターフェイスを実装                                  │
│   • WinForms 知識はここに閉じ込める                              │
└─────────────────────────────────────────────────────────────────┘
                              │
                  ViewAction (宣言的バインド)
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Presenter Layer (ユースケースロジック)                          │
│   • 業務処理・バリデーション・状態管理                            │
│   • View インターフェイス経由でだけ画面を操作                      │
│   • WinForms 型への依存ゼロ                                     │
└─────────────────────────────────────────────────────────────────┘
            │                                       │
            ▼                                       ▼
┌─────────────────────────────┐   ┌─────────────────────────────┐
│  Model Layer                │   │  Platform Services Layer    │
│   • Repository              │   │   • IMessageService         │
│   • Domain Model / DTO      │   │   • IDialogProvider         │
│   • Business Logic          │   │   • IFileService            │
│                             │   │   • ILogger                 │
└─────────────────────────────┘   │   • IWindowNavigator        │
                                  └─────────────────────────────┘
```

「Presenter Layer から下は WinForms を一切知らない」ことが、テスト可能性と保守性の根源です。

---

## レイヤーの責務

| レイヤー | 役割 | WinForms 知識 |
|---------|------|--------------|
| **View Layer** | 画面の見た目、ユーザー入力の捕捉、UI 状態の保持 | あり (内部に閉じる) |
| **Presenter Layer** | ユースケースの調整、ビジネスフロー、状態の最終決定 | ゼロ |
| **Model Layer** | データとビジネスルール (Repository、DTO、ドメインロジック) | ゼロ |
| **Platform Services Layer** | UI 操作・I/O・ロギング等の抽象化 | サービス実装にのみ存在 |

---

## 主要コンポーネント早見表

| コンポーネント | 担当 | 詳細 |
|--------------|------|----|
| `PresenterBase<TView>` 系 4 クラス | Presenter の基底クラス (Form/Control × ±Param) | [Presenter 基底クラス](Reference-Presenter-Base-Classes) |
| **ViewAction システム** | UI イベント → 宣言的アクション → ハンドラの経路 | [ViewAction システム](Reference-ViewAction-System) |
| `WindowNavigator` | Modal/Non-Modal ウィンドウのライフサイクル管理 | [WindowNavigator](Reference-WindowNavigator) |
| `IViewMappingRegister` | View インターフェイスと Form クラスの紐付け | [ViewMappingRegister](Reference-ViewMappingRegister) |
| **Platform Services** | `IMessageService` 等の UI 抽象化サービス群 | [Platform Services](Reference-Platform-Services) |
| **Logging** | M.E.L. 互換 API の自社抽象、`net40` 対応 | [Logging](Reference-Logging) |
| `ChangeTracker<T>` | 編集 / キャンセル用の変更追跡 | [ChangeTracker](Reference-ChangeTracker) |
| `EventAggregator` | Presenter 間の疎結合 pub/sub | [EventAggregator](Reference-EventAggregator) |
| `PlatformServices` + DI 連携 | サービスの構成・取得 | [Dependency Injection](Reference-DependencyInjection) |

---

## Presenter の 4 つの基底クラス

選択基準だけ示します。詳しい使い分けは [Presenter 基底クラス](Reference-Presenter-Base-Classes) を参照してください。

| シナリオ | 使う基底クラス |
|---------|--------------|
| 通常の Form (ダイアログ・メインウィンドウ) | `WindowPresenterBase<TView>` |
| 入力データを受け取って開く Form | `WindowPresenterBase<TView, TParam>` |
| 親 Form の中に配置する UserControl | `ControlPresenterBase<TView>` |
| 構成情報を持つ UserControl | `ControlPresenterBase<TView, TParam>` |

結果を呼び出し元に返したい場合は、基底 `RequestClose(result, status)` を呼ぶだけです。インターフェイスの実装は不要です。
詳細は [ウィンドウクローズモデル](Concept-Window-Closing-Model) を参照してください。

---

## ViewAction システムの位置づけ

ボタンクリック → Presenter の関数呼び出しを「直接配線」ではなく **宣言的バインド** にするのが ViewAction システムの目的です (WPF の `ICommand` を WinForms に持ち込んだもの)。

- **Presenter は `Button` を知らない**。`Dispatcher.Register(action, handler, canExecute)` でアクションキーとハンドラを結ぶだけ。
- **View は Presenter を知らない**。`ActionBinder.Add(action, button)` でボタンとアクションを結ぶだけ。
- 両者が直接やり取りするのは **`ViewAction` (=不変な識別子)** のみ。
- `CanExecute` の判定結果に応じて、フレームワークがバインド済みコントロールの `Enabled` を自動更新する。

詳細は [ViewAction システム](Reference-ViewAction-System) を参照してください。

---

## サービスの利用方法

Presenter からは、コンストラクタ注入なしで以下のサービスにアクセスできます。これらは `PlatformServices.Default` から自動的に注入されます。

| プロパティ | 型 | 用途 |
|----------|----|----|
| `Messages` | `IMessageService` | メッセージ・確認ダイアログ |
| `Dialogs` | `IDialogProvider` | OpenFile / SaveFile / FolderBrowser |
| `Files` | `IFileService` | ファイル I/O |
| `Logger` | `ILogger` | 構造化ロギング |
| `Navigator` | `IWindowNavigator` | 子ウィンドウの表示 |
| `Platform` | `IPlatformServices` | カスタムサービス取得用のコンテナ |

業務ロジック用サービス (`IUserRepository` 等) は通常通りコンストラクタ注入してください。
構成方法の詳細は [Platform Services](Reference-Platform-Services) と [Dependency Injection](Reference-DependencyInjection) を参照してください。

---

## 横断的な仕組み

業務処理に対して直交する以下の機構が、必要に応じて使えます。

| 仕組み | こんなときに |
|--------|------------|
| [ChangeTracker](Reference-ChangeTracker) | 「保存 / キャンセル」が必要なフォーム編集画面 |
| [EventAggregator](Reference-EventAggregator) | 複数 Presenter が同じイベントに反応する必要があるとき |
| [Logging](Reference-Logging) | 操作ログ・障害解析 |
| ViewAction Middleware | 監査ログ・パフォーマンス計測等の横断的処理を Dispatcher に挟む |

---

## 次のステップ

| 目的 | 読むべきページ |
|------|------------|
| ウィンドウクローズの設計を深掘りしたい | [ウィンドウクローズモデル](Concept-Window-Closing-Model) |
| 17 条の設計ルールを把握したい | [MVP 設計ルール](Design-Rules) |
| ViewAction システムの全 API を知りたい | [ViewAction システム](Reference-ViewAction-System) |
| サービス層の使い方を知りたい | [Platform Services](Reference-Platform-Services) |
| DI コンテナと統合したい | [Dependency Injection](Reference-DependencyInjection) |
