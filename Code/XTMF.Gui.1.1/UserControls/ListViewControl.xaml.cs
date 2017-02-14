using System;
using System.Collections.Generic;
using System.Linq;
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

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ListViewControl.xaml
    /// </summary>
    public partial class ListViewControl : UserControl
    {
        public static readonly DependencyProperty TitleTextDependencyProperty = 
            
            DependencyProperty.Register("TitleText", 
            typeof(string), typeof(ListViewControl),
                new PropertyMetadata(null));


        public static readonly DependencyProperty SubTextDependencyProperty =
        DependencyProperty.Register("SubText", 
            typeof(string), typeof(ListViewControl),
                new PropertyMetadata(null));



        public ListViewControl()
        {
            

            InitializeComponent();

            //this.Title.Content = (string)this.GetValue(TitleTextDependencyProperty);
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
    }
}
