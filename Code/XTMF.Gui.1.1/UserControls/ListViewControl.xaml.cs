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
using XTMF.Gui.Annotations;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ListViewControl.xaml
    /// </summary>
    public partial class ListViewControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty TitleTextDependencyProperty = 
            
            DependencyProperty.Register("TitleText", 
            typeof(string), typeof(ListViewControl),
                new PropertyMetadata(null));


        public static readonly DependencyProperty SubTextDependencyProperty =
        DependencyProperty.Register("SubText", 
            typeof(string), typeof(ListViewControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty BitmapIconDependencyProperty =
        DependencyProperty.Register("IsBitmapIcon",
            typeof(bool), typeof(ListViewControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty PathIconDependencyProperty =
        DependencyProperty.Register("IsPathIcon",
            typeof(bool), typeof(ListViewControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty IconPathDependencyProperty =
       DependencyProperty.Register("IconPath",
           typeof(Path), typeof(ListViewControl),
               new PropertyMetadata(null));


        public static readonly DependencyProperty IsSelectedDependencyProperty =
         DependencyProperty.Register("IsSelected",
             typeof(bool), typeof(ListViewControl),
                 new PropertyMetadata(true));


        public ListViewControl()
        {
            

            InitializeComponent();

            
  
        }

        public Path IconPath
        {
            get { return (Path) this.GetValue(IconPathDependencyProperty); }
            set { this.SetValue(IconPathDependencyProperty, value); }
        }

        public bool IsBitmapIcon
        {
            get { return (bool)this.GetValue(BitmapIconDependencyProperty); }
            set
            {

                this.SetValue(BitmapIconDependencyProperty, value);
            }
        }

        public bool IsSelected
        {
            get { return (bool)this.GetValue(IsSelectedDependencyProperty); }
            set
            {

                this.SetValue(IsSelectedDependencyProperty, value);
            }
        }



        public bool IsPathIcon
        {
            get { return (bool)this.GetValue(PathIconDependencyProperty); }
            set
            {

                this.SetValue(PathIconDependencyProperty, value);
            }
        }

        public string TitleText
        {
            get { return (string)this.GetValue(TitleTextDependencyProperty); }
            set
            {
               
                this.SetValue(TitleTextDependencyProperty,value);
                this.Title.Content = value;
            }
        }

        public string SubText
        {
            get { return (string)this.GetValue(SubTextDependencyProperty); }
            set
            {

                this.SetValue(SubTextDependencyProperty, value);
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
