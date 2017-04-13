using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace XTMF.Gui.Models
{
    public class ModelSystemInfoTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ModuleDisabled { get; set; }

        public DataTemplate ModuleEnabled { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var param = item as ModelSystemStructureDisplayModel;
            if (param != null)
            {
                if (param.IsDisabled)
                {
                    return ModuleDisabled;
                }

                else
                {
                    return ModuleEnabled;
                }
            }

            return ModuleDisabled;

        }
    }
}
