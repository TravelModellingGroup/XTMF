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
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MaterialDesignThemes.Wpf;
using XTMF.Annotations;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls;

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

    public static readonly DependencyProperty CustomBackgroundDependencyProperty =
         DependencyProperty.Register("CustomBackground", typeof(Brush), typeof(ModuleTreeViewItem), new PropertyMetadata(Brushes.Transparent));

    /// <summary>
    /// 
    /// </summary>
    public Brush CustomBackground
    {
        get => (Brush)GetValue(CustomBackgroundDependencyProperty);
        set => SetValue(CustomBackgroundDependencyProperty, value);
    }


    public static readonly DependencyProperty ModelSystemTreeViewDisplayDependencyProperty =
        DependencyProperty.Register("ModelSystemTreeViewDisplay", typeof(ModelSystemTreeViewDisplay), typeof(ModuleTreeViewItem), new PropertyMetadata(null));

    /// <summary>
    /// 
    /// </summary>
    public ModelSystemTreeViewDisplay ModelSystemTreeViewDisplay
    {
        get
        {
            return (ModelSystemTreeViewDisplay)GetValue(ModelSystemTreeViewDisplayDependencyProperty);

        }
        set
        {
            SetValue(ModelSystemTreeViewDisplayDependencyProperty, value);
        }
    }

    public static ModuleTreeViewItem ActiveDragItem { get; set; }

    public static DataObject DragData { get; set; }

    public PackIconKind Icon { get; set; }


    /// <summary>
    /// 
    /// </summary>
    public ModuleTreeViewItem()
    {
        InitializeComponent();
        Loaded += ModuleTreeViewItem_Loaded;
        MouseMove += OnMouseMove;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Mouse.LeftButton == MouseButtonState.Pressed)
        {
            if (!ModelSystemTreeViewDisplay.IsDragActive && System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) == true)
            {
                DragData = new DataObject();

                DragData.SetData("drag", this);

                ModelSystemTreeViewDisplay.IsDragActive = true;
                ActiveDragItem = this;
                DragDrop.DoDragDrop(ActiveDragItem, DragData, DragDropEffects.Move);
            }
            else if (ModelSystemTreeViewDisplay.IsDragActive)
            {
                DragDrop.DoDragDrop(ActiveDragItem, DragData, DragDropEffects.Move);
                e.Handled = true;
            }

        }

    }

    /// <summary>
    /// 
    /// </summary>
    private void UpdateIcon()
    {
        var type = BackingModel.BaseModel.Type;
        //PackIcon.ShowMinorIcon = false;
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
                            iconKind = (PackIconKind)System.Enum.Parse(typeof(PackIconKind), info.IconURI);
                            PackIcon.ShowMinorIcon = false;
                            hasCustomIcon = true;
                        }
                        catch
                        {
                        }

                    }
                }
            }
        }

        if (BackingModel.BaseModel.IsMetaModule && !hasCustomIcon)
        {
            IconPath = new Path { Data = (PathGeometry)Application.Current.Resources["MetaModuleIconPath"] };
            PathBorder.Visibility = Visibility.Visible;
        }
        else if (BackingModel.BaseModel.IsMetaModule && hasCustomIcon)
        {
            IconPath = new Path
            {
                Data = (PathGeometry)Application.Current.Resources["ModuleIcon2Path"],
            };
            PackIcon.IconKind = iconKind;
            PackIcon.ShowMinorIcon = true;
            PackIcon.IconKindMinor = PackIconKind.SelectAll;
            PackIcon.IconKind = iconKind;
            IconPath.Visibility = Visibility.Hidden;
            PathBorder.Visibility = Visibility.Collapsed;
            PackIcon.Visibility = Visibility.Visible;

        }
        else if (!BackingModel.BaseModel.IsMetaModule && !BackingModel.BaseModel.IsCollection)
        {
            IconPath = new Path
            {
                Data = (PathGeometry)Application.Current.Resources["ModuleIcon2Path"],
            };
            if (hasCustomIcon)
            {
                PackIcon.IconKind = iconKind;
                IconPath.Visibility = Visibility.Hidden;
                PathBorder.Visibility = Visibility.Collapsed;
                PackIcon.Visibility = Visibility.Visible;

            }
            else
            {
                PackIcon.IconKind = PackIconKind.Settings;
                PackIcon.Visibility = Visibility.Collapsed;
                PathBorder.Visibility = Visibility.Visible;


            }

        }
        else if (BackingModel.BaseModel.IsCollection)
        {
            IconPath = new Path { Data = (PathGeometry)Application.Current.Resources["CollectionIconPath"] };
            PathBorder.Visibility = Visibility.Visible;
            //PackIcon.Kind = PackIconKind.ViewList;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ModuleTreeViewItem_Loaded(object sender, RoutedEventArgs e)
    {
        PackIcon.ShowMinorIcon = false;
        BackingModel.BaseModel.PropertyChanged += BaseModelOnPropertyChanged;
        BackingModel.PropertyChanged += BaseModelOnPropertyChanged;

        var type = BackingModel.BaseModel.Type;
        //PackIcon.ShowMinorIcon = false;
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
                            iconKind = (PackIconKind)System.Enum.Parse(typeof(PackIconKind), info.IconURI);
                            PackIcon.ShowMinorIcon = false;
                            hasCustomIcon = true;
                        }
                        catch
                        {
                        }

                    }
                }
            }
        }


        if (BackingModel.BaseModel.IsMetaModule && !hasCustomIcon)
        {
            IconPath = new Path { Data = (PathGeometry)Application.Current.Resources["MetaModuleIconPath"] };
            PathBorder.Visibility = Visibility.Visible;
        }
        else if (BackingModel.BaseModel.IsMetaModule && hasCustomIcon)
        {
            IconPath = new Path
            {
                Data = (PathGeometry)Application.Current.Resources["ModuleIcon2Path"],
            };
            PackIcon.IconKind = iconKind;
            PackIcon.ShowMinorIcon = true;
            PackIcon.IconKindMinor = PackIconKind.SelectAll;
            PackIcon.IconKind = iconKind;
            IconPath.Visibility = Visibility.Hidden;
            PathBorder.Visibility = Visibility.Collapsed;
            PackIcon.Visibility = Visibility.Visible;

        }
        else if (!BackingModel.BaseModel.IsMetaModule && !BackingModel.BaseModel.IsCollection)
        {
            IconPath = new Path
            {
                Data = (PathGeometry)Application.Current.Resources["ModuleIcon2Path"],
            };
            if (hasCustomIcon)
            {
                PackIcon.IconKind = iconKind;
                IconPath.Visibility = Visibility.Hidden;
                PathBorder.Visibility = Visibility.Collapsed;
                PackIcon.Visibility = Visibility.Visible;

            }
            else
            {
                PackIcon.IconKind = PackIconKind.Settings;
                PackIcon.Visibility = Visibility.Collapsed;
                PathBorder.Visibility = Visibility.Visible;


            }

        }
        else if (BackingModel.BaseModel.IsCollection)
        {
            IconPath = new Path { Data = (PathGeometry)Application.Current.Resources["CollectionIconPath"] };
            PathBorder.Visibility = Visibility.Visible;
            //PackIcon.Kind = PackIconKind.ViewList;
        }

        var amount = BackingModel.BaseModel.IsDisabled ? 0.4 : 1.0;
        SubTextLabel.Opacity = amount;
        Title.Opacity = amount;
        if (IconPath != null)
        {
            IconPath.Opacity = amount;
        }

        SetupColours();
        e.Handled = true;

        if (BackingModel != null)
        {
            BackingModel.ControlTreeViewItem = this;
        }

        var layer = AdornerLayer.GetAdornerLayer(this);
        if (layer != null)
        {
            DragDropAdorner moveAdorner = new(this);
            layer.Add(moveAdorner);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public List<ModuleTreeViewItem> GetSiblingModuleTreeViewItems()
    {
        var list = new List<ModuleTreeViewItem>();
        if (BackingModel.Parent != null)
        {
            foreach (var childModule in BackingModel.Parent.Children)
            {
                list.Add(childModule.ControlTreeViewItem);
            }
        }

        return list;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="propertyChangedEventArgs"></param>
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
                UpdateIcon();
                break;
            case "IsDisabled":
                if (BackingModel != null)
                {
                    var amount = BackingModel.BaseModel.IsDisabled ? 0.4 : 1.0;
                    SubTextLabel.Opacity = amount;
                    Title.Opacity = amount;
                    IconPath.Opacity = amount;
                }
                break;
            case "Type":
                TitleText = BackingModel.BaseModel.Name;
                SubText = BackingModel.BaseModel.Description;
                UpdateIcon();
                break;
            case "Name":
                TitleText = BackingModel.BaseModel.Name;
                break;
        }
        Dispatcher.Invoke(() =>
        {
            SetupColours();
        });
    }

    /// <summary>
    /// 
    /// </summary>
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
                PackIcon.IconKind = PackIconKind.AlertCircle;
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
            else if (BackingModel.Type == null && !BackingModel.IsCollection)
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

    /// <summary>
    /// 
    /// </summary>
    public ModuleType ModuleType
    {
        get => (ModuleType)GetValue(ModuleTypeDependencyProperty);
        set => SetValue(ModuleTypeDependencyProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
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

    /// <summary>
    /// 
    /// </summary>
    public Path IconPath
    {
        get => (Path)GetValue(IconPathDependencyProperty);
        set => SetValue(IconPathDependencyProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsBitmapIcon
    {
        get => (bool)GetValue(BitmapIconDependencyProperty);
        set => SetValue(BitmapIconDependencyProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public ModelSystemStructureDisplayModel BackingModel
    {
        get => (ModelSystemStructureDisplayModel)GetValue(BackingModelDependencyProperty);
        set => SetValue(BackingModelDependencyProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public bool IsPathIcon
    {
        get => (bool)GetValue(PathIconDependencyProperty);
        set => SetValue(PathIconDependencyProperty, value);
    }

    /// <summary>
    /// 
    /// </summary>
    public string TitleText
    {
        get => (string)GetValue(TitleTextDependencyProperty);
        set
        {
            SetValue(TitleTextDependencyProperty, value);
            Title.Text = value;
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ModuleTreeViewItem_OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        base.OnGiveFeedback(e);

        e.Handled = true;
    }
}

public enum ModuleType
{
    Optional, Meta, Required
}
