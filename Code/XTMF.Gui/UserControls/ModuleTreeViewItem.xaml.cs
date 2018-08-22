/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MaterialDesignThemes.Wpf;
using XTMF.Annotations;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for ModuleTreeViewItem.xaml
    /// </summary>
    public partial class ModuleTreeViewItem : UserControl, INotifyPropertyChanged
    {

        public static readonly DependencyProperty ModuleTypeDependencyProperty =
            DependencyProperty.Register("ModuleType", typeof(ModuleType), typeof(ModuleTreeViewItem), new PropertyMetadata(null));

        public static readonly DependencyProperty BackingModelDependencyProperty =
            DependencyProperty.Register("BackingModel", typeof(ModelSystemStructureDisplayModel), typeof(ModuleTreeViewItem), new PropertyMetadata(null));

        public static readonly DependencyProperty IsSelectedDependencyProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(ModuleTreeViewItem), new PropertyMetadata(true));

        public static readonly DependencyProperty IsExpandedDependencyProperty =
            DependencyProperty.Register("IsExpanded", typeof(bool), typeof(ModuleTreeViewItem), new PropertyMetadata(true));

        public static readonly DependencyProperty TitleTextDependencyProperty =
            DependencyProperty.Register("TitleText", typeof(string), typeof(ModuleTreeViewItem), new PropertyMetadata(null));

        public static readonly DependencyProperty SubTextDependencyProperty =
            DependencyProperty.Register("SubText", typeof(string), typeof(ModuleTreeViewItem), new PropertyMetadata(null));

        public static readonly DependencyProperty BitmapIconDependencyProperty =
            DependencyProperty.Register("IsBitmapIcon", typeof(bool), typeof(ModuleTreeViewItem), new PropertyMetadata(false));

        public static readonly DependencyProperty PathIconDependencyProperty =
            DependencyProperty.Register("IsPathIcon", typeof(bool), typeof(ModuleTreeViewItem), new PropertyMetadata(true));

        public static readonly DependencyProperty IconPathDependencyProperty =
            DependencyProperty.Register("IconPath", typeof(Path), typeof(ModuleTreeViewItem), new PropertyMetadata(null));

        public static readonly DependencyProperty CustomBackgrounDependencyProperty =
            DependencyProperty.Register("CustomBackground", typeof(Brush), typeof(ModuleTreeViewItem), new PropertyMetadata(Brushes.Transparent));


        public PackIconKind Icon { get; set; }

        public ModuleTreeViewItem()
        {
            InitializeComponent();
            Loaded += ModuleTreeViewItem_Loaded;
        }


        public Brush CustomBackground
        {
            get => (Brush)GetValue(CustomBackgrounDependencyProperty);
            set => SetValue(CustomBackgrounDependencyProperty,value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleTreeViewItem_Loaded(object sender, RoutedEventArgs e)
        {

            BackingModel.BaseModel.PropertyChanged += BaseModelOnPropertyChanged;
            BackingModel.PropertyChanged += BaseModelOnPropertyChanged;

            var type = BackingModel.BaseModel.Type;

            PackIconKind iconKind = PackIconKind.Settings;
            bool hasCustomIcon = false;


            if (type != null)
            {
                foreach (var attr in type.GetCustomAttributes(true))
                {
                    if (attr.GetType() == typeof(ModuleInformationAttribute))
                    {
                        var info = (attr as ModuleInformationAttribute);
                        if (info.IconURI != null)
                        {
                            try
                            {
                                iconKind = (PackIconKind) System.Enum.Parse(typeof(PackIconKind), info.IconURI);
                                hasCustomIcon = true;
                            }
                            catch
                            {
                            }

                        }
                    }
                }
            }


            if (BackingModel.BaseModel.IsMetaModule)
            {
                IconPath = new Path {Data = (PathGeometry) Application.Current.Resources["MetaModuleIconPath"]};
                PathBorder.Visibility = Visibility.Visible;
            }
            else if (!BackingModel.BaseModel.IsMetaModule && !BackingModel.BaseModel.IsCollection)
            {
                IconPath = new Path
                {
                    Data = (PathGeometry) Application.Current.Resources["ModuleIcon2Path"],
                };
                if (hasCustomIcon)
                {
                    PackIcon.Kind = iconKind;
                    IconPath.Visibility = Visibility.Hidden;
                    PathBorder.Visibility = Visibility.Collapsed;
                    PackIcon.Visibility = Visibility.Visible;

                }
                else
                {
                    PackIcon.Kind = PackIconKind.Settings;
                    PackIcon.Visibility = Visibility.Collapsed;
                    PathBorder.Visibility = Visibility.Visible;


                }

            }
            else if (BackingModel.BaseModel.IsCollection)
            {
                IconPath = new Path {Data = (PathGeometry) Application.Current.Resources["CollectionIconPath"]};
                PathBorder.Visibility = Visibility.Visible;
                //PackIcon.Kind = PackIconKind.ViewList;
            }

            var ammount = BackingModel.BaseModel.IsDisabled ? 0.4 : 1.0;
            SubTextLabel.Opacity = ammount;
            Title.Opacity = ammount;
            IconPath.Opacity = ammount;
            SetupColours();
            e.Handled = true;
           
        }



        private void BaseModelOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case "IsSelected":
                    {
                        if (sender is ModelSystemStructureDisplayModel s) IsSelected = s.IsSelected;
                    }
                    break;
                case "IsExpanded":
                    {
                        if (sender is ModelSystemStructureDisplayModel s) IsExpanded = s.IsExpanded;
                    }
                    break;
                case "IsMetaModule":
                    IconPath =
                    new Path
                    {
                        Data = BackingModel.BaseModel.IsMetaModule ? (PathGeometry)Application.Current.Resources["MetaModuleIconPath"] :
                                                                     (PathGeometry)Application.Current.Resources["ModuleIcon2Path"],
                        Fill = Brushes.DarkSlateGray
                    };
                    break;
                case "IsDisabled":
                    if (BackingModel != null)
                    {
                        var ammount = BackingModel.BaseModel.IsDisabled ? 0.4 : 1.0;
                        SubTextLabel.Opacity = ammount;
                        Title.Opacity = ammount;
                        IconPath.Opacity = ammount;
                    }
                    break;
            }
            Dispatcher.Invoke(() =>
            {
                SetupColours();
            });
        }

        private void SetupColours()
        {
            if (BackingModel != null)
            {
                if (BackingModel.BaseModel.IsOptional && BackingModel.IsCollection &&
                    BackingModel.BaseModel.Children.Count == 0)
                {
                    ContentBorder.BorderThickness = new Thickness(1);
                    ContentBorder.BorderBrush = new SolidColorBrush(Colors.OliveDrab);
                    IconPath.Fill = new SolidColorBrush(Colors.OliveDrab);
                    NotificationIcon.Data = (PathGeometry)Application.Current.Resources["OptionalIconPath"];
                    NotificationIcon.Visibility = Visibility.Visible;
                    NotificationIcon.Fill = Brushes.OliveDrab;
                   
                }
                else if (!BackingModel.BaseModel.IsOptional && BackingModel.IsCollection && BackingModel.BaseModel.Children.Count == 0)
                {
                    ContentBorder.BorderBrush = new SolidColorBrush(Colors.IndianRed);
                    ContentBorder.BorderThickness = new Thickness(1);
                    NotificationIcon.Data = (PathGeometry)Application.Current.Resources["FullErrorIconPath"];
                    NotificationIcon.Visibility = Visibility.Visible;
                    NotificationIcon.Fill = Brushes.IndianRed;
                    InfoBorder.Background = (Brush)Application.Current.Resources["StripeBrush"];
                    BlockBorder.Opacity = 0.7;
                    PackIcon.Kind = PackIconKind.AlertCircle;
                }
                else if (!BackingModel.BaseModel.IsOptional && BackingModel.Type == null && !BackingModel.IsCollection)
                {
                    ContentBorder.BorderBrush = new SolidColorBrush(Colors.IndianRed);
                    ContentBorder.BorderThickness = new Thickness(1);
                    NotificationIcon.Data = (PathGeometry)Application.Current.Resources["FullErrorIconPath"];
                    NotificationIcon.Visibility = Visibility.Visible;
                    NotificationIcon.Fill = Brushes.IndianRed;
                    InfoBorder.Background = (Brush)Application.Current.Resources["StripeBrush"];
                    BlockBorder.Opacity = 0.7;
                }
                else if(BackingModel.Type == null && !BackingModel.IsCollection)
                {
                    ContentBorder.BorderThickness = new Thickness(1);
                    ContentBorder.BorderBrush = new SolidColorBrush(Colors.OliveDrab);
                    IconPath.Fill = new SolidColorBrush(Colors.OliveDrab);
                    NotificationIcon.Data = (PathGeometry)Application.Current.Resources["OptionalIconPath"];
                    NotificationIcon.Visibility = Visibility.Visible;
                    NotificationIcon.Fill = Brushes.OliveDrab;
                }
                else
                {
                    ContentBorder.BorderBrush = new SolidColorBrush(Colors.LightSlateGray);
                    ContentBorder.BorderThickness = new Thickness(1);
                    BlockBorder.Opacity = 1.0;
                    NotificationIcon.Visibility = Visibility.Hidden;
                }
            }
        }

        public ModuleType ModuleType
        {
            get => (ModuleType)GetValue(ModuleTypeDependencyProperty);
            set => SetValue(ModuleTypeDependencyProperty, value);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedDependencyProperty);
            set
            {
                if (value == true)
                {
                    BringIntoView();
                }
                SetValue(IsSelectedDependencyProperty, value);
            }
        }

        public Path IconPath
        {
            get => (Path)GetValue(IconPathDependencyProperty);
            set => SetValue(IconPathDependencyProperty, value);
        }

        public bool IsBitmapIcon
        {
            get => (bool)GetValue(BitmapIconDependencyProperty);
            set => SetValue(BitmapIconDependencyProperty, value);
        }

        public ModelSystemStructureDisplayModel BackingModel
        {
            get => (ModelSystemStructureDisplayModel)GetValue(BackingModelDependencyProperty);
            set => SetValue(BackingModelDependencyProperty, value);
        }

        public bool IsPathIcon
        {
            get => (bool)GetValue(PathIconDependencyProperty);
            set => SetValue(PathIconDependencyProperty, value);
        }

        public string TitleText
        {
            get => (string)GetValue(TitleTextDependencyProperty);
            set
            {
                SetValue(TitleTextDependencyProperty, value);
                Title.Content = value;
            }
        }

        public string SubText
        {
            get => (string)GetValue(SubTextDependencyProperty);
            set => SetValue(SubTextDependencyProperty, value);
        }

        public bool IsExpanded { get; set; } = false;

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum ModuleType
    {
        Optional, Meta, Required
    }
}
