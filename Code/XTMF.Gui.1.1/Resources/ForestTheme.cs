using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.AvalonDock.Themes;

namespace XTMF.Gui.Resources
{
    public class ForestTheme : Theme
    {
        public override Uri GetResourceUri()
        {
            return new Uri(
                "/XTMF.Gui;component/Resources/ForestTheme.xaml",
                UriKind.Relative);
        }
    }
}
