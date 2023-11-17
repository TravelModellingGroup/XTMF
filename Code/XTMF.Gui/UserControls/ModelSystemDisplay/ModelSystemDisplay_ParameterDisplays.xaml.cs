using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using XTMF.Gui.Controllers;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls
{
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
        private void QuickParameterDisplay2_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (QuickParameterDisplaySearch.Opacity > 0 && e.Key == Key.Escape)
            {
                this.ToggleQuickParameterDisplaySearch();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleParameterDisplay_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ModuleParameterDisplaySearch.Opacity > 0 && e.Key == Key.Escape)
            {
                this.ToggleModuleParameterDisplaySearch();
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
            ToggleQuickParameterDisplaySearch();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleParameterDisplaySearchBackButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.ToggleModuleParameterDisplaySearch();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModuleParameterSearchButton_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            this.ToggleModuleParameterDisplaySearch();
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
        private void QuickParameterDisplayClose_MouseUp(object sender, MouseButtonEventArgs e)
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

        /// <summary>
        /// 
        /// </summary>
        private void ToggleQuickParameterDisplaySearch()
        {
            Action localAction = new(() =>
            {
                if (QuickParameterDisplaySearch.Opacity == 0.0)
                {
                    this.AnimateOpacity(QuickParameterDisplaySearch, 0, 1.0, QuickParameterFilterBox);
                    this.AnimateOpacity(QuickParameterDisplayHeader, 1.0, 0.0);
                }
                else
                {
                    this.AnimateOpacity(QuickParameterDisplaySearch, 1.0, 0.0);
                    this.AnimateOpacity(QuickParameterDisplayHeader, 0.0, 1.0);
                    QuickParameterFilterBox.Box.Text = "";
                }
            });
            if (!IsQuickParameterDisplayOpen())
            {
                ToggleQuickParameterDisplay(100, localAction);
            }
            else
            {
                localAction.Invoke();
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
            if (ModuleParameterDisplaySearch.Opacity == 0.0)
            {
                this.AnimateOpacity(ModuleParameterDisplaySearch, 0, 1.0, ParameterFilterBox);
                this.AnimateOpacity(ModuleParameterDisplayHeader, 1.0, 0.0);

            }
            else
            {
                this.AnimateOpacity(ModuleParameterDisplaySearch, 1.0, 0.0);
                this.AnimateOpacity(ModuleParameterDisplayHeader, 0.0, 1.0);
                ParameterFilterBox.Box.Text = "";
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
}
