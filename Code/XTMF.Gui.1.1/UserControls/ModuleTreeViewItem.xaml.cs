using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModuleTreeViewItem.xaml
    /// </summary>
    public partial class ModuleTreeViewItem : UserControl
    {

        public static readonly DependencyProperty ModuleTypeDependencyProperty =
  DependencyProperty.Register("ModuleType",
      typeof(ModuleType), typeof(ModuleTreeViewItem),
          new PropertyMetadata(null));


        public static readonly DependencyProperty IsSelectedDependencyProperty =
         DependencyProperty.Register("IsSelected",
             typeof(bool), typeof(ModuleTreeViewItem),
                 new PropertyMetadata(true));



        public static readonly DependencyProperty TitleTextDependencyProperty =

            DependencyProperty.Register("TitleText",
            typeof(string), typeof(ModuleTreeViewItem),
                new PropertyMetadata(null));


        public static readonly DependencyProperty SubTextDependencyProperty =
        DependencyProperty.Register("SubText",
            typeof(string), typeof(ModuleTreeViewItem),
                new PropertyMetadata(null));

        public static readonly DependencyProperty BitmapIconDependencyProperty =
        DependencyProperty.Register("IsBitmapIcon",
            typeof(bool), typeof(ModuleTreeViewItem),
                new PropertyMetadata(false));

        public static readonly DependencyProperty PathIconDependencyProperty =
        DependencyProperty.Register("IsPathIcon",
            typeof(bool), typeof(ModuleTreeViewItem),
                new PropertyMetadata(true));

        public static readonly DependencyProperty IconPathDependencyProperty =
       DependencyProperty.Register("IconPath",
           typeof(Path), typeof(ModuleTreeViewItem),
               new PropertyMetadata(null));

        public ModuleTreeViewItem()
        {
            InitializeComponent();

            Path path = new Path();
            path.Data = (PathGeometry)Application.Current.Resources["MetaModuleIconPath"];
            path.Fill = Brushes.Black;
            this.IconPath = path;
        }


        public ModuleType ModuleType
        {

            get { return (ModuleType)GetValue(ModuleTypeDependencyProperty); }

            set { SetValue(ModuleTypeDependencyProperty,value);}
        }

        public bool IsSelected
        {
            get { return (bool)this.GetValue(IsSelectedDependencyProperty); }
            set
            {

                this.SetValue(IsSelectedDependencyProperty, value);
            }
        }

        public Path IconPath
        {
            get { return (Path)this.GetValue(IconPathDependencyProperty); }
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

                this.SetValue(TitleTextDependencyProperty, value);
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

    public enum ModuleType
    {
        Optional, Meta, Required
    }


}
