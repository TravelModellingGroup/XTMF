using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace XTMF.Gui.UserControls
{
    class ModelSystemListView : ListView
    {
        protected override void OnContextMenuOpening(ContextMenuEventArgs e)
        {
            base.OnContextMenuOpening(e);


            IsCanPasteModelSystem = MainWindow.Us.ClipboardModel != null;



        }

        public static readonly DependencyProperty IsCanPasteModelSystemDependencyProperty =
        DependencyProperty.Register("IsCanPasteModelSystem", typeof(bool), typeof(ModelSystemListView),
        new PropertyMetadata(false));

        public bool IsCanPasteModelSystem
        {
            set
            {
                SetValue(IsCanPasteModelSystemDependencyProperty, value);
            }


            get
            {
                return (bool)GetValue(IsCanPasteModelSystemDependencyProperty);
            }
        }
    }
}
