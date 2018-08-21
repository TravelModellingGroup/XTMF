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
using MaterialDesignThemes.Wpf;
using XTMF.Gui.Annotations;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for PackIconOverlap.xaml
    /// </summary>
    public partial class PackIconOverlap : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty IconKindDependencyProperty =
            DependencyProperty.Register("IconKind", typeof(PackIconKind), typeof(PackIconOverlap), new PropertyMetadata(null));

        public static readonly DependencyProperty IconKindMinorDependencyProperty =
            DependencyProperty.Register("IconKindMinor", typeof(PackIconKind), typeof(PackIconOverlap), new PropertyMetadata(null));

        public PackIconOverlap()
        {


            InitializeComponent();
            DataContext = this;
        }

        public PackIconKind IconKind
        {
            get => (PackIconKind)GetValue(IconKindDependencyProperty);
            set => SetValue(IconKindDependencyProperty, value);
        }

        public PackIconKind IconKindMinor
        {
            get => (PackIconKind)GetValue(IconKindMinorDependencyProperty);
            set => SetValue(IconKindMinorDependencyProperty, value); 
        }

        public double MinorWidth => Width / 2;

        public double MinorHeight => Height /2;

        public double MinorBorderWidth => (Width / 2)+5;

        public double MinorBorderHeight => (Height / 2)+5;

        public double MajorWidth => Width;

        public double MajorHeight => Height;



        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
