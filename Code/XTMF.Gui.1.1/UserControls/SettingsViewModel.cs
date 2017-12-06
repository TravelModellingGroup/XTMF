using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MaterialDesignColors;

namespace XTMF.Gui.UserControls
{
    class SettingsViewModel
    {
        public IEnumerable<Swatch> Swatches { get; }
        public SettingsViewModel()
        {
            Swatches = new SwatchesProvider().Swatches;
        }
    }
}
