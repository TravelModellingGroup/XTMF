using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XTMF.Annotations;

namespace XTMF.Gui.UserControls
{


    /// <summary>
    /// Interaction logic for FlatToolButton.xaml
    /// </summary>
    /// 
    public partial class FlatToolButton : UserControl, INotifyPropertyChanged
    {


        public static readonly DependencyProperty ToolTextTextDependencyProperty =

    DependencyProperty.Register("ToolText",
    typeof(string), typeof(FlatToolButton),
        new PropertyMetadata(null));

        public static readonly DependencyProperty IconPathDependencyProperty =
DependencyProperty.Register("IconPath",
   typeof(Path), typeof(FlatToolButton),
       new PropertyMetadata(null));

        public event RoutedEventHandler Click;

        public FlatToolButton()
        {
            InitializeComponent();
            DataContext = this;

           
        }


        public string ToolText
        {
            get
            {
                return (string)GetValue(ToolTextTextDependencyProperty);
            }
            set
            {
                SetValue(ToolTextTextDependencyProperty, value);
            }
        }

        public Path IconPath
        {
            get
            {
                return (Path)this.GetValue(IconPathDependencyProperty); 

                
            }
            set
            {
        
                this.SetValue(IconPathDependencyProperty, value);
              
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (Click != null)
            {
                Click(sender, e);
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
