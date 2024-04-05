using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace XTMF.Gui.UserControls;

/// <summary>
/// Interaction logic for PackIconOverlap.xaml
/// </summary>
public partial class PackIconOverlap : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty IconKindDependencyProperty =
        DependencyProperty.Register("IconKind", typeof(PackIconKind), typeof(PackIconOverlap), new PropertyMetadata(null));

    public static readonly DependencyProperty IconKindMinorDependencyProperty =
        DependencyProperty.Register("IconKindMinor", typeof(PackIconKind), typeof(PackIconOverlap), new PropertyMetadata(null));

    public static readonly DependencyProperty ShowMinorIconBackgroundDependencyProperty =
        DependencyProperty.Register("ShowMinorIconBackground", typeof(bool), typeof(PackIconOverlap), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowMinorIconDependencyProperty =
        DependencyProperty.Register("ShowMinorIcon", typeof(bool), typeof(PackIconOverlap), new PropertyMetadata(true));

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
        set
        {
            OnPropertyChanged(nameof(IconKindMinor));
            OnPropertyChanged(nameof(MinorIconVisibility));
            OnPropertyChanged(nameof(MinorIconBackgroundVisibility));
            SetValue(IconKindMinorDependencyProperty, value); 

        }
    }

    public bool ShowMinorIconBackground
    {
        get => (bool) GetValue(ShowMinorIconBackgroundDependencyProperty);
        set => SetValue(ShowMinorIconBackgroundDependencyProperty, value);
    }

    public bool ShowMinorIcon
    {
        get => (bool)GetValue(ShowMinorIconDependencyProperty);
        set
        {
            OnPropertyChanged(nameof(ShowMinorIcon));
            OnPropertyChanged(nameof(ShowMinorIconBackground));
            OnPropertyChanged(nameof(MinorIconVisibility));
            OnPropertyChanged(nameof(MinorIconBackgroundVisibility));
            SetValue(ShowMinorIconDependencyProperty, value);
        }
    }

    public Visibility MinorIconVisibility
    {
        get
        {
            if (!ShowMinorIcon)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }
    }

    public Visibility MinorIconBackgroundVisibility
    {
        get
        {
            if (ShowMinorIconBackground && ShowMinorIcon)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Hidden;
            }
        }
    }

    public double MinorWidth => Width / 2;

    public double MinorHeight => Height /2;

    public double MinorBorderWidth => (Width / 2)-2;

    public double MinorBorderHeight => (Height / 2)-2;

    public double MajorWidth => Width;

    public double MajorHeight => Height;



    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
