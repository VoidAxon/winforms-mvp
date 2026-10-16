using Xunit;
using static WinformsMVP.Analyzers.Tests.AnalyzerHarness;

namespace WinformsMVP.Analyzers.Tests
{
    /// <summary>
    /// One positive and the relevant negative cases per diagnostic. Negative cases include the
    /// false-positive shapes of the old name-matching analyzers: domain types named like controls
    /// (Label, Panel, ApplicationForm), framework look-alikes in other namespaces, and classes that
    /// are named *Presenter / I*View without actually deriving from the framework types.
    /// </summary>
    public class AnalyzerTests
    {
        private const string Usings = @"
using System;
using System.Collections.Generic;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.Presenters;
";

        // Same, plus WinForms — for snippets that intentionally reference UI types.
        private const string UiUsings = Usings + "using System.Windows.Forms;\n";

        // ------------------------------------------------------------------ MVP001

        [Fact]
        public void MVP001_ViewInterfaceWithoutViewSuffix_Reports()
        {
            Assert.Equal(new[] { "MVP001:IOrderEditor" },
                IdsWithText(Usings + "public interface IOrderEditor : IWindowView { }"));
        }

        [Fact]
        public void MVP001_ViewInterfaceWithSuffix_NoDiagnostic()
        {
            Assert.Empty(Ids(Usings + "public interface IOrderEditorView : IWindowView { }"));
        }

        [Fact]
        public void MVP001_InterfaceDerivingLookalikeIViewBase_NoDiagnostic()
        {
            Assert.Empty(Ids(@"
namespace Other { public interface IViewBase { } }
public interface IOrderEditor : Other.IViewBase { }"));
        }

        // ------------------------------------------------------------------ MVP002

        [Fact]
        public void MVP002_PresenterWithoutSuffix_Reports()
        {
            Assert.Equal(new[] { "MVP002:OrderCoordinator" },
                IdsWithText(Usings + @"
public interface IOrderView : IWindowView { }
public class OrderCoordinator : WindowPresenterBase<IOrderView> { }"));
        }

        [Fact]
        public void MVP002_TransitivePresenterWithSuffix_NoDiagnostic()
        {
            Assert.Empty(Ids(Usings + @"
public interface IOrderView : IWindowView { }
public abstract class AppPresenter<T> : WindowPresenterBase<T> where T : IWindowView { }
public class OrderPresenter : AppPresenter<IOrderView> { }"));
        }

        [Fact]
        public void MVP002_LookalikePresenterBaseInOtherNamespace_NoDiagnostic()
        {
            Assert.Empty(Ids(@"
namespace Other { public abstract class PresenterBase<T> { } }
public class Helper : Other.PresenterBase<int> { }"));
        }

        // ------------------------------------------------------------------ MVP003

        [Fact]
        public void MVP003_PresenterCreatesControl_Reports()
        {
            Assert.Equal(new[] { "MVP003:Button" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnSave() { object b = new Button(); }
}"));
        }

        [Fact]
        public void MVP003_TargetTypedNew_Reports()
        {
            // The local's declared type is reported by MVP014, the creation by MVP003.
            Assert.Equal(new[] { "MVP014:Button", "MVP003:new()" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnSave() { Button b = new(); }
}"));
        }

        [Fact]
        public void MVP003_PresenterCreatesCommonDialog_Reports()
        {
            Assert.Equal(new[] { "MVP003:OpenFileDialog" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnOpen() { object d = new OpenFileDialog(); }
}"));
        }

        [Fact]
        public void MVP003_PresenterNotNamedPresenter_StillReports()
        {
            Assert.Equal(new[] { "MVP002", "MVP003" },
                Ids(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderScreen : WindowPresenterBase<IOrderView>
{
    private void OnSave() { object b = new Button(); }
}"));
        }

        [Fact]
        public void MVP003_DomainTypeNamedLikeControl_NoDiagnostic()
        {
            Assert.Empty(Ids(Usings + @"
namespace Shipping { public class Label { } public class Panel { } }
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnPrint() { var l = new Shipping.Label(); var p = new Shipping.Panel(); }
}"));
        }

        [Fact]
        public void MVP003_ClassNamedPresenterButNotDerived_NoDiagnostic()
        {
            Assert.Empty(Ids(UiUsings + @"
public class ToolbarPresenter
{
    private void Build() { object b = new Button(); }
}"));
        }

        // ------------------------------------------------------------------ MVP004

        [Fact]
        public void MVP004_ViewInterfaceExposesControl_Reports()
        {
            Assert.Equal(new[] { "MVP004:SaveButton" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { Button SaveButton { get; } }"));
        }

        [Fact]
        public void MVP004_ViewInterfaceExposesFormOrControl_Reports()
        {
            // Form / Control used to be exempt.
            Assert.Equal(new[] { "MVP004", "MVP004" },
                Ids(UiUsings + @"
public interface IOrderView : IWindowView { Form OwnerForm { get; } Control Host { get; } }"));
        }

        [Fact]
        public void MVP004_ViewInterfaceDialogResultAndClosingEvent_Reports()
        {
            Assert.Equal(new[] { "MVP004:ShowConfirm", "MVP004:Closing" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView
{
    DialogResult ShowConfirm();
    event EventHandler<FormClosingEventArgs> Closing;
}"));
        }

        [Fact]
        public void MVP004_GenericAndArrayWrappers_Report()
        {
            Assert.Equal(new[] { "MVP004:Labels", "MVP004:_buttons", "MVP004:host" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { IList<Label> Labels { get; } }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private List<Button> _buttons;
    public OrderPresenter(Control[] host) { }
}"));
        }

        [Fact]
        public void MVP004_PresenterFieldOfFormType_Reports()
        {
            Assert.Equal(new[] { "MVP004:_owner" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private Form _owner;
}"));
        }

        [Fact]
        public void MVP004_DomainTypesNamedLikeControls_NoDiagnostic()
        {
            Assert.Empty(Ids(Usings + @"
namespace Shipping { public class Label { } public class Panel { } public class ApplicationForm { } }
public interface IOrderView : IWindowView
{
    Shipping.Label ShippingLabel { get; }
    List<Shipping.Panel> Panels { get; }
}
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private Shipping.ApplicationForm _form;
}"));
        }

        [Fact]
        public void MVP004_InterfaceNamedViewButNotAView_NoDiagnostic()
        {
            Assert.Empty(Ids(UiUsings + @"
public interface IPreviewView { Button Button { get; } }"));
        }

        // ------------------------------------------------------------------ MVP006

        [Fact]
        public void MVP006_PublicPresenterMethodReturningValue_Reports()
        {
            Assert.Equal(new[] { "MVP006:Count" },
                IdsWithText(Usings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    public int Count() => 0;
}"));
        }

        [Fact]
        public void MVP006_NonPresenterOrPrivate_NoDiagnostic()
        {
            Assert.Empty(Ids(Usings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private int Count() => 0;
}
public class PricingPresenter { public int Total() => 0; }"));
        }

        // ------------------------------------------------------------------ MVP007

        [Fact]
        public void MVP007_PresenterFieldOfConcreteForm_Reports()
        {
            // Only MVP007 — MVP004 must not double-report the same concrete view.
            Assert.Equal(new[] { "MVP007:_form", "MVP007:_forms" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderForm : Form, IOrderView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private OrderForm _form;
    private List<OrderForm> _forms;
}"));
        }

        [Fact]
        public void MVP007_PresenterTypedOnConcreteForm_Reports()
        {
            Assert.Equal(new[] { "MVP007:OrderPresenter" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderForm : Form, IOrderView { }
public class OrderPresenter : WindowPresenterBase<OrderForm> { }"));
        }

        [Fact]
        public void MVP007_DomainTypeEndingInForm_NoDiagnostic()
        {
            Assert.Empty(Ids(Usings + @"
public class ApplicationForm { public string Applicant; }
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private ApplicationForm _application;
}"));
        }

        [Fact]
        public void MVP007_NonWinFormsTestDoubleAsView_NoDiagnostic()
        {
            Assert.Empty(Ids(Usings + @"
public interface IOrderView : IWindowView { }
public class FakeOrderView : IOrderView { }
public class OrderPresenter : WindowPresenterBase<FakeOrderView>
{
    private FakeOrderView _fake;
}"));
        }

        // ------------------------------------------------------------------ MVP008

        [Fact]
        public void MVP008_PublicFormMethodNotInViewInterface_Reports()
        {
            Assert.Equal(new[] { "MVP008:Refresh" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { void ShowTotal(decimal total); }
public class OrderForm : Form, IOrderView
{
    public void ShowTotal(decimal total) { }
    public void Refresh() { }
}"));
        }

        [Fact]
        public void MVP008_ClassImplementingNonViewInterfaceNamedView_NoDiagnostic()
        {
            Assert.Empty(Ids(@"
public interface IReportView { void Render(); }
public class ReportRenderer : IReportView
{
    public void Render() { }
    public void Extra() { }
}"));
        }

        // ------------------------------------------------------------------ MVP013

        [Fact]
        public void MVP013_ViewMethodNamedAfterControl_Reports()
        {
            Assert.Equal(new[] { "MVP013:AddTreeViewNode" },
                IdsWithText(Usings + @"
public interface IOrderView : IWindowView { void AddTreeViewNode(string text); }"));
        }

        [Fact]
        public void MVP013_NonViewInterface_NoDiagnostic()
        {
            Assert.Empty(Ids(@"
public interface ITreeBuilderView { void AddTreeViewNode(string text); }"));
        }

        // ------------------------------------------------------------------ MVP014

        [Fact]
        public void MVP014_StaticImportedMessageBoxShow_Reports()
        {
            Assert.Equal(new[] { "MVP014:Show", "MVP014:OK" },
                IdsWithText(Usings + @"
using static System.Windows.Forms.MessageBox;
using static System.Windows.Forms.DialogResult;
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private bool OnSave() => Show(""Saved!"") == OK;
}"));
        }

        [Fact]
        public void MVP004_ViewInterfaceExposingConcreteForm_Reports()
        {
            // MVP007 only covers presenters, so the concrete view on an interface is MVP004's.
            Assert.Equal(new[] { "MVP004:ChildForm" },
                IdsWithText(UiUsings + @"
public interface IChildView : IWindowView { }
public class ChildForm : Form, IChildView { }
public interface IOrderView : IWindowView { IChildView Child { get; } ChildForm ChildForm { get; } }"));
        }

        [Fact]
        public void MVP014_MessageBoxShow_Reports()
        {
            Assert.Equal(new[] { "MVP014:MessageBox" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnSave() { MessageBox.Show(""Saved!""); }
}"));
        }

        [Fact]
        public void MVP014_FullyQualifiedMessageBox_ReportsOnce()
        {
            Assert.Equal(new[] { "MVP014:MessageBox" },
                IdsWithText(Usings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnSave() { System.Windows.Forms.MessageBox.Show(""Saved!""); }
}"));
        }

        [Fact]
        // 'var host = View as Control' reports only the explicit Control, not the 'var' as well.
        public void MVP014_DialogResultCastsAndTypeof_Report()
        {
            Assert.Equal(
                new[] { "MVP014:DialogResult", "MVP014:Form", "MVP014:Control", "MVP014:Form" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnClose(object answer)
    {
        if ((DialogResult)answer == default) { }
        ((Form)(object)View).ToString();
        var host = View as Control;
        var t = typeof(Form);
    }
}"));
        }

        [Fact]
        public void MVP014_VarOfWinFormsTypeFromService_Reports()
        {
            Assert.Equal(new[] { "MVP014:var" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public interface IUiFactory { List<Button> CreateButtons(); }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnBuild(IUiFactory factory) { var buttons = factory.CreateButtons(); }
}"));
        }

        [Fact]
        public void MVP014_NestedHelperInsidePresenter_Reports()
        {
            Assert.Equal(new[] { "MVP014:MessageBox" },
                IdsWithText(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private class Notifier { public void Notify() { MessageBox.Show(""x""); } }
}"));
        }

        [Fact]
        public void MVP014_SignatureAndCreation_NotDuplicated()
        {
            // Field type -> MVP004, creation -> MVP003; MVP014 stays silent for both.
            Assert.Equal(new[] { "MVP004", "MVP003" },
                Ids(UiUsings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private Button _button = new Button();
}"));
        }

        [Fact]
        public void MVP014_DomainMessageBoxAndDocCref_NoDiagnostic()
        {
            Assert.Empty(Ids(Usings + @"
namespace Domain { public static class MessageBox { public static void Show(string s) { } } }
public interface IOrderView : IWindowView { }
/// <summary>Unlike a <see cref=""System.Windows.Forms.Form""/>, this has no UI.</summary>
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnSave() { Domain.MessageBox.Show(""Saved!""); }
}"));
        }

        [Fact]
        public void MVP014_WinFormsInViewImplementation_NoDiagnostic()
        {
            Assert.Empty(Ids(UiUsings + @"
public interface IOrderView : IWindowView { void ConfirmDelete(); }
public class OrderForm : Form, IOrderView
{
    public void ConfirmDelete() { MessageBox.Show(""Delete?""); }
}"));
        }

        // ------------------------------------------------------------------ MVP016

        [Fact]
        public void MVP016_PublicEventOnPresenter_Reports()
        {
            Assert.Equal(new[] { "MVP016:IsDirtyChanged" },
                IdsWithText(Usings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    public event EventHandler IsDirtyChanged;
}"));
        }

        [Fact]
        public void MVP016_NonPublicEventOrNonPresenter_NoDiagnostic()
        {
            Assert.Empty(Ids(Usings + @"
public interface IOrderView : IWindowView { }
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    internal event EventHandler Saved;
    private event EventHandler Internal;
}
public class EventsPresenter { public event EventHandler Changed; }"));
        }
    }
}
