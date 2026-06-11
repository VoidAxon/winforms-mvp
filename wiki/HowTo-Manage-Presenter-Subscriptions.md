# HowTo: Presenter の購読ライフサイクルを管理する

Presenter は生存期間中に、**解放が必要なものを少しずつ溜め込みます**。`IEventAggregator.Subscribe(...)` が返す購読トークン、`Cascade.Bind(...)` が返す `IDisposable`、View イベントへの `+=` 購読……。

統一的に管理しないと、典型的にはこう書くことになります — `IDisposable` をフィールドに持ち、`Cleanup` で 1 つずつ解放する:

```csharp
private IDisposable _bind;
// ...
_bind = Cascade.Bind(...);
// ...
protected override void Cleanup()
{
    if (_bind != null) _bind.Dispose();
    View.SelectionChanged -= OnUserSelected;
}
```

このパターンには明確な弱点があります。**ライフサイクル管理が作成地点から遠い**(作成は `OnViewAttached`、解放は `Cleanup`)、**解放漏れ・購読解除漏れが起きやすい**、漏れた場合はぶら下がったイベントハンドラ — つまりメモリリークです。実際、リファクタ前の `CascadeDemo` のサンプルコードはまさにこの様板で埋まっていました。

本ページでは、フレームワーク組み込みの小さな道具で、**作成したその行でライフサイクルを宣言し、基底クラスに自動解放させる**方法を説明します。`Cleanup` のオーバーライドは不要になります。

---

## 道具:`CompositeDisposable` + `DisposeWith`

`WinformsMVP.Common` にあります。外部依存ゼロ・net40 対応:

| 型 | 役割 |
|----|------|
| `CompositeDisposable` | `IDisposable` の入れ物。自身が Dispose されると、メンバーを**登録の逆順**で全解放 |
| `Disposable.Create(Action)` | `+=` / `-=` 形式のイベント購読を `IDisposable` 化するラッパー(高々 1 回だけ実行) |
| `.DisposeWith(bag)` 拡張メソッド | disposable を bag に登録し、**そのまま同じインスタンスを返す**。作成行にチェーンできる |

鍵は `DisposeWith` です。「これはいつまで生きるか」が、遠く離れた `Cleanup` ではなく**作成したその行**に書かれます。

## 基底クラスの `Disposables` バッグ

`PresenterBase` は `protected CompositeDisposable Disposables` を持っています:

- **遅延生成** — 購読を持たない Presenter は何も割り当てません。儀式は不要です。
- **自動解放** — Presenter の `Dispose()` が `Cleanup()` を呼んだ**直後**に、フレームワークがバッグを掃除します。解放処理は `Cleanup` ではなく `Dispose` 側にあるため、`Cleanup` をオーバーライドして base を呼び忘れても漏れません。
- **再構築なし** — このフレームワークに detach / re-attach のモデルはありません(Presenter と View は生存期間を共にし、`Cleanup` は `Dispose` から一度だけ実行されます)。バッグは 1 つ、解放は 1 回です。
- **解放後の `Add` は安全** — 解放済みのバッグに追加すると、その場で即 Dispose されます。黙ってリークすることはありません。

## 使い方

購読を作る行に `.DisposeWith(Disposables)` を付けるだけです。`Cleanup` は書きません:

```csharp
protected override void OnViewAttached()
{
    // Cascade binding — released automatically when the presenter is disposed
    Cascade.Bind(_category, _subCategory, cat =>
        View.SubCategoryItems = cat == null ? Empty<SubCategory>() : _repo.GetSubCategories(cat.Id))
        .DisposeWith(Disposables);

    // EventAggregator subscription — same one-liner
    Events.Subscribe<OrderShipped>(OnOrderShipped)
        .DisposeWith(Disposables);
}
// No _bind field, no null guard, no Cleanup override.
```

### View イベント(`+=` / `-=`)も同じ流儀で

イベント購読自体は `IDisposable` ではないので、`Disposable.Create` で包みます:

```csharp
protected override void OnViewAttached()
{
    EventHandler handler = (s, e) => _category.Select(View.SelectedCategory);
    View.CategorySelected += handler;
    Disposable.Create(() => View.CategorySelected -= handler).DisposeWith(Disposables);
}
```

detach 時にバッグが解放されると同時に、すべての `-=` が自動実行されます。解除漏れは構造的に起きません。

### Before / After(実例:CascadeDemo)

リファクタ前の `CascadePresenters.cs` は、Presenter 3 つがそれぞれ `_bind` フィールド + null ガード + `Cleanup` オーバーライド + 手動 `-=` を抱えていました。リファクタ後は上記の 2 パターンだけになり、**`Cleanup` オーバーライドは 3 つとも削除**されています。現物は `samples/WinformsMVP.Samples/CascadeDemo/CascadePresenters.cs` を見てください。

---

## 使わなくていい場面(境界)

これは**汎用インフラであって、義務ではありません**:

- **解放すべき購読がない Presenter** → `Disposables` に触れる必要はありません。遅延生成なのでコストもゼロです。
- **単一 Presenter 内で完結する固定段数のカスケード** → 素朴なイベントハンドラ実装には管理すべき disposable 自体がなく、この道具の出番もありません。`Cascade` + Store + バッグは、**複数 Presenter の疎結合・深い/可変段数・共有状態が必要**な場面のための装備です。
- **fluent builder の先回り追加はしない**(`Cascade.From().Into().Build()` のような)。3 段なら `Bind(...).DisposeWith(...)` を 2 行書けば十分明瞭です。

一言でまとめると:**購読があれば使う、なければ使わない。**

## 注意点

- **逆順解放**:構築と逆の順序で解放されます(「後に作った依存を先に壊す」の直感どおり)。
- **冪等**:`CompositeDisposable` も `Disposable.Create` も解放は高々 1 回。多重 `Dispose` は安全です。
- **スレッド**:WinForms では購読も解放も UI スレッド上なので、ロックは持ちません(設計判断)。バックグラウンドスレッドから同じバッグに触らないでください。
- **Presenter 全寿命より短い購読**:途中で解除したい購読は、戻り値を自分で持って個別に `Dispose` してください。バッグは「Presenter と共に死ぬもの」専用です。
- **net40**:`List<T>`・ジェネリック拡張メソッド・`Action` のみで構成されており、追加要件はありません。

## Rx / ReactiveUI との関係

これは Rx 世界の定石をそのまま持ち込んだものです。`System.Reactive` の `CompositeDisposable`、ReactiveUI の `.DisposeWith(d)` / `WhenActivated` と同じ発想 — **作成地点でライフサイクルを宣言し、寿命に紐づいたバッグに後始末を委ねる**。

フレームワークは依存ゼロを維持するため `System.Reactive` を参照せず、数十行を自前実装しています。型名・メソッド名は意図的に Rx と同じにしてあるので、ReactiveUI 経験者は一目で分かります。万一 `System.Reactive` を同居させる場合は、型名衝突を using エイリアスで解決してください。

## 関連ページ

- [HowTo-Handle-Cascading-Selection](HowTo-Handle-Cascading-Selection) — `Cascade.Bind` が返す disposable はこの仕組みで管理します
- [HowTo-Communicate-Between-Presenters](HowTo-Communicate-Between-Presenters) — `IEventAggregator.Subscribe` の購読トークンも同様
- [Reference-Presenter-Base-Classes](Reference-Presenter-Base-Classes) — ライフサイクル(`OnViewAttached` / `Cleanup` / `Dispose`)と `Disposables` の位置づけ
