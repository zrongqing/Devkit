using System.Windows.Input;
using Syncfusion.UI.Xaml.TreeView;

namespace Devkit.Core.UI.Command
{
    public static class ExpandCollapseCommand
    {
        static ExpandCollapseCommand()
        {
            CommandManager.RegisterClassCommandBinding(typeof(SfTreeView), new CommandBinding(ExpandAllNodes, OnExecuteExpandAllNodes, OnCanExecuteExpandAllNodes));
            CommandManager.RegisterClassCommandBinding(typeof(SfTreeView), new CommandBinding(CollapseAllNodes, OnExecuteCollapseAllNodes, OnCanExecuteCollapseAllNodes));
        }

        #region ExpandAll Command

        public static RoutedCommand ExpandAllNodes = new RoutedCommand("ExpandAll", typeof(SfTreeView));

        private static void OnExecuteExpandAllNodes(object sender, ExecutedRoutedEventArgs args)
        {
            var treeView = sender as SfTreeView;
            treeView?.ExpandAll();
        }

        private static void OnCanExecuteExpandAllNodes(object sender, CanExecuteRoutedEventArgs args)
        {
            args.CanExecute = true;
        }

        #endregion

        #region CollapseAll Command

        public static RoutedCommand CollapseAllNodes = new RoutedCommand("CollapseAll", typeof(SfTreeView));

        private static void OnExecuteCollapseAllNodes(object sender, ExecutedRoutedEventArgs args)
        {
            var treeView = sender as SfTreeView;
            treeView?.CollapseAll();
        }

        private static void OnCanExecuteCollapseAllNodes(object sender, CanExecuteRoutedEventArgs args)
        {
            args.CanExecute = true;
        }

        #endregion
    }
}
