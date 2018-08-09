using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using XTMF.Annotations;
using XTMF.Interfaces;

namespace XTMF.Editing
{
    public class RegionDisplaysModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private List<IRegionDisplay> _regionDisplays;

        private ModelSystemEditingSession _session;

        private ModelSystemModel _modelSystemModel;

        public List<IRegionDisplay> RegionDisplays
        {
            get
            {
                return this._regionDisplays;
            }
            private set
            {
                this._regionDisplays = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="session"></param>
        /// <param name="modelSystemModel"></param>
        /// <param name="regionDisplays"></param>
        public RegionDisplaysModel(ModelSystemEditingSession session, ModelSystemModel modelSystemModel, List<IRegionDisplay> regionDisplays)
        {
            this._regionDisplays = regionDisplays;
            this._session = session;
            this._modelSystemModel = modelSystemModel;
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
