using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Gui.Models
{
    public class ValidationErrorDisplayModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;


        private ModelSystemStructureDisplayModel displayModel;

        private string _error;
        public string ErrorString =>  _error;


        public string ModuleName => displayModel.Name;

        public ModelSystemStructureDisplayModel DisplayModule => displayModel;



        public ValidationErrorDisplayModel(ModelSystemStructureDisplayModel root, string error, Queue<int> path)
        {
            this._error = error;
            if(path.Count == 1)
            {
                displayModel = root;
            }
            else
            {
                path.Dequeue();
                displayModel = MapModuleWithPath(root, path);
            }

           
        }


        private ModelSystemStructureDisplayModel MapModuleWithPath(ModelSystemStructureDisplayModel parent, Queue<int> path)
        {
            if(path.Count == 1)
            {
                return parent.Children[path.Dequeue()];
            }
            else
            {
                return MapModuleWithPath(parent.Children[path.Dequeue()], path);
            }
        }
    }
}
