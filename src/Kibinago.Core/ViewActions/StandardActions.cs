using System;

namespace Kibinago.ViewActions
{
    /// <summary>
    /// Ready-made keys for the common verbs (Save, Cancel, Delete, Refresh, Ok, …), so a screen does not
    /// declare them itself. Each key is qualified like a screen's own — <c>"Standard." + member name</c>
    /// (<c>StandardActions.Save</c> is <c>"Standard.Save"</c>) — so no two keys share a name and logs and
    /// middleware show where a key comes from. Keys are registered per presenter, so two screens both
    /// using <see cref="Save"/> do not collide.
    /// </summary>
    /// <remarks>
    /// A screen's own actions go in its own class with a qualified factory:
    /// <code>
    /// public static class OrderEditActions
    /// {
    ///     private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("OrderEdit");
    ///
    ///     public static readonly ViewAction Approve = Factory.Create(nameof(Approve));   // "OrderEdit.Approve"
    /// }
    ///
    /// Dispatcher.Register(StandardActions.Save, OnSave);          // a common verb
    /// Dispatcher.Register(OrderEditActions.Approve, OnApprove);   // this screen's own
    /// </code>
    /// </remarks>
    public static class StandardActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Standard");

        // ==================== Common Dialog Actions ====================

        /// <summary>OK</summary>
        public static readonly ViewAction Ok = Factory.Create(nameof(Ok));

        /// <summary>Cancel</summary>
        public static readonly ViewAction Cancel = Factory.Create(nameof(Cancel));

        /// <summary>Yes</summary>
        public static readonly ViewAction Yes = Factory.Create(nameof(Yes));

        /// <summary>No</summary>
        public static readonly ViewAction No = Factory.Create(nameof(No));

        /// <summary>Apply</summary>
        public static readonly ViewAction Apply = Factory.Create(nameof(Apply));

        /// <summary>Close</summary>
        public static readonly ViewAction Close = Factory.Create(nameof(Close));

        /// <summary>Retry</summary>
        public static readonly ViewAction Retry = Factory.Create(nameof(Retry));

        /// <summary>Ignore</summary>
        public static readonly ViewAction Ignore = Factory.Create(nameof(Ignore));

        /// <summary>Abort</summary>
        public static readonly ViewAction Abort = Factory.Create(nameof(Abort));

        // ==================== CRUD Operations ====================

        /// <summary>Save</summary>
        public static readonly ViewAction Save = Factory.Create(nameof(Save));

        /// <summary>Add/New</summary>
        public static readonly ViewAction Add = Factory.Create(nameof(Add));

        /// <summary>Edit/Modify</summary>
        public static readonly ViewAction Edit = Factory.Create(nameof(Edit));

        /// <summary>Delete</summary>
        public static readonly ViewAction Delete = Factory.Create(nameof(Delete));

        /// <summary>Remove (similar to Delete, but lighter semantics)</summary>
        public static readonly ViewAction Remove = Factory.Create(nameof(Remove));

        /// <summary>Create</summary>
        public static readonly ViewAction Create = Factory.Create(nameof(Create));

        /// <summary>Update</summary>
        public static readonly ViewAction Update = Factory.Create(nameof(Update));

        /// <summary>Refresh</summary>
        public static readonly ViewAction Refresh = Factory.Create(nameof(Refresh));

        /// <summary>Reset</summary>
        public static readonly ViewAction Reset = Factory.Create(nameof(Reset));

        // ==================== 文件操作（File 前缀） ====================

        /// <summary>新建文件</summary>
        public static readonly ViewAction FileNew = Factory.Create(nameof(FileNew));

        /// <summary>打开文件</summary>
        public static readonly ViewAction FileOpen = Factory.Create(nameof(FileOpen));

        /// <summary>保存文件</summary>
        public static readonly ViewAction FileSave = Factory.Create(nameof(FileSave));

        /// <summary>另存为</summary>
        public static readonly ViewAction FileSaveAs = Factory.Create(nameof(FileSaveAs));

        /// <summary>关闭文件</summary>
        public static readonly ViewAction FileClose = Factory.Create(nameof(FileClose));

        /// <summary>打印</summary>
        public static readonly ViewAction Print = Factory.Create(nameof(Print));

        /// <summary>打印预览</summary>
        public static readonly ViewAction PrintPreview = Factory.Create(nameof(PrintPreview));

        /// <summary>页面设置</summary>
        public static readonly ViewAction PageSetup = Factory.Create(nameof(PageSetup));

        // ==================== 编辑操作 ====================

        /// <summary>撤销</summary>
        public static readonly ViewAction Undo = Factory.Create(nameof(Undo));

        /// <summary>重做</summary>
        public static readonly ViewAction Redo = Factory.Create(nameof(Redo));

        /// <summary>剪切</summary>
        public static readonly ViewAction Cut = Factory.Create(nameof(Cut));

        /// <summary>复制</summary>
        public static readonly ViewAction Copy = Factory.Create(nameof(Copy));

        /// <summary>粘贴</summary>
        public static readonly ViewAction Paste = Factory.Create(nameof(Paste));

        /// <summary>全选</summary>
        public static readonly ViewAction SelectAll = Factory.Create(nameof(SelectAll));

        // ==================== 导航操作 ====================

        /// <summary>打开/导航到</summary>
        public static readonly ViewAction Open = Factory.Create(nameof(Open));

        /// <summary>下一个</summary>
        public static readonly ViewAction Next = Factory.Create(nameof(Next));

        /// <summary>上一个</summary>
        public static readonly ViewAction Previous = Factory.Create(nameof(Previous));

        /// <summary>第一个</summary>
        public static readonly ViewAction First = Factory.Create(nameof(First));

        /// <summary>最后一个</summary>
        public static readonly ViewAction Last = Factory.Create(nameof(Last));

        /// <summary>后退</summary>
        public static readonly ViewAction GoBack = Factory.Create(nameof(GoBack));

        /// <summary>前进</summary>
        public static readonly ViewAction GoForward = Factory.Create(nameof(GoForward));

        /// <summary>转到</summary>
        public static readonly ViewAction GoTo = Factory.Create(nameof(GoTo));

        // ==================== 数据操作 ====================

        /// <summary>加载</summary>
        public static readonly ViewAction Load = Factory.Create(nameof(Load));

        /// <summary>重新加载</summary>
        public static readonly ViewAction Reload = Factory.Create(nameof(Reload));

        /// <summary>导入</summary>
        public static readonly ViewAction Import = Factory.Create(nameof(Import));

        /// <summary>导出</summary>
        public static readonly ViewAction Export = Factory.Create(nameof(Export));

        /// <summary>筛选</summary>
        public static readonly ViewAction Filter = Factory.Create(nameof(Filter));

        /// <summary>排序</summary>
        public static readonly ViewAction Sort = Factory.Create(nameof(Sort));

        /// <summary>搜索</summary>
        public static readonly ViewAction Search = Factory.Create(nameof(Search));

        /// <summary>查找</summary>
        public static readonly ViewAction Find = Factory.Create(nameof(Find));

        /// <summary>清除</summary>
        public static readonly ViewAction Clear = Factory.Create(nameof(Clear));

        // ==================== 视图/显示操作（View 前缀） ====================

        /// <summary>查看</summary>
        public static readonly ViewAction View = Factory.Create(nameof(View));

        /// <summary>显示</summary>
        public static readonly ViewAction ViewShow = Factory.Create(nameof(ViewShow));

        /// <summary>隐藏</summary>
        public static readonly ViewAction ViewHide = Factory.Create(nameof(ViewHide));

        /// <summary>切换</summary>
        public static readonly ViewAction ViewToggle = Factory.Create(nameof(ViewToggle));

        /// <summary>展开</summary>
        public static readonly ViewAction ViewExpand = Factory.Create(nameof(ViewExpand));

        /// <summary>折叠</summary>
        public static readonly ViewAction ViewCollapse = Factory.Create(nameof(ViewCollapse));

        /// <summary>放大</summary>
        public static readonly ViewAction ZoomIn = Factory.Create(nameof(ZoomIn));

        /// <summary>缩小</summary>
        public static readonly ViewAction ZoomOut = Factory.Create(nameof(ZoomOut));

        /// <summary>全屏</summary>
        public static readonly ViewAction FullScreen = Factory.Create(nameof(FullScreen));

        // ==================== 其他常用操作 ====================

        /// <summary>提交</summary>
        public static readonly ViewAction Submit = Factory.Create(nameof(Submit));

        /// <summary>确认</summary>
        public static readonly ViewAction Confirm = Factory.Create(nameof(Confirm));

        /// <summary>开始</summary>
        public static readonly ViewAction Start = Factory.Create(nameof(Start));

        /// <summary>停止</summary>
        public static readonly ViewAction Stop = Factory.Create(nameof(Stop));

        /// <summary>暂停</summary>
        public static readonly ViewAction Pause = Factory.Create(nameof(Pause));

        /// <summary>继续</summary>
        public static readonly ViewAction Resume = Factory.Create(nameof(Resume));

        /// <summary>帮助</summary>
        public static readonly ViewAction Help = Factory.Create(nameof(Help));

        /// <summary>设置</summary>
        public static readonly ViewAction Settings = Factory.Create(nameof(Settings));

        /// <summary>关于</summary>
        public static readonly ViewAction About = Factory.Create(nameof(About));
    }
}
