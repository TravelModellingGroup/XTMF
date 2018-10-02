using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace XTMF.Gui.UserControls
{
    public class DragDropListView : ListViewControl
    {
        protected override void OnDragOver(DragEventArgs e)
        {
            Console.WriteLine("Drag over");
            base.OnDragOver(e);

        }
    }
}
