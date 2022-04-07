using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XTMF.Gui.Controllers;
using XTMF.Gui.Models;

namespace XTMF.Gui.UserControls
{
    public partial class ModelSystemDisplay
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled == false)
            {
                if (EditorController.IsShiftDown() && EditorController.IsControlDown())
                {
                    switch (e.Key)
                    {
                        case Key.F:
                            if (QuickParameterDisplay2.IsEnabled)
                            {
                                QuickParameterDialogHost.IsOpen = true;
                            }
                            else if (ModuleParameterDisplay.IsEnabled)
                            {
                                ModuleParameterDialogHost.IsOpen = true;
                            }
                            break;
                    }
                }

                if (EditorController.IsControlDown())
                {
                    switch (e.Key)
                    {
                        case Key.M:
                            if (EditorController.IsAltDown())
                            {
                                //SetMetaModuleStateForSelected(false);
                            }
                            else if (EditorController.IsShiftDown())
                            {
                                //SetMetaModuleStateForSelected(true);
                            }
                            else
                            {
                                SelectReplacement();
                            }

                            e.Handled = true;
                            break;
                        case Key.R:
                            e.Handled = true;
                            break;
                        case Key.P:
                            ModuleParameterDisplay.Focus();
                            Keyboard.Focus(ParameterFilterBox);
                            e.Handled = true;
                            break;
                        case Key.E:
                            FilterBox.Focus();
                            e.Handled = true;
                            break;
                        case Key.W:
                            Close();
                            e.Handled = true;
                            break;
                        case Key.L:
                            ShowLinkedParameterDialog(true);
                            e.Handled = true;
                            break;
                        case Key.N:
                            CopyParameterName();
                            e.Handled = true;
                            break;
                        case Key.O:
                            OpenParameterFileLocation(false, false);
                            e.Handled = true;
                            break;
                        case Key.F:
                            SelectFileForCurrentParameter();
                            e.Handled = true;
                            break;
                        case Key.D:
                            if (ModuleParameterDisplay.IsKeyboardFocusWithin || QuickParameterDisplay2.IsKeyboardFocusWithin)
                            {
                                SelectDirectoryForCurrentParameter();
                            }
                            e.Handled = true;
                            break;
                        case Key.Z:
                            Undo();
                            e.Handled = true;
                            break;
                        case Key.Y:
                            Redo();
                            e.Handled = true;
                            break;
                        case Key.C:
                            CopyCurrentModule();
                            e.Handled = true;
                            break;
                        case Key.V:
                            PasteCurrentModule();
                            e.Handled = true;
                            break;
                        case Key.Q:
                            if (EditorController.IsShiftDown())
                            {
                                ToggleQuickParameterDisplay();
                                e.Handled = true;
                            }
                            else
                            {
                                ToggleQuickParameterDisplaySearch();

                                e.Handled = true;
                                break;
                            }
                            break;
                    }
                }
                else
                {
                    switch (e.Key)
                    {
                        case Key.F2:
                            if (EditorController.IsShiftDown())
                            {
                                RenameDescription();
                            }
                            else
                            {
                                RenameParameter();
                            }
                            e.Handled= true;
                            break;
                        case Key.F1:
                            ShowDocumentation();
                            e.Handled = true;
                            break;
                        case Key.Delete:
                            if ((ModelSystemDisplayContent.Content as UserControl).IsKeyboardFocusWithin)
                            {
                                RemoveSelectedModules();
                                e.Handled = true;
                            }
                            break;
                        case Key.F5:
                            e.Handled = true;
                            SaveCurrentlySelectedParameters();
                            ExecuteRun();
                            break;
                        case Key.Escape:
                            FilterBox.Box.Text = string.Empty;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModelSystemDisplay_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && e.KeyboardDevice.IsKeyDown(Key.LeftCtrl))
            {
                SaveRequested(false);
                e.Handled = true;
            }
        }
        /// <summary>
        /// PreviewKeyDown Listener for this control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="keyEventArgs"></param>
        private void UsOnPreviewKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.Key == Key.F5 && IsKeyboardFocusWithin && !LinkedParameterDisplayOverlay.IsVisible)
            {
                SaveCurrentlySelectedParameters();
                ExecuteRun();
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void HandleKeyPreviewDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && e.KeyboardDevice.IsKeyDown(Key.LeftCtrl))
            {
                SaveRequested(false);
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                SaveCurrentlySelectedParameters();
                ExecuteRun();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnassignedModulesStatusBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            List<ModelSystemStructureDisplayModel> modules = new List<ModelSystemStructureDisplayModel>();

            Dispatcher.BeginInvoke(new Action(() =>
              {
                  this.EnumerateUnassignedRequiredModules(DisplayRoot, modules);
                  if (modules.Count > 0)
                  {
                      this.UnassignedModulesList.ItemsSource = modules;
                      UnassignedModulesPopup.IsOpen = true;
                  }
                  else
                  {
                      NoUnassignedModulesPopup.IsOpen = true;
                  }
              }));

            e.Handled = true;
            return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisabledModulesStatusBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            List<ModelSystemStructureDisplayModel> modules = DisabledModules.ToList();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (modules.Count > 0)
                {
                    this.DisabledModulesPopupList.ItemsSource = modules;
                    DisabledModulesPopup.IsOpen = true;
                }
            }));

            e.Handled = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnassignedModuleLabel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)e.Source).Tag is ModelSystemStructureDisplayModel model)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    model.IsSelected = true;
                    if (ActiveModelSystemView is ModelSystemTreeViewDisplay view)
                    {
                        view.ExpandToRoot(model);
                    }
                    UnassignedModulesPopup.IsOpen = false;
                    NoUnassignedModulesPopup.IsOpen = false;
                    DisabledModulesPopup.IsOpen = false;
                }));
            }
        }
    }
}
