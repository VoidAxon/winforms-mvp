using System.Collections.Generic;
using Kibinago.Samples.AnchoredMessageDemo;
using Kibinago.Samples.AsyncDemo;
using Kibinago.Samples.BulkBindingDemo;
using Kibinago.Samples.CascadeDemo;
using Kibinago.Samples.CheckBoxDemo;
using Kibinago.Samples.MasterDetailDemo;
using Kibinago.Samples.ValidationDemo;
using Kibinago.Samples.ValueBindingDemo;
using Kibinago.Events;
using Kibinago.Samples.ComplexInteractionDemo.Models;
using Kibinago.Samples.ComplexInteractionDemo.OrderManagement;
using Kibinago.Samples.ComplexInteractionDemo.Services;
using Kibinago.Services;
using EventBasedOrders = Kibinago.Samples.ComplexInteractionDemo_EventBased.OrderManagement.OrderManagementPresenter;
using ServiceBasedOrders = Kibinago.Samples.ComplexInteractionDemo_ServiceBased.OrderManagement.OrderManagementPresenter;
using Kibinago.Samples.BindingDemo;
using Kibinago.Samples.BusyDemo;
using Kibinago.Samples.ChangeTrackingDemo;
using Kibinago.Samples.CompositionDemo;
using Kibinago.Samples.FirstErrorDemo;
using Kibinago.Samples.ListDemo;
using Kibinago.Samples.ToDoDemo;

namespace Kibinago.Samples.Shell
{
    /// <summary>The demo ids, shared by the catalog and the view's map of demo controls and windows.</summary>
    public static class DemoIds
    {
        public const string MvpComparison = "mvp-comparison";
        public const string ToDo = "todo";
        public const string CheckBox = "checkbox";
        public const string BulkBinding = "bulk-binding";
        public const string ValueBinding = "value-binding";
        public const string ExplicitEvent = "explicit-event";
        public const string ExplicitParameters = "explicit-parameters";
        public const string Async = "async";
        public const string Busy = "busy";
        public const string DebounceThrottle = "debounce-throttle";
        public const string Navigator = "navigator";
        public const string WindowClosing = "window-closing";
        public const string LegacyPopup = "legacy-popup";
        public const string Mdi = "mdi";
        public const string MessageBox = "messagebox";
        public const string Toast = "toast";
        public const string AnchoredMessage = "anchored-message";
        public const string Validation = "validation";
        public const string FirstError = "first-error";
        public const string Outpatient = "outpatient";
        public const string ProfileEdit = "profile-edit";
        public const string Sort = "sort";
        public const string Move = "move";
        public const string Contacts = "contacts";
        public const string Reservation = "reservation";
        public const string MasterDetail = "master-detail";
        public const string Cascade = "cascade";
        public const string ServiceBased = "service-based";
        public const string EventBased = "event-based";
        public const string Customization = "customization";
        public const string Email = "email";
    }

    /// <summary>
    /// The sidebar of the sample: every category with its explanation, wiki pages, demos and sources.
    /// The view creates a demo's control or window from its id (ShellForm); everything else is here.
    /// </summary>
    public static class SampleCatalog
    {
        public const string CustomizeCategoryId = "customize";

        public static IReadOnlyList<SampleCategory> Create() => new[]
        {
            new SampleCategory("intro", "はじめに", "このサンプルの見方",
                "Kibinago は WinForms で MVP (Model-View-Presenter) を書くためのフレームワークです。\r\n\r\n" +
                "左の大項目を選ぶと、右のタブにその機能の説明・デモ・コードが並びます。\r\n" +
                "・説明: 何ができるか、どのページを読めばよいか\r\n" +
                "・デモ: タブの中で動くもの (UserControl + 子 Presenter) と、ボタンで別ウィンドウを開くもの (ウィンドウそのものの機能)\r\n" +
                "・コード: 選んだデモのソース (ビルド時に埋め込んだもの)\r\n\r\n" +
                "大項目を切り替えると、タブの中のデモはリセットされます (子 Presenter は破棄され、戻ると作り直されます)。\r\n" +
                "このメイン画面自体も MVP です: ShellPresenter がデモの Presenter を AttachChild で子として持ちます。",
                new[]
                {
                    new WikiLink("はじめての Kibinago", "Getting-Started"),
                    new WikiLink("MVP パターン", "Concept-MVP-Pattern"),
                    new WikiLink("設計ルール", "Design-Rules"),
                },
                new[]
                {
                    SampleDemo.Window(DemoIds.MvpComparison, "MVP の比較",
                        "Passive View と Supervising Controller の同じ画面を並べて比べます。Kibinago は Supervising Controller です。",
                        "MVPComparisonDemo/MVPComparisonLauncher.cs",
                        "MVPComparisonDemo/PassiveView/IUserEditorView.cs", "MVPComparisonDemo/PassiveView/UserEditorPresenter.cs",
                        "MVPComparisonDemo/SupervisingController/IUserEditorView.cs", "MVPComparisonDemo/SupervisingController/UserEditorPresenter.cs"),
                },
                new[] { "Shell/IShellView.cs", "Shell/ShellPresenter.cs", "Shell/SampleCatalog.cs", "Shell/ShellForm.cs" }),

            new SampleCategory("viewaction", "ViewAction", "ボタン → Presenter の処理、CanExecute",
                "コントロールの操作を ViewAction (キー) で Presenter の処理につなぎます。\r\n" +
                "Presenter は Dispatcher.Register(キー, 処理, canExecute) を登録するだけで、ボタンは知りません。\r\n" +
                "View は ViewActionBinder でコントロールとキーを対応づけ、ActionBinder プロパティで渡します。\r\n" +
                "canExecute に合わせてボタンの有効・無効が自動で切り替わり、非同期の処理中は二度押しできません。",
                new[] { new WikiLink("ViewAction", "Reference-ViewAction-System") },
                new[]
                {
                    SampleDemo.Embedded(DemoIds.ToDo, "ToDo",
                        "CanExecute と、状態が変わったときの RaiseCanExecuteChanged。",
                        (a, v) => a.Attach(new ToDoDemoPresenter(), (IToDoView)v),
                        "ToDoDemo/ToDoDemoPresenter.cs", "ToDoDemo/IToDoView.cs", "ToDoDemo/ToDoDemoControl.cs"),
                    SampleDemo.Embedded(DemoIds.CheckBox, "CheckBox / RadioButton",
                        "CheckedChanged をアクションにする設定画面。",
                        (a, v) => a.Attach(new SettingsDemoPresenter(), (ISettingsView)v),
                        "CheckBoxDemo/SettingsDemoPresenter.cs", "CheckBoxDemo/ISettingsView.cs", "CheckBoxDemo/SettingsDemoControl.cs"),
                    SampleDemo.Embedded(DemoIds.BulkBinding, "一括バインド",
                        "AddRange / AddByTag で多数のラジオボタンをまとめて対応づけます。",
                        (a, v) => a.Attach(new SurveyDemoPresenter(), (ISurveyView)v),
                        "BulkBindingDemo/SurveyDemoPresenter.cs", "BulkBindingDemo/ISurveyView.cs", "BulkBindingDemo/SurveyDemoControl.cs"),
                    SampleDemo.Embedded(DemoIds.ValueBinding, "値をペイロードに",
                        "Add(control, c => 値) で CheckBox・NumericUpDown・日付の値をそのまま処理に渡します。",
                        (a, v) => a.Attach(new ValueBindingPresenter(), (IValueBindingView)v),
                        "ValueBindingDemo/ValueBindingDemo.cs"),
                    SampleDemo.Embedded(DemoIds.ExplicitEvent, "明示イベント方式",
                        "ActionBinder => null、View.ActionRequest。古い Form を移行するときの書き方。",
                        (a, v) => a.Attach(new ExplicitEventDemoPresenter(ServiceLocator.Current.Resolve<IMessageService>()), (IExplicitEventDemoView)v),
                        "ViewActionExplicitEventExample.cs"),
                    SampleDemo.Embedded(DemoIds.ExplicitParameters, "明示イベント + パラメーター",
                        "値のバインドを ActionRequest で受け取ります。",
                        (a, v) => a.Attach(new MixedParametersDemoPresenter(ServiceLocator.Current.Resolve<IMessageService>()), (IMixedParametersDemoView)v),
                        "ViewActionExplicitWithParametersExample.cs"),
                }),

            new SampleCategory("async", "非同期・処理中", "async の処理、間引き",
                "async の処理は Dispatcher.Register(キー, async ct => ...) で登録します。実行中はボタンが無効になり、" +
                "画面を閉じるとトークン (Lifetime) がキャンセルされます。\r\n" +
                "時間のかかる処理の間は using (ShowBusy(\"…\")) { … }: ウィンドウが待ちカーソルになり入力を受け付けません " +
                "(このサンプルは Program.cs の mvp.Busy.UseOverlay() でオーバーレイも出します)。タブの中のデモなので、メイン画面全体が処理中になります。\r\n" +
                "Debouncer / Throttler は連続するイベント (入力・スクロール) を間引きます。",
                new[]
                {
                    new WikiLink("非同期の処理", "HowTo-Handle-Async-Operations"),
                    new WikiLink("処理中の表示 (ShowBusy)", "HowTo-Show-Busy"),
                    new WikiLink("レート制限", "Reference-RateLimiting"),
                },
                new[]
                {
                    SampleDemo.Embedded(DemoIds.Busy, "処理中 (ShowBusy)",
                        "同期処理・await・ワーカースレッドからの ShowBusy。",
                        (a, v) => a.Attach(new BusyDemoPresenter(), (IBusyDemoView)v),
                        "BusyDemo/BusyDemoPresenter.cs", "BusyDemo/IBusyDemoView.cs", "BusyDemo/BusyDemoControl.cs", "Program.cs"),
                    SampleDemo.Embedded(DemoIds.Async, "非同期の処理",
                        "実行中の自動無効化、キャンセル、進捗。",
                        (a, v) => a.Attach(new AsyncDemoPresenter(), (IAsyncDemoView)v),
                        "AsyncDemo/AsyncDemoPresenter.cs", "AsyncDemo/IAsyncDemoView.cs", "AsyncDemo/AsyncDemoControl.cs"),
                    SampleDemo.ViewOnly(DemoIds.DebounceThrottle, "Debounce / Throttle",
                        "生のイベント回数と、間引いた後の回数を比べます。",
                        "DebounceThrottleDemo/DebounceThrottleDemoControl.cs"),
                }),

            new SampleCategory("windows", "ウィンドウ", "開く・結果・閉じる",
                "ウィンドウは Presenter から Navigator で開きます (モーダルは結果を返し、モードレスはキーで 1 つに)。\r\n" +
                "閉じるかどうかは Presenter の CanClose / CanCloseAsync が決めます。✕ でも Alt+F4 でも RequestClose でも同じ門を通ります。\r\n" +
                "置き場所を自分で決めるコンテナ (MDI、ドッキングのペイン、タブ) には PrepareWindow: 表示以外はすべて済ませた View を返します。\r\n" +
                "ここのデモはウィンドウそのものの機能なので、ボタンで別ウィンドウを開きます。",
                new[]
                {
                    new WikiLink("WindowNavigator", "Reference-WindowNavigator"),
                    new WikiLink("ウィンドウを閉じる仕組み", "Concept-Window-Closing-Model"),
                    new WikiLink("閉じるときの確認", "HowTo-Handle-Window-Closing"),
                },
                new[]
                {
                    SampleDemo.Window(DemoIds.Navigator, "Navigator",
                        "モーダル・モードレス・パラメーター・結果・キーで 1 つだけ開くウィンドウ。",
                        "NavigatorDemo/NavigatorDemoPresenter.cs", "NavigatorDemo/INavigatorDemoView.cs", "NavigatorDemo/NavigatorDemoForm.cs",
                        "NavigatorDemo/Dialogs/InputDialogPresenter.cs", "NavigatorDemo/Windows/SingletonWindowPresenter.cs"),
                    SampleDemo.Window(DemoIds.WindowClosing, "閉じる確認",
                        "未保存の変更があるときの「保存しますか」。CanClose / CanCloseAsync。",
                        "WindowClosingDemo/WindowClosingDemoPresenter.cs", "WindowClosingDemo/IWindowClosingDemoView.cs", "WindowClosingDemo/WindowClosingDemoForm.cs"),
                    SampleDemo.Window(DemoIds.Mdi, "PrepareWindow と MDI",
                        "Navigator で用意だけしたウィンドウを MDI の子として表示。子の ShowBusy はその子だけを処理中にします。",
                        "MdiDemo/MdiHostPresenter.cs", "MdiDemo/IMdiHostView.cs", "MdiDemo/MdiHostForm.cs",
                        "MdiDemo/DocumentPresenter.cs", "MdiDemo/IDocumentView.cs", "MdiDemo/DocumentForm.cs"),
                    SampleDemo.Window(DemoIds.LegacyPopup, "MVP でない Form を開く",
                        "既存の Form を意味のある View メソッドから開き、InteractionResult で返します。",
                        "LegacyPopupDemo/LegacyPopupDemoPresenter.cs", "LegacyPopupDemo/ILegacyPopupDemoView.cs", "LegacyPopupDemo/LegacyPopupDemoForm.cs", "LegacyPopupDemo/LegacyCustomerForm.cs"),
                }),

            new SampleCategory("dialogs", "ダイアログ・通知", "メッセージボックス、トースト",
                "Presenter からは Messages (ShowInfo / ConfirmYesNo …) と Toasts で知らせます。WinForms の型は使いません。\r\n" +
                "位置に意味がある表示 (ボタンのそば) は View の仕事: AnchoredToast / AnchoredMessageBox を View が使います。",
                new[]
                {
                    new WikiLink("プラットフォームサービス", "Reference-Platform-Services"),
                    new WikiLink("トースト", "Reference-Toast-Notifications"),
                },
                new[]
                {
                    SampleDemo.ViewOnly(DemoIds.MessageBox, "メッセージボックスの位置",
                        "標準の MessageBox を指定した位置に出します。",
                        "MessageBoxDemo/MessageBoxDemoControl.cs"),
                    SampleDemo.ViewOnly(DemoIds.Toast, "トースト",
                        "種類・位置・スタイル・自作の描画。OpenForms に出ない層付きポップアップ。",
                        "ToastDemo/ToastDemoControl.cs"),
                    SampleDemo.Embedded(DemoIds.AnchoredMessage, "アンカー表示",
                        "既定は隅のトースト、位置が大事なときは意味のある View メソッドで。",
                        (a, v) => a.Attach(new AnchoredMessageDemoPresenter(), (IAnchoredMessageDemoView)v),
                        "AnchoredMessageDemo/AnchoredMessageDemoPresenter.cs", "AnchoredMessageDemo/IAnchoredMessageDemoView.cs", "AnchoredMessageDemo/AnchoredMessageDemoControl.cs"),
                }),

            new SampleCategory("validation", "入力検証", "DataAnnotations、エラー表示",
                "Presenter の Validation に、モデルの DataAnnotations と Presenter のルールをまとめます。\r\n" +
                "エラーは、フィールドを離れた後か、送信を試みた後にだけ表示します。\r\n" +
                "表示は 2 通り: メッセージだけ (送信に失敗したら最初のエラーを出して、その欄へフォーカス。WinForms の業務画面でよくある形) と、" +
                "メッセージ + 欄の横のアイコン (View が IValidatableView を実装)。エラーの順番はモデルのプロパティの宣言順です。\r\n" +
                "送信ボタンはエラーがあっても無効にしません (まだ触っていない欄のエラーを見せるため)。",
                new[] { new WikiLink("入力検証", "HowTo-Validate-Form-Input") },
                new[]
                {
                    SampleDemo.Embedded(DemoIds.FirstError, "最初のエラーへ (FirstError)",
                        "メッセージだけの形: 送信に失敗したら最初のエラーを出し、その欄へフォーカス。",
                        (a, v) => a.Attach(new IntakePresenter(), (IIntakeView)v),
                        "FirstErrorDemo/IntakePresenter.cs", "FirstErrorDemo/IntakeModel.cs", "FirstErrorDemo/IIntakeView.cs", "FirstErrorDemo/IntakeControl.cs"),
                    SampleDemo.Embedded(DemoIds.Validation, "入力検証 (アイコン付き)",
                        "DataAnnotations、ErrorProvider、送信時にまとめて表示。",
                        (a, v) => a.Attach(new ValidationDemoPresenter(), (IValidationDemoView)v),
                        "ValidationDemo/ValidationDemoPresenter.cs", "ValidationDemo/RegistrationModel.cs", "ValidationDemo/IValidationDemoView.cs", "ValidationDemo/ValidationDemoControl.cs"),
                }),

            new SampleCategory("binding", "バインド", "モデルとコントロールの双方向",
                "Presenter が入力モデル (INotifyPropertyChanged、NotifyingObject) を持ち、View がコントロールをそれにバインドします。\r\n" +
                "Presenter はモデルを読み書きするだけで、コントロールには触りません。View 側は 1 コントロール 1 行: textBox.Bind(model, x => x.Name)。\r\n" +
                "式はコンパイラーが確認します (名前の変更に追従、型違いはビルドエラー)。日付の null はチェックボックス、ラジオボタンは BindRadioGroup。",
                new[] { new WikiLink("データバインド", "Reference-Data-Binding") },
                new[]
                {
                    SampleDemo.Embedded(DemoIds.Outpatient, "双方向バインド",
                        "入力するとモデルが変わり、Presenter がモデルを変えるとコントロールが変わります。",
                        (a, v) => a.Attach(new OutpatientPresenter(), (IOutpatientView)v),
                        "BindingDemo/OutpatientPresenter.cs", "BindingDemo/IOutpatientView.cs", "BindingDemo/OutpatientControl.cs", "BindingDemo/OutpatientInput.cs"),
                }),

            new SampleCategory("tracking", "変更追跡", "未保存の変更があるか",
                "TrackChanges(model) はモデルのスナップショットを取り、IsChanged で内容を比べます (値を元に戻せば「変更なし」)。\r\n" +
                "検出だけの部品です: 元に戻す機能やイベントはありません。保存したら AcceptChanges()、元に戻すときは読み直して新しく追跡します。\r\n" +
                "使い方は 2 通り: 1 つのオブジェクトをその場で編集する (このデモ) か、毎回 View から新しいモデルを作って IsChangedWith(model) で比べるか。\r\n" +
                "画面ごとに無視するメンバーは .Ignore(x => x.UpdatedAt)。ウィンドウでは同じ IsChanged を CanClose の「保存しますか」に使います (ウィンドウ → 閉じる確認)。",
                new[] { new WikiLink("ChangeTracker", "Reference-ChangeTracker") },
                new[]
                {
                    SampleDemo.Embedded(DemoIds.ProfileEdit, "変更の検出",
                        "編集すると「未保存の変更」、元の値に戻すと消えます。UpdatedAt は無視。",
                        (a, v) => a.Attach(new ProfileEditPresenter(), (IProfileEditView)v),
                        "ChangeTrackingDemo/ProfileEditPresenter.cs", "ChangeTrackingDemo/IProfileEditView.cs", "ChangeTrackingDemo/ProfileEditControl.cs", "ChangeTrackingDemo/PatientProfile.cs"),
                }),

            new SampleCategory("list", "一覧", "並べ替え、行の移動",
                "オブジェクトの一覧にバインドしたグリッドは、SortableBindingList<T> なら見出しのクリックで並びます " +
                "(安定ソート、型が混在しても落ちない、ReplaceAll で並べ替えを保ったまま読み直し)。\r\n" +
                "一覧は Presenter が持ち、View はグリッドを 1 回バインドするだけです。業務の既定の並び (ApplySort) と絞り込み (ReplaceAll + LINQ) は Presenter に書きます。\r\n" +
                "上へ・下へ・最上へ・最下へは rows.MoveUp(row) など。SortableBindingList なら現在行が動かした行についていきます。",
                new[] { new WikiLink("データバインド (一覧・グリッド)", "Reference-Data-Binding") },
                new[]
                {
                    SampleDemo.Embedded(DemoIds.Sort, "並べ替え",
                        "見出しクリック、Shift+クリックで複数列、病棟で絞り込み、KeepSorted。",
                        (a, v) => a.Attach(new SortDemoPresenter(), (ISortDemoView)v),
                        "ListDemo/SortDemoPresenter.cs", "ListDemo/ISortDemoView.cs", "ListDemo/SortDemoControl.cs", "ListDemo/PatientRow.cs"),
                    SampleDemo.Embedded(DemoIds.Move, "行の移動",
                        "上へ・下へ・最上へ・最下へ。変わった表示順だけを保存します。",
                        (a, v) => a.Attach(new MoveDemoPresenter(), (IMoveDemoView)v),
                        "ListDemo/MoveDemoPresenter.cs", "ListDemo/IMoveDemoView.cs", "ListDemo/MoveDemoControl.cs", "ListDemo/PhraseRow.cs"),
                }),

            new SampleCategory("composition", "画面の組み立て", "マスター/詳細、連動する選択",
                "1 つの画面を複数の Presenter で組み立てます。親 View のインターフェイスが子 View のインターフェイスを公開し、" +
                "親 Presenter が AttachChild で子 Presenter をつなぎます (子は親のサービスを使い、親と一緒に破棄されます)。\r\n" +
                "行やタブのように実行中に増減するパネルは、AttachChild の戻り値 (ハンドル) を子 View ごとに持ち、外すときはハンドルを破棄してから View がコントロールを外します。\r\n" +
                "「選択」は Selection<T> で共有し、上の選択が変わったら下を読み直す連動は Cascade.Bind (親が 2 つなら Cascade.Combine) です。",
                new[]
                {
                    new WikiLink("マスター/詳細", "HowTo-Implement-Master-Detail"),
                    new WikiLink("連動する選択", "HowTo-Handle-Cascading-Selection"),
                },
                new[]
                {
                    SampleDemo.Embedded(DemoIds.Contacts, "動的なパネル (AttachChild)",
                        "連絡先の行を追加・削除。行ごとに子 Presenter が付き、削除でハンドルを破棄します。",
                        (a, v) => a.Attach(new ContactsDemoPresenter(), (IContactsDemoView)v),
                        "CompositionDemo/ContactsDemoPresenter.cs", "CompositionDemo/IContactsDemoView.cs", "CompositionDemo/ContactsDemoControl.cs",
                        "CompositionDemo/ContactRowPresenter.cs", "CompositionDemo/IContactRowView.cs", "CompositionDemo/ContactRowControl.cs", "CompositionDemo/ContactRow.cs"),
                    SampleDemo.Embedded(DemoIds.Reservation, "3 段の連動 (Cascade.Combine)",
                        "診療科 → 医師、医師 + 曜日 → 予約枠。上を変えると下が空になり読み直されます。",
                        (a, v) => a.Attach(new ReservationDemoPresenter(), (IReservationDemoView)v),
                        "CompositionDemo/ReservationDemoPresenter.cs", "CompositionDemo/IReservationDemoView.cs", "CompositionDemo/ReservationDemoControl.cs", "CompositionDemo/ReservationModels.cs"),
                    SampleDemo.Embedded(DemoIds.MasterDetail, "マスター/詳細",
                        "顧客を選ぶと注文が切り替わります。",
                        (a, v) => a.Attach(new MasterDetailPresenter(), (IMasterDetailView)v),
                        "MasterDetailDemo/MasterDetailPresenter.cs", "MasterDetailDemo/IMasterDetailView.cs", "MasterDetailDemo/MasterDetailControl.cs"),
                    SampleDemo.Embedded(DemoIds.Cascade, "連動する選択 (N 段)",
                        "Selection と Cascade.Bind。親 Presenter が 3 つのリストの子 Presenter を AttachChild でつなぎます。",
                        (a, v) => a.Attach(new CascadeDemoPresenter(), (ICascadeDemoView)v),
                        "CascadeDemo/CascadeDemoPresenter.cs", "CascadeDemo/CascadePresenters.cs", "CascadeDemo/ICascadeDemoView.cs",
                        "CascadeDemo/CascadeDemoControl.cs", "CascadeDemo/SelectListView.cs", "CascadeDemo/CascadeModels.cs"),
                }),

            new SampleCategory("communication", "Presenter 間の連携", "共有サービス、EventAggregator",
                "別々の Presenter の間で知らせ合う方法の比較です。\r\n" +
                "同じ画面のパネル同士なら、まず共有モデル (親 Presenter が持つ) を考えてください。" +
                "画面やモジュールをまたぐときは、イベントを持つサービスか IEventAggregator を使います。",
                new[]
                {
                    new WikiLink("Presenter 間の連携", "HowTo-Communicate-Between-Presenters"),
                    new WikiLink("EventAggregator", "Reference-EventAggregator"),
                },
                new[]
                {
                    SampleDemo.Embedded(DemoIds.ServiceBased, "共有サービス",
                        "すべての Presenter が同じサービスに依存し、サービスのイベントで更新します。",
                        (a, v) => a.Attach(new ServiceBasedOrders(new OrderManagementService(), SampleProducts.All()), (IOrderManagementView)v),
                        "ComplexInteractionDemo_ServiceBased/OrderManagement/OrderManagementPresenter.cs", "ComplexInteractionDemo/OrderManagement/IOrderManagementView.cs",
                        "ComplexInteractionDemo_ServiceBased/OrderSummary/OrderSummaryPresenter.cs",
                        "ComplexInteractionDemo_ServiceBased/ProductSelector/ProductSelectorPresenter.cs"),
                    SampleDemo.Embedded(DemoIds.EventBased, "EventAggregator",
                        "メッセージの発行・購読で、お互いを知らない Presenter が連携します。",
                        (a, v) => a.Attach(new EventBasedOrders(ServiceLocator.Current.ResolveRequired<IEventAggregator>(), SampleProducts.All()), (IOrderManagementView)v),
                        "ComplexInteractionDemo_EventBased/OrderManagement/OrderManagementPresenter.cs", "ComplexInteractionDemo/OrderManagement/IOrderManagementView.cs",
                        "ComplexInteractionDemo_EventBased/OrderSummary/OrderSummaryPresenter.cs",
                        "ComplexInteractionDemo_EventBased/ProductSelector/ProductSelectorPresenter.cs",
                        "ComplexInteractionDemo_EventBased/Messages/ProductAddedMessage.cs"),
                }),

            new SampleCategory("startup", "起動・DI", "起動時の設定、View の対応づけ",
                "アプリは起動時に 1 回だけ MvpApplication.Configure を呼びます (フレームワークの設定と、業務サービスの登録)。\r\n" +
                "View の対応づけ (IViewMappingRegister.RegisterFromAssembly) で、Navigator がインターフェイスから Form を作れるようになります。\r\n" +
                "メイン画面は自分で作って Presenter を Connect し、MvpApplication.Run で表示します。それ以外のウィンドウは Presenter から Navigator で開きます。\r\n" +
                "M.E.DI を使う場合は services.AddKibinago(...)。複数プロジェクト構成の例は samples/MultiProjectDemo.* にあります。",
                new[]
                {
                    new WikiLink("フレームワークのカスタマイズ", "HowTo-Customize-Framework"),
                    new WikiLink("DI", "Reference-DependencyInjection"),
                    new WikiLink("View の対応づけ", "Reference-ViewMappingRegister"),
                },
                new SampleDemo[0],
                new[] { "Program.cs" }),

            new SampleCategory(CustomizeCategoryId, "カスタマイズ", "ダイアログ・トーストを差し替える",
                "設定は起動時の 1 か所だけです (mvp.Messages.UseDialogs、mvp.Toasts.UseRenderer など)。\r\n" +
                "動いているアプリの設定は変えないので、このデモはカスタム設定で別のプロセスとして起動します " +
                "(Kibinago.Samples.exe --profile custom)。そちらでは、すべてのメッセージボックスとトーストが差し替わります。",
                new[] { new WikiLink("フレームワークのカスタマイズ", "HowTo-Customize-Framework") },
                new[]
                {
                    SampleDemo.Window(DemoIds.Customization, "カスタム設定",
                        "会社のダイアログ、ブランドのトースト、変更追跡の比較方法。",
                        "CustomizationDemo/CustomizationDemoSetup.cs", "CustomizationDemo/CompanyStyleDialogBackend.cs",
                        "CustomizationDemo/CustomizationDemoPresenter.cs", "CustomizationDemo/CustomizationDemoForm.cs", "Program.cs"),
                }),

            new SampleCategory("testing", "テスト・ツール", "Presenter のテスト、アナライザー、テンプレート",
                "Presenter は Form なしでテストできます: モックの View を作り、PresenterTestHost.Start(services, presenter, view) で本物の初期化を走らせ、" +
                "ボタンの代わりに view.Binder.Dispatch(アクション) で操作します (Kibinago.Testing パッケージ)。\r\n" +
                "TestServices.CreateFakes() はフレームワークのサービスをすべて偽物にします (fakes.Messages.Calls で出したメッセージを確認、確認ダイアログの答えも指定できます)。\r\n" +
                "テストのために Presenter の公開範囲を広げることはしません。\r\n\r\n" +
                "アナライザー (Kibinago.Core に同梱、すべて警告): MVP001/002 名前、MVP003/004/014 Presenter と View インターフェイスに WinForms の型を入れない、" +
                "MVP006/007/008/013/016 公開範囲とインターフェイス、MVP017 EventAggregator の購読を捨てない、MVP018 Debouncer / Throttler を破棄する。\r\n\r\n" +
                "テンプレート: dotnet new kibinago (アプリ)、mvp-window (--edit で編集ダイアログ)、mvp-control、kibinago-tests。",
                new[]
                {
                    new WikiLink("Presenter のテスト", "HowTo-Test-A-Presenter"),
                    new WikiLink("設計ルールとアナライザー", "Design-Rules"),
                    new WikiLink("テンプレート", "HowTo-Use-Templates"),
                },
                new SampleDemo[0],
                new[]
                {
                    "Tests/ToDoDemoPresenterTests.cs", "Tests/MockToDoView.cs", "Tests/MoveDemoPresenterTests.cs",
                    "Tests/IntakePresenterTests.cs", "Tests/ReservationDemoPresenterTests.cs", "Tests/ContactsDemoPresenterTests.cs",
                }),

            new SampleCategory("app", "総合アプリ", "メールアプリ",
                "ここまでの機能を組み合わせた小さなアプリです: Navigator、閉じる確認、変更追跡、入力検証。",
                new[] { new WikiLink("大きな編集画面の作り方", "HowTo-Build-Large-Edit-Screen") },
                new[]
                {
                    SampleDemo.Window(DemoIds.Email, "メール",
                        "一覧・作成・保存確認。",
                        "EmailDemo/MainEmailPresenter.cs", "EmailDemo/ComposeEmailPresenter.cs", "EmailDemo/IMainEmailView.cs", "EmailDemo/IComposeEmailView.cs",
                        "EmailDemo/ComposeEmailParameters.cs", "EmailDemo/Services/IEmailRepository.cs"),
                }),
        };
    }
}
