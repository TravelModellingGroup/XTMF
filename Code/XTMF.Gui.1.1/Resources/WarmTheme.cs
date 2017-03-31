using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.AvalonDock.Themes;

namespace XTMF.Gui.Resources
{
    public class WarmTheme : Theme
    {
        public override Uri GetResourceUri()
        {
            return new Uri(
                "/XTMF.Gui;component/Resources/WarmTheme.xaml",
                UriKind.Relative);
        }
    }
}
