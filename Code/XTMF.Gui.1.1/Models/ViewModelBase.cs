using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using XTMF.Annotations;

namespace XTMF.Gui.Models
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public string ViewTitle
        {
            get => _viewTitle;
            set
            {
                _viewTitle = value;
                OnPropertyChanged(nameof(ViewTitle));
            }
        }

        private UserControl _viewModelControl;

        private string _viewTitle;

        public UserControl ViewModelControl
        {
            get => _viewModelControl;
            set
            {
                _viewModelControl = value;
                OnPropertyChanged(nameof(ViewModelControl));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
