using System;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Services;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Services
{
    /// <summary>
    /// RegisterFromAssembly / RegisterFromNamespace: a Form is registered under its application window
    /// view interface — the most derived one; capabilities and control views are never chosen.
    /// </summary>
    public class ViewMappingScanTests
    {
        public interface IBaseEditView : IWindowView { }
        public interface IOrderEditView : IBaseEditView, IValidationErrorBindable { }
        public interface IOtherView : IWindowView { }
        public interface IPanelView : IControlView { }

        private class OrderEditForm : Form, IOrderEditView
        {
            public IViewActionBinder ActionBinder => null;
            public Kibinago.Validation.IValidationErrorDisplay ValidationErrors => null;
        }

        private class TwoWindowsForm : Form, IBaseEditView, IOtherView
        {
            public IViewActionBinder ActionBinder => null;
        }

        private class PanelHostForm : Form, IPanelView   // a Form as a control view: not a window to scan
        {
            public IViewActionBinder ActionBinder => null;
        }

        private class PlainForm : Form { }

        private class GenericForm<T> : Form, IOtherView
        {
            public IViewActionBinder ActionBinder => null;
        }

        [Fact]
        public void TheMostDerivedWindowViewInterface_IsChosen_NotItsBaseOrACapability()
        {
            Assert.Equal(typeof(IOrderEditView), ViewMappingRegisterExtensions.SelectWindowContract(typeof(OrderEditForm)));
        }

        [Fact]
        public void FormsThatAreNotMvpWindows_AreSkipped()
        {
            Assert.Null(ViewMappingRegisterExtensions.SelectWindowContract(typeof(PlainForm)));
            Assert.Null(ViewMappingRegisterExtensions.SelectWindowContract(typeof(PanelHostForm)));
        }

        [Fact]
        public void SeveralUnrelatedWindowViewInterfaces_AreAnError_ThatSaysWhatToDo()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ViewMappingRegisterExtensions.SelectWindowContract(typeof(TwoWindowsForm)));
            Assert.Contains("IBaseEditView", ex.Message);
            Assert.Contains("IOtherView", ex.Message);
            Assert.Contains("Register", ex.Message);
        }

        [Fact]
        public void TheScan_RegistersEachWindowOnce_AndSkipsOpenGenericForms()
        {
            var register = new ViewMappingRegister();
            var count = ViewMappingRegisterExtensions.RegisterForms(register,
                new[] { typeof(OrderEditForm), typeof(PanelHostForm), typeof(PlainForm), typeof(GenericForm<>) }, allowOverride: false);

            Assert.Equal(1, count);
            Assert.True(register.IsRegistered(typeof(IOrderEditView)));
            Assert.False(register.IsRegistered(typeof(IBaseEditView)));
            Assert.False(register.IsRegistered(typeof(IPanelView)));
        }

        [Fact]
        public void AnExistingMapping_IsKept_WithoutAllowOverride()
        {
            var register = new ViewMappingRegister();
            register.Register<IOrderEditView, OrderEditForm>();

            var count = ViewMappingRegisterExtensions.RegisterForms(register, new[] { typeof(OrderEditForm) }, allowOverride: false);

            Assert.Equal(0, count);
            Assert.True(register.IsRegistered(typeof(IOrderEditView)));
        }
    }
}
