/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
  DependencyProperty.Register("ModuleType",
      typeof(ModuleType), typeof(ModuleTreeViewItem),
          new PropertyMetadata(null));

        public static readonly DependencyProperty BackingModelDependencyProperty =
DependencyProperty.Register("BackingModel",
   typeof(ModelSystemStructureDisplayModel), typeof(ModuleTreeViewItem),
       new PropertyMetadata(null));


        public static readonly DependencyProperty IsSelectedDependencyProperty =
         DependencyProperty.Register("IsSelected",
             typeof(bool), typeof(ModuleTreeViewItem),
                 new PropertyMetadata(true));

        public static readonly DependencyProperty IsExpandedDependencyProperty =
        DependencyProperty.Register("IsExpanded",
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




            this.Loaded += ModuleTreeViewItem_Loaded;
        }

        private void ModuleTreeViewItem_Loaded(object sender, RoutedEventArgs e)
        {

            BackingModel.BaseModel.PropertyChanged += BaseModelOnPropertyChanged;
            BackingModel.PropertyChanged += BaseModelOnPropertyChanged;
            if (BackingModel.BaseModel.IsMetaModule)
            {
                Path path = new Path { Data = (PathGeometry)Application.Current.Resources["MetaModuleIconPath"] };


                this.IconPath = path;
            }
            else if (!BackingModel.BaseModel.IsMetaModule && !BackingModel.BaseModel.IsCollection)
            {
                Path path = new Path
                {
                    Data = (PathGeometry)Application.Current.Resources["ModuleIcon2Path"],

                };

                this.IconPath = path;
            }
            else if (BackingModel.BaseModel.IsCollection)
            {
                Path path = new Path { Data = (PathGeometry)Application.Current.Resources["CollectionIconPath"] };

                this.IconPath = path;
            }


            if (BackingModel.BaseModel.IsDisabled)
            {

                SubTextLabel.Opacity = 0.4;
                Title.Opacity = 0.4;
                IconPath.Opacity = 0.4;
            }
            else
            {
                SubTextLabel.Opacity = 1.0;
                Title.Opacity = 1.0;
                IconPath.Opacity = 1.0;
            }

            if (BackingModel.BaseModel.IsOptional && BackingModel.IsCollection && BackingModel.BaseModel.Children.Count == 0)
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
            else
            {
                BlockBorder.Opacity = 1.0;
                NotificationIcon.Visibility = Visibility.Hidden;
                ContentBorder.BorderBrush = new SolidColorBrush(Colors.LightSlateGray);
                ContentBorder.BorderThickness = new Thickness(1);


            }





        }

        private void UpdateComponents()
        {

        }

        private void BaseModelOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (sender is ModelSystemStructureDisplayModel)
            {
                if (propertyChangedEventArgs.PropertyName == "IsSelected")
                {
                    IsSelected = ((ModelSystemStructureDisplayModel) sender).IsSelected;

                }

                if (propertyChangedEventArgs.PropertyName == "IsExpanded")
                {
                    IsExpanded = ((ModelSystemStructureDisplayModel) sender).IsExpanded;

                }
            }

            if (propertyChangedEventArgs.PropertyName == "IsMetaModule")
            {
                if (BackingModel.BaseModel.IsMetaModule)
                {
                    Path path = new Path();

                    path.Data = (PathGeometry)Application.Current.Resources["MetaModuleIconPath"];
                    path.Fill = Brushes.DarkSlateGray;
                    this.IconPath = path;
                }
                else if (!BackingModel.BaseModel.IsMetaModule)
                {
                    Path path = new Path();

                    path.Data = (PathGeometry)Application.Current.Resources["ModuleIcon2Path"];
                    path.Fill = Brushes.DarkSlateGray;
                    this.IconPath = path;
                }
            }

            if (propertyChangedEventArgs.PropertyName == "IsDisabled")
            {

                if (BackingModel != null)
                {
                    if (BackingModel.BaseModel.IsDisabled)
                    {

                        SubTextLabel.Opacity = 0.4;
                        Title.Opacity = 0.4;
                        IconPath.Opacity = 0.4;
                    }
                    else
                    {
                        SubTextLabel.Opacity = 1.0;
                        Title.Opacity = 1.0;
                        IconPath.Opacity = 1.0;
                    }
                }
            }
            this.Dispatcher.Invoke(() =>
            {

                if (BackingModel == null)
                {
                    return;
                }
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
                else
                {
                    ContentBorder.BorderBrush = new SolidColorBrush(Colors.LightSlateGray);
                    ContentBorder.BorderThickness = new Thickness(1);
                    BlockBorder.Opacity = 1.0;
                    NotificationIcon.Visibility = Visibility.Hidden;



                }
            });

        }


        public ModuleType ModuleType
        {

            get { return (ModuleType)GetValue(ModuleTypeDependencyProperty); }

            set { SetValue(ModuleTypeDependencyProperty, value); }
        }


        public bool IsSelected
        {
            get
            {

                return (bool)this.GetValue(IsSelectedDependencyProperty);
            }
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



        public ModelSystemStructureDisplayModel BackingModel
        {
            get { return (ModelSystemStructureDisplayModel)GetValue(BackingModelDependencyProperty); }

            set
            {
                SetValue(BackingModelDependencyProperty, value);
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
