using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace XTMF.Gui.UserControls
{
    class PopupBoxEx : PopupBox
    {
        /// <summary>
        /// Overridden mouse leave event to delay the close action.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            return;
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            return;
        }
    }
}
