using System;

namespace WinformsMVP.MVP.ViewActions
{
    /// <summary>
    /// Standard action library - Provides ViewAction instances for common actions
    ///
    /// Purpose:
    /// 1. Directly use standard ViewAction (without qualifiers)
    /// 2. Ensure consistency in action naming across application
    /// 3. Reduce code duplication
    ///
    /// Usage examples:
    /// <code>
    /// // Method 1: Directly use standard actions (no qualifiers)
    /// public static class MyModuleActions
    /// {
    ///     public static readonly ViewAction Save = StandardActions.Save;
    ///     public static readonly ViewAction Cancel = StandardActions.Cancel;
    /// }
    ///
    /// // Method 2: When module prefix is needed, use Factory
    /// public static class MyModuleActions
    /// {
    ///     private static readonly ViewActionFactory Factory =
    ///         ViewAction.Factory.WithQualifier("MyModule");
    ///
    ///     public static readonly ViewAction Save = Factory.Create("Save");  // "MyModule.Save"
    ///     public static readonly ViewAction ProcessOrder = Factory.Create("ProcessOrder");
    /// }
    /// </code>
    /// </summary>
    public static class StandardActions
    {
        // ==================== Common Dialog Actions ====================

        /// <summary>OK</summary>
        public static readonly ViewAction Ok = ViewAction.Create("Ok");

        /// <summary>Cancel</summary>
        public static readonly ViewAction Cancel = ViewAction.Create("Cancel");

        /// <summary>Yes</summary>
        public static readonly ViewAction Yes = ViewAction.Create("Yes");

        /// <summary>No</summary>
        public static readonly ViewAction No = ViewAction.Create("No");

        /// <summary>Apply</summary>
        public static readonly ViewAction Apply = ViewAction.Create("Apply");

        /// <summary>Close</summary>
        public static readonly ViewAction Close = ViewAction.Create("Close");

        /// <summary>Retry</summary>
        public static readonly ViewAction Retry = ViewAction.Create("Retry");

        /// <summary>Ignore</summary>
        public static readonly ViewAction Ignore = ViewAction.Create("Ignore");

        /// <summary>Abort</summary>
        public static readonly ViewAction Abort = ViewAction.Create("Abort");

        // ==================== CRUD Operations ====================

        /// <summary>Save</summary>
        public static readonly ViewAction Save = ViewAction.Create("Save");

        /// <summary>Add/New</summary>
        public static readonly ViewAction Add = ViewAction.Create("Add");

        /// <summary>Edit/Modify</summary>
        public static readonly ViewAction Edit = ViewAction.Create("Edit");

        /// <summary>Delete</summary>
        public static readonly ViewAction Delete = ViewAction.Create("Delete");

        /// <summary>Remove (similar to Delete, but lighter semantics)</summary>
        public static readonly ViewAction Remove = ViewAction.Create("Remove");

        /// <summary>Create</summary>
        public static readonly ViewAction Create = ViewAction.Create("Create");

        /// <summary>Update</summary>
        public static readonly ViewAction Update = ViewAction.Create("Update");

        /// <summary>Refresh</summary>
        public static readonly ViewAction Refresh = ViewAction.Create("Refresh");

        /// <summary>Reset</summary>
        public static readonly ViewAction Reset = ViewAction.Create("Reset");

        // ==================== 文件操作（File 前缀） ====================

        /// <summary>新建文件</summary>
        public static readonly ViewAction FileNew = ViewAction.Create("New");

        /// <summary>打开文件</summary>
        public static readonly ViewAction FileOpen = ViewAction.Create("Open");

        /// <summary>保存文件</summary>
        public static readonly ViewAction FileSave = ViewAction.Create("Save");

        /// <summary>另存为</summary>
        public static readonly ViewAction FileSaveAs = ViewAction.Create("SaveAs");

        /// <summary>关闭文件</summary>
        public static readonly ViewAction FileClose = ViewAction.Create("Close");

        /// <summary>打印</summary>
        public static readonly ViewAction Print = ViewAction.Create("Print");

        /// <summary>打印预览</summary>
        public static readonly ViewAction PrintPreview = ViewAction.Create("PrintPreview");

        /// <summary>页面设置</summary>
        public static readonly ViewAction PageSetup = ViewAction.Create("PageSetup");

        // ==================== 编辑操作 ====================

        /// <summary>撤销</summary>
        public static readonly ViewAction Undo = ViewAction.Create("Undo");

        /// <summary>重做</summary>
        public static readonly ViewAction Redo = ViewAction.Create("Redo");

        /// <summary>剪切</summary>
        public static readonly ViewAction Cut = ViewAction.Create("Cut");

        /// <summary>复制</summary>
        public static readonly ViewAction Copy = ViewAction.Create("Copy");

        /// <summary>粘贴</summary>
        public static readonly ViewAction Paste = ViewAction.Create("Paste");

        /// <summary>全选</summary>
        public static readonly ViewAction SelectAll = ViewAction.Create("SelectAll");

        // ==================== 导航操作 ====================

        /// <summary>打开/导航到</summary>
        public static readonly ViewAction Open = ViewAction.Create("Open");

        /// <summary>下一个</summary>
        public static readonly ViewAction Next = ViewAction.Create("Next");

        /// <summary>上一个</summary>
        public static readonly ViewAction Previous = ViewAction.Create("Previous");

        /// <summary>第一个</summary>
        public static readonly ViewAction First = ViewAction.Create("First");

        /// <summary>最后一个</summary>
        public static readonly ViewAction Last = ViewAction.Create("Last");

        /// <summary>后退</summary>
        public static readonly ViewAction GoBack = ViewAction.Create("GoBack");

        /// <summary>前进</summary>
        public static readonly ViewAction GoForward = ViewAction.Create("GoForward");

        /// <summary>转到</summary>
        public static readonly ViewAction GoTo = ViewAction.Create("GoTo");

        // ==================== 数据操作 ====================

        /// <summary>加载</summary>
        public static readonly ViewAction Load = ViewAction.Create("Load");

        /// <summary>重新加载</summary>
        public static readonly ViewAction Reload = ViewAction.Create("Reload");

        /// <summary>导入</summary>
        public static readonly ViewAction Import = ViewAction.Create("Import");

        /// <summary>导出</summary>
        public static readonly ViewAction Export = ViewAction.Create("Export");

        /// <summary>筛选</summary>
        public static readonly ViewAction Filter = ViewAction.Create("Filter");

        /// <summary>排序</summary>
        public static readonly ViewAction Sort = ViewAction.Create("Sort");

        /// <summary>搜索</summary>
        public static readonly ViewAction Search = ViewAction.Create("Search");

        /// <summary>查找</summary>
        public static readonly ViewAction Find = ViewAction.Create("Find");

        /// <summary>清除</summary>
        public static readonly ViewAction Clear = ViewAction.Create("Clear");

        // ==================== 视图/显示操作（View 前缀） ====================

        /// <summary>查看</summary>
        public static readonly ViewAction View = ViewAction.Create("View");

        /// <summary>显示</summary>
        public static readonly ViewAction ViewShow = ViewAction.Create("Show");

        /// <summary>隐藏</summary>
        public static readonly ViewAction ViewHide = ViewAction.Create("Hide");

        /// <summary>切换</summary>
        public static readonly ViewAction ViewToggle = ViewAction.Create("Toggle");

        /// <summary>展开</summary>
        public static readonly ViewAction ViewExpand = ViewAction.Create("Expand");

        /// <summary>折叠</summary>
        public static readonly ViewAction ViewCollapse = ViewAction.Create("Collapse");

        /// <summary>放大</summary>
        public static readonly ViewAction ZoomIn = ViewAction.Create("ZoomIn");

        /// <summary>缩小</summary>
        public static readonly ViewAction ZoomOut = ViewAction.Create("ZoomOut");

        /// <summary>全屏</summary>
        public static readonly ViewAction FullScreen = ViewAction.Create("FullScreen");

        // ==================== 其他常用操作 ====================

        /// <summary>提交</summary>
        public static readonly ViewAction Submit = ViewAction.Create("Submit");

        /// <summary>确认</summary>
        public static readonly ViewAction Confirm = ViewAction.Create("Confirm");

        /// <summary>开始</summary>
        public static readonly ViewAction Start = ViewAction.Create("Start");

        /// <summary>停止</summary>
        public static readonly ViewAction Stop = ViewAction.Create("Stop");

        /// <summary>暂停</summary>
        public static readonly ViewAction Pause = ViewAction.Create("Pause");

        /// <summary>继续</summary>
        public static readonly ViewAction Resume = ViewAction.Create("Resume");

        /// <summary>帮助</summary>
        public static readonly ViewAction Help = ViewAction.Create("Help");

        /// <summary>设置</summary>
        public static readonly ViewAction Settings = ViewAction.Create("Settings");

        /// <summary>关于</summary>
        public static readonly ViewAction About = ViewAction.Create("About");
    }
}
