# Presenter の責務と肥大化の防止

このページは、MVP で最も起こりやすい失敗 — **肥大化した Presenter (Fat Presenter)** — をどう防ぐかを、Presenter の責務という観点から解説します。

MVP パターンそのものの考え方は [MVP パターンとは](Concept-MVP-Pattern)、ルールの一覧は [MVP 設計ルール](Design-Rules) を参照してください。本ページはその土台の上に立ち、「**Presenter には何を書き、何を書かないか**」という線引きに焦点を絞ります。

> 📌 **前提**: 本ページは DDD ではなく、**サービス層 + Transaction Script** スタイルの素朴な構成を前提にしています。ドメインモデル・集約・ドメインサービスといった枠組みは仮定しません。長く生き続ける業務システム (LOB) に対する、地に足のついた分担の指針を目指します。

---

## 一言で言うと

> **Presenter は「痩せた監督」である。指揮するだけで、自分では演じない。**

MVP の 3 つの役割を演劇に例えると分かりやすくなります。

| 役割 | 例え | 仕事 |
|------|------|------|
| **View** | 舞台 | 描画と入力の受け取り |
| **Presenter** | **監督** | 表現ロジックと協調 |
| **Model** | 脚本/道具 | データと業務ルール (後述) |

監督は俳優 (View) に何をするか指示し、脚本/データ (Model) から素材を取り、芝居全体のテンポを協調させますが、**自分は画面に映らないし、演技もしません**。この一点を覚えておくと、以降の準則はすべてここから導けます。

### よくある誤解: Presenter = アプリケーション層 ?

レイヤの観点では、**View も Presenter も表現層に属します**。Presenter は表現層のうち「協調/ロジック」を担う部分、View は「描画」を担う部分です。アプリケーション層 (あれば) は **Presenter の背後にある、呼び出す相手** であって、Presenter 自身ではありません。

WinForms は十数年生き、業務ロジックが際限なく積み上がっていく LOB システムです。その Presenter は「監督」から「何でも自分でやる人」へ滑り落ちやすい。本ページの大半は、これを防ぐための話です。

---

## Presenter がやる 4 種類の仕事だけ

Presenter に書いてよいのは、次の 4 種類だけです。

1. **View から入力を集める** — `IXxxView` インターフェイス越しにユーザーが何を入力したかを読む。
2. **サービスを呼ぶ** — 集めた入力を業務サービス/データアクセスへ渡して処理させる。
3. **結果/エラーを View に返す** — 戻り値に応じて、View に成功表示かエラー表示かを指示する。
4. **データと表示の間のマッピング/整形** — サービスが返したデータを View が求める形に整える。

これ以外のロジックは、基本的に Presenter に書くべきではありません。

### 入力イベントの扱い

低レベルの Mouse / Keyboard イベントを **そのまま Presenter に流し込まない** こと。View が生の入力を捕まえ、**意味レベル/意図レベルのイベント** に翻訳してから外へ出します。Presenter に届くのは「意図 + すでに抽出済みのデータ」であって、`MouseEventArgs` / `KeyEventArgs` ではありません。

```csharp
// The View translates raw input into intent
searchBox.KeyDown += (s, e) =>
{
    if (e.KeyCode == Keys.Enter) SearchRequested?.Invoke(searchBox.Text); // intent + text
};

grid.CellMouseEnter += (s, e) => HighlightRow(e.RowIndex); // pure UI: stays in the View
```

理由は 2 つ。

- **Presenter は UI フレームワーク型に依存できない。** `Keys` や `KeyEventArgs` が Presenter に入った瞬間、WinForms に縛られ、単体テストも難しくなります。
- **`IXxxView` は「意図」を語るべきで、「コントロールの配管」を語るべきではない。** `event Action<string> SearchRequested` は意図、`event KeyEventHandler SearchBoxKeyDown` は漏洩です。

> 💡 このフレームワークでは、この「生イベント → 意図」の翻訳と、それに伴う有効/無効制御を [ViewAction システム](Reference-ViewAction-System) が引き受けます。手書きの `event` でも実現できますが、ViewAction を使うと `canExecute` による活性制御まで一箇所に集約できます。
>
> **例外**: キャンバス/描画/図形編集系のアプリでは、マウス座標そのものがデータです。それでも View の境界で**スクリーン座標をドメイン座標に翻訳してから**渡します — ピクセルは View に留め、意味だけが Presenter に入ります。

---

## Presenter がやらないこと

- ❌ **業務ルール** (「メールアドレスが重複していないか」の判定、割引計算、状態遷移の可否)
- ❌ **データアクセス** (SQL、コネクション、ORM 呼び出し)
- ❌ **複数ステップの業務フローの編成 / トランザクション管理**
- ❌ **UI フレームワーク型の保持** (イベント引数、コントロール型)
- ❌ **View と重複する「影の状態」の保持**

### 判定の物差し: どこに書くべきか

あるロジックを Presenter に置くべきか迷ったら、この 1 問で判定します。

> **この画面から離れても、それは成立するか?**

| 答え | 例 | 置き場所 |
|------|----|---------|
| 成立する | 「メールアドレスは重複してはいけない」 — どの入口から登録しても守るべき | **Presenter ではない** (サービスへ) |
| この画面限定 | 「このフォームが正しく埋まっているか」 (必須、形式) | **境界 (Presenter/View) に置いてよい** |

---

## このアーキテクチャでの「Model」とは

MVP の「Model」は曖昧な大袋です。サービス層スタイルでは、Presenter の背後の「Model」は具体的に次の 3 つを指します。

| 構成要素 | 性質 | 例 |
|---------|------|----|
| **データクラス (DTO / POCO)** | 振る舞いを持たない純粋なデータ。層から層へ流れる「通貨」。貧血で十分。 | `Customer`、`OrderDto` |
| **業務サービス層** | 業務操作 (Transaction Script)。業務ルールと編成はここ。 | `ICustomerService` |
| **データアクセス** | repository / DAO。DB コネクションと SQL をインターフェイスの裏に封じる。 | `ICustomerRepository` |

したがって Presenter と Model の関係は、一文で言い切れます。

> **Presenter は「サービスインターフェイス」と「データクラス」だけと付き合い、データをサービスと View の間で運び/翻訳する。DB には絶対に触れず、業務ルールも置かない。**

---

## 業務サービスをいつ導入するか

重要な認識: **「DB コネクション操作」をサービスインターフェイスに抽出するのは必要だが、肥大化の防止には不十分** です。SQL を repository に追い出しても、検証・業務判断・フロー編成が Presenter に残れば、それはまだ肥大化しています — SQL を持たないだけです。

Presenter を本当に痩せさせるのは、**業務操作そのものがサービスに移ること**です。監督は「この顧客を登録しておけ」と一声かけるべきで、登録フローを自分で一歩ずつ走るべきではありません。

### 簡単 vs 複雑: 専用の業務サービスは要るか?

| ケース | 構成 |
|--------|------|
| **簡単** (純粋な CRUD、本物の業務ルールなし) | Presenter → データアクセスインターフェイス の 2 層で十分。転送するだけの業務サービスを無理に挟まない (無意味な間接)。 |
| **複雑 / 本物の業務ロジックあり** | 独立した業務サービス (`ICustomerService`) を抽出する。 |

判断基準は「コード量の大小」ではなく、**「データアクセスを超える業務ロジックがあるか」** です。フォーム全体は単純でも、重要なルール (重複チェック、計算、状態遷移) を 1 つ抱えていることがあります。そのルールは、周りが単純でも Presenter 以外の家を持つべきです。

Presenter はすでにインターフェイス越しに repository を呼んでいるので、3 層へ昇格する (業務サービスを 1 つ挟む) のは局所的で安価な変更です。**まず簡単に始め、シグナルを見張り、その時が来たら抽出する**のが正解です。

業務サービスを抽出すべきシグナル:

- 2 つ目の入口が同じ操作を使い始めた (再利用)
- `OnXxx` ハンドラが長い分岐を持ち始め、もはや入力検証だけではない
- 「データを見ないと判定できない」ルールが出てきた
- このロジックを UI から切り離して単体テストする価値がある (金額・資格など)
- 1 操作が複数の repository をまたぐ / トランザクションを管理する必要がある

---

## 入力検証 vs 業務検証

| 種類 | 関心事 | 置き場所 | 例 |
|------|--------|---------|----|
| **入力レベル / 形式検証** | このフォームが正しく埋まっているか | Presenter / View 境界 | 必須、メール形式らしさ、数値範囲 |
| **業務検証 + 業務ルール** | 入口を変えても守るべきか | サービス層 | メール重複、与信の充足、状態遷移の可否 |

原則: **Presenter に業務ルールの「正本」を置かない。** 即時の入力ヒント (赤字表示など) は出してよいが、裁定権はサービスにあります。

> 検証パターンの詳細 (フィールド/クロスフィールド/パターン) は [フォーム入力を検証する](HowTo-Validate-Form-Input) を参照してください。

---

## 肥大化した Presenter を防ぐ (核心)

肥大化した Presenter は MVP で最も多い失敗パターンです。それが肥大化するのは、**性質の異なる 3 つのものを同時に背負う**からです。

| 背負うもの | 性質 | どうするか |
|-----------|------|-----------|
| **① 表現の協調** | 本職 | Presenter に留める |
| **② アプリ/ユースケースの編成** (サービス呼び出し、フロー連結、トランザクション) | 境界の仕事 | 許容するが抑制的に。複雑ならサービス/ユースケースへ抽出 |
| **③ 業務ルール** | 漏れ込んだもの | 必ず追い出す (サービスへ) |

WinForms の Presenter が特に肥大化しやすいのは、この 3 つが一塊に溶けやすいからです。「監督は指揮するだけ、演じない」を貫いて、一つずつ正しい場所へ戻します。

- 表現の協調 → Presenter に留める。
- 業務ルール → サービスに任せ、監督は「これを計算しておけ」と命じるだけ。自分で計算しない。
- 複数ステップのフロー / データ取得 / トランザクション → 複雑ならサービスへ抽出。小規模なら Presenter に残してもよいが、ここが最も肥大化しやすい場所だと自覚する。

### 健康度シグナル

次に気づいたら、編成/ルールを外へ追い出すべき合図です。

- Presenter にライフサイクル以外の `public` メソッドが生え始めた
- `OnSubmit` の中に長い `if/else` の判定が挟まっている
- 「この Presenter、アプリケーション層みたいだな」と感じ始めた

> **Presenter をアプリケーション層のように感じるほど、その部分を本物のサービス層へ抽出すべきだという合図です。**

非同期処理 (DB/ネットワーク待ち、ビジー表示、完了後の再活性化、キャンセル) も、現代の Presenter が肥大化する典型的な現場です。扱い方は [非同期処理を扱う](HowTo-Handle-Async-Operations) を参照してください。

---

## 状態はどこに持つか

状態の「正本」は **常に 1 つ** に保ちます。影の副本を持つと、2 つの状態はいずれ食い違います。どこを正本にするかは、[MVP パターンとは](Concept-MVP-Pattern) で解説している **Supervising Controller ↔ Passive View のスペクトラム** 上の選択であり、フォームごとに倒す先を選んでよい性質のものです。

| そのフォームの性質 | 正本の置き場所 | 寄せる先 |
|------------------|--------------|---------|
| 単純なフォーム (データバインドで足りる) | View のコントロール。Presenter は `IXxxView` プロパティ越しに読む。 | Supervising Controller 寄り |
| 本物の作業状態を持つ画面 (編集中、多段、ダーティ判定、Undo) | Presenter が現在の作業データオブジェクトを private フィールドで保持し、View を純粋な描画器として片方向に押す。 | Passive View 寄り |

> ⚠️ **トレードオフ**: コントロールを正本にすると記述量は減りますが、書式化/解析の往復 (例: まだバインドされていないコントロール、表示用フォーマットの丸め) で状態がずれることがあり、Presenter 単体テストでの観測性も下がります。テスト容易性やダーティ判定が重要な画面ほど、Model を正本にする (Passive View 寄りの) 方が安全です。ダーティ判定には [ChangeTracker](Reference-ChangeTracker) を使えます。
>
> いずれの場合も、**View と Presenter の両方に同じデータの可変コピーを持たせない** こと。これが「影の状態」であり、食い違いの原因です。

---

## Presenter 間のやり取り

「関係」と「渡すものの性質」で機構を選びます。一律にしないこと。要点だけ示します (詳細と完全なコード例は [Presenter 間の通信方法](HowTo-Communicate-Between-Presenters))。

| 関係 | 渡すもの | 機構 |
|------|---------|------|
| 親 → 子 | 一回的コマンド (窄い例外) | **まず共有 Model / イベントを検討**。本物の一回的コマンドのときだけ子 Presenter のメソッドを直接呼ぶ (handler は `private`、既定 `internal`、縫い目が要れば接口)。共有・観測される状態なら命令でなく下の Store へ |
| 子 → 親 | 通知 | 子がイベント/コールバックを公開し、親が購読する (**局所イベント**。グローバルバスを通さない) |
| 兄弟 (所有関係なし) — **共有する状態** | 双方が見続ける 1 つのデータ | 変更通知付きの共有 **Store** (`event` / `INotifyPropertyChanged`)。両 Presenter に同じインスタンスを注入 |
| 兄弟 — **一過性の信号** | 「エクスポートした」「再読み込みして」 | [EventAggregator](Reference-EventAggregator) または共通の親で協調 |

> ⚠️ **状態で信号を偽装しない**。`bool` フラグを立てて即座にリセットする、カウンタを自増させて観察者を発火させる — こういうやり方は醜く、順序バグの温床です。両 Presenter が観察する「状態」なら Store、転瞬で消える「信号」なら EventAggregator。両者は競合ではなく、性質の異なる 2 つのやり取りを分担します。
>
> 共有 Store を「何でも入れるグローバルゴミ箱」にしないこと。A が B を突くためだけにフィールドを足したなら、それは状態に偽装された信号です — イベントで送るべきです。

---

## 命名: 全部「Model」と呼ばない

サービス層スタイルでは性質の全く異なるオブジェクトが何種類も出てきます。すべて `XxxModel` と呼ぶと区別がつきません。接尾辞は「MVP のどの文字か」ではなく、**「オブジェクトの性質/責務」** を表すべきです。

| 性質 | 推奨接尾辞 | 例 |
|------|-----------|----|
| 受動的なデータ担体 | `Dto` / 裸の名詞 / `Info` / `Record` | `CustomerDto`、`Customer` |
| ある View 用に整形した表示形状 | `PresentationModel` / `Vm` | `CustomerEditVm` |
| 状態を持ち、観測可能で、共有ソースになる | `Store` / `State` / `StateContainer` | `SelectionStore`、`SelectionState` |

`Store` は Flux/Redux、`State`/`StateContainer` は Blazor のコンポーネント間通信に由来し、いずれも「状態を持つ + 通知を出す」を正確に伝えます。

> 💡 実用的には、「やり取りに使うもの」だけを `Store`/`State` にリネームすれば、名前の衝突は即座に解消します。データクラスは元のままでも混同しなくなります。

---

## public 面の最小化とライフサイクル

Presenter の `public` 面は、既定で **コンストラクタ / `Initialize` / `Dispose`** だけに保ちます。その他は原則 `private`。

これは「`public` メソッドを 1 つも許さない」という意味ではなく、**「Presenter を、外部から命令的に突けるサービスにしない」** という意味です。Presenter は一度組み立て (構築 + `Initialize` で購読)、以降はイベントに応答して自律的に動き、破棄時 (`Dispose`) に購読を解除する — そういうスタイルへの約束です。

- 親子協調に必要なメソッドは例外として許容されますが、抑制的に。
- 出力ポートとしての**イベント/Observable を公開すること**と、**コマンドメソッドを公開すること**は別物です。前者は通知の出口で最小化の精神と矛盾しませんが、後者がこのルールの真の対象です。
- **`Dispose` で必ず購読解除すること。** 購読解除の忘れによるメモリリークは、MVP の実戦で最も多いバグです。

> ルールの詳細は [MVP 設計ルール](Design-Rules) の Rule 16 (Presenter メソッドの可視性) と Rule 17 (Presenter イベントの可視性) を参照してください。Rule 16・17 はアナライザ化されておらず、コードレビューのチェックリストで担保します。

---

## 完全なサンプル: 読み込み-編集-保存

```csharp
// ── Data class: pure data, no behavior ──
public class Customer { public int Id; public string Name; public string Email; }

// ── Data access interface: DB hidden behind it ──
public interface ICustomerRepository
{
    Customer GetById(int id);
    void Save(Customer c);
    bool ExistsByEmail(string email);
}

// ── Business service: business rules + orchestration (Transaction Script) ──
public interface ICustomerService
{
    Customer Load(int id);
    Result Save(Customer c);
}

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repo;
    public CustomerService(ICustomerRepository repo) => _repo = repo;

    public Customer Load(int id) => _repo.GetById(id);

    public Result Save(Customer c)
    {
        if (string.IsNullOrWhiteSpace(c.Name))           // business validation
            return Result.Fail("氏名は必須です");
        if (c.Id == 0 && _repo.ExistsByEmail(c.Email))   // rule that needs data to decide
            return Result.Fail("既に登録済みです");
        _repo.Save(c);                                   // orchestration + persistence
        return Result.Ok();
    }
}

// ── IView: intent-level, no controls leaked ──
public interface ICustomerEditView
{
    string Name { get; set; }
    string Email { get; set; }
    event Action SaveRequested;
    void ShowError(string message);
    void ShowSaved();
}

// ── Presenter: pure coordination; public surface is only ctor / Initialize / Dispose ──
public class CustomerEditPresenter
{
    private readonly ICustomerEditView _view;
    private readonly ICustomerService _service;
    private int _customerId;                       // only the minimal state the work needs

    public CustomerEditPresenter(ICustomerEditView view, ICustomerService service)
    {
        _view = view;
        _service = service;
    }

    public void Initialize(int customerId)
    {
        _customerId = customerId;
        var c = _service.Load(customerId);          // ② call the service to fetch data
        _view.Name = c.Name;                        // ④ map onto the View
        _view.Email = c.Email;
        _view.SaveRequested += OnSave;              // wire up
    }

    public void Dispose() => _view.SaveRequested -= OnSave;  // unsubscribe to avoid leaks

    private void OnSave()
    {
        var c = new Customer                         // ① collect input from the View
        {
            Id = _customerId,
            Name = _view.Name,
            Email = _view.Email
        };
        var result = _service.Save(c);               // ② delegate to the service
        if (result.IsOk) _view.ShowSaved();     // ③ report back
        else _view.ShowError(result.Error);
    }
}
```

Presenter には SQL も、業務ルールも、WinForms の入力型もありません — それは `IView` とサービス層の間の協調者そのものです。

> 上のサンプルは仕組みを示すための最小形です。実フレームワークでは、`Initialize`/`Dispose` での手書き購読を [ViewAction システム](Reference-ViewAction-System) が、`Result` 型を [`InteractionResult<T>`](HowTo-Handle-Errors) が引き受け、購読解除忘れを構造的に防ぎます。

---

## チェックリスト

- [ ] Presenter には 4 種類の仕事だけ: 入力収集・サービス呼び出し・結果の返却・表示マッピング。
- [ ] Presenter に業務ルール・SQL・WinForms 入力型を含めない。
- [ ] 業務ルールはサービスに。簡単なら データアクセスのみ抽出、複雑/本物のルールがあれば業務サービスを抽出。
- [ ] 入力検証は境界、業務検証はサービス。Presenter にルールの正本を残さない。
- [ ] 状態は 1 つだけ: 単純フォームは View コントロール、作業状態は Presenter の private フィールド。影の副本を作らない。
- [ ] 生の Mouse/Keyboard を Presenter に入れない。View が意味イベントに翻訳する。
- [ ] Presenter 間: 親子は直接メソッド呼び出し + 上向きイベント、兄弟の共有状態は通知付き Store、一過性の信号は EventAggregator/親で協調。
- [ ] 命名は性質で: データは `Dto`、共有状態は `Store`/`State`。
- [ ] Presenter の public 面はコンストラクタ/`Initialize`/`Dispose` のみ。`Dispose` で購読解除。
- [ ] Presenter とサービスに対応する単体テストがある。

---

## 関連ページ

| 目的 | ページ |
|------|--------|
| MVP パターンそのものと Supervising/Passive スペクトラム | [MVP パターンとは](Concept-MVP-Pattern) |
| 全 17 条の設計ルール (Rule 16/17 = public 面の最小化) | [MVP 設計ルール](Design-Rules) |
| Presenter 間の通信 (親子/兄弟/Store/EventAggregator) | [Presenter 間の通信方法](HowTo-Communicate-Between-Presenters) |
| 入力/業務検証の具体パターン | [フォーム入力を検証する](HowTo-Validate-Form-Input) |
| 非同期処理での協調 (ビジー表示・再活性化・キャンセル) | [非同期処理を扱う](HowTo-Handle-Async-Operations) |
| Presenter の単体テスト | [Presenter をテストする](HowTo-Test-A-Presenter) |
| エラー処理 (`InteractionResult<T>`) | [エラー処理戦略](HowTo-Handle-Errors) |
