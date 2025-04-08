using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls;

/// <summary>
/// Partial class implementation for handlers and functions related to the parameter and quick parameter display.
/// </summary>
public partial class ModelSystemDisplay
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ParameterDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((ListView)sender).SelectedItem is ParameterDisplayModel s)
        {
            _selectedParameterDisplayModel = s;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void QuickParameterDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((ListView)sender).SelectedItem is ParameterDisplayModel s)
        {
            _selectedParameterDisplayModel = s;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void QuickParameterListView_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        SoftActiveParameterDisplay = QuickParameterListView.SelectedItem as ParameterDisplayModel;
        var listView = e.Source as ListView;
        foreach (var item in listView.ContextMenu.Items)
        {
            var menuItem = item as FrameworkElement;
            if (menuItem.Name == "SelectFileMenuItem" || menuItem.Name == "SelectDirectoryMenuItem")
            {
                menuItem.IsEnabled = !SoftActiveParameterDisplay.IsEnumeration && SoftActiveParameterDisplay.ParameterType != typeof(bool);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ParameterDisplay_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        SoftActiveParameterDisplay = ParameterDisplay.SelectedItem as ParameterDisplayModel;
        var listView = e.Source as ListView;
        foreach (var item in listView.ContextMenu.Items)
        {
            var menuItem = item as FrameworkElement;
            if (menuItem.Name == "PSelectFileMenuItem" || menuItem.Name == "PSelectDirectoryMenuItem"
                || menuItem.Name == "POpenFileMenuItem")
            {
                menuItem.IsEnabled = !SoftActiveParameterDisplay.IsEnumeration && SoftActiveParameterDisplay.ParameterType != typeof(bool);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void QuickParameterDisplaySearchBackButton_OnClick(object sender, RoutedEventArgs e)
    {
        SetQuickParaemterDisplaySearch(true);
    }

    private void ModuleParameterBackButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleModuleParameterDisplaySearch();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ModuleParameterSearchButton_Click(object sender, RoutedEventArgs e)
    {
        this.ToggleModuleParameterDisplaySearch();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ModuleParameterDisplayClose_Click(object sender, RoutedEventArgs e)
    {
        this.ToggleModuleParameterDisplay();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void QuickParameterToolbarToggle_OnClick(object sender, RoutedEventArgs e)
    {
        this.ToggleQuickParameterDisplay();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ModuleParametersToolbarToggle_OnClick(object sender, RoutedEventArgs e)
    {
        this.ToggleModuleParameterDisplay();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void QuickParameterDisplayClose_Click(object sender, RoutedEventArgs e)
    {
        this.ToggleQuickParameterDisplay();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ModuleParameterDisplayClose_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        this.ToggleModuleParameterDisplay();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void QuickParameterSearchButton_Click(object sender, RoutedEventArgs e)
    {
        this.ToggleQuickParameterDisplaySearch();
    }

    private void FocusQuickParameterDisplaySearch()
    {
        // Make sure the quick parameter display is open
        SetQuickParaemterDisplaySearch(true);
        Keyboard.Focus(QuickParameterFilterBox.Box);
    }

    private void FocusModuleParameterDisplaySearch()
    {
        SetModuleParameterDisplaySearch(true);
        Keyboard.Focus(ParameterFilterBox.Box);
    }

    /// <summary>
    /// 
    /// </summary>
    private void ToggleQuickParameterDisplaySearch()
    {
        var isVisable = IsQuickParameterDisplayOpen();
        SetQuickParaemterDisplaySearch(isVisable);
        if (!isVisable)
        {
            QuickParameterFilterBox.Box.Text = "";
        }
    }

    private void SetQuickParaemterDisplaySearch(bool visable)
    {
        var alreadyVisable = QuickParameterDisplaySearch.Opacity > 0.0;
        if (visable && !alreadyVisable)
        {
            this.AnimateOpacity(QuickParameterDisplaySearch, 0, 1.0, QuickParameterFilterBox);
            this.AnimateOpacity(QuickParameterDisplayHeader, 1.0, 0.0);
        }
        else if (!visable)
        {
            this.AnimateOpacity(QuickParameterDisplaySearch, 1.0, 0.0);
            this.AnimateOpacity(QuickParameterDisplayHeader, 0.0, 1.0);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private bool IsQuickParameterDisplayOpen()
    {
        return ContentDisplayGrid.ColumnDefinitions[2].ActualWidth > 0;
    }

    /// <summary>
    /// 
    /// </summary>
    private void ToggleModuleParameterDisplaySearch()
    {
        var isVisable = ModuleParameterDisplaySearch.Opacity > 0.0;
        SetModuleParameterDisplaySearch(!isVisable);
        if (!isVisable)
        {
            ParameterFilterBox.Box.Text = "";
        }
    }

    private void SetModuleParameterDisplaySearch(bool visable)
    {
        var alreadyVisable = ModuleParameterDisplaySearch.Opacity > 0.0;
        if (visable && !alreadyVisable)
        {
            AnimateOpacity(ModuleParameterDisplaySearch, 0, 1.0, ParameterFilterBox);
            AnimateOpacity(ModuleParameterDisplayHeader, 1.0, 0.0);
        }
        else if(!visable)
        {
            AnimateOpacity(ModuleParameterDisplaySearch, 1.0, 0.0);
            AnimateOpacity(ModuleParameterDisplayHeader, 0.0, 1.0);
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ModuleParameterDialogHost_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ModuleParameterDialogHost.IsOpen = false;
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void QuickParameterDialogHost_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            QuickParameterDialogHost.IsOpen = false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void StandardParameterTemplateTextBox_OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                var parameterDisplayModel = ((TextBox)sender).Tag as ParameterDisplayModel;

                string path = files[0];

                GetInputDirectory(Session.GetModelSystemStructureModel(DisplayRoot.BaseModel.RealModelSystemStructure), out var inputDirectory);

                string inputDirectoryString = inputDirectory.Value;

                TransformToRelativePath(inputDirectoryString, ref path);
                SetParameterValue(parameterDisplayModel, path);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void StandardParameterTemplateTextBox_OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }
        var parameterDisplayModel = ((TextBox)sender).Tag as ParameterDisplayModel;
        if (parameterDisplayModel?.ParameterType != typeof(int))
        {
            e.Handled = true;
        }
    }
}
