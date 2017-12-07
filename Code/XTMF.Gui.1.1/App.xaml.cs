using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using XTMF.Gui.Controllers;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for XtmfApplication.xaml
    /// </summary>
    public partial class App : Application
    {


        MainWindow xtmfMainWindow = null;

        private void RegisterEditorController()
        {
            EditorController.Register(xtmfMainWindow, () =>
            {

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    IEnumerable<Swatch> swatches = new SwatchesProvider().Swatches;

                    if (EditorController.Runtime.Configuration.PrimaryColour != null) 
                    {
                        Swatch swatch = swatches.First((s) => s.Name == EditorController.Runtime.Configuration.PrimaryColour);
                        new PaletteHelper().ReplacePrimaryColor(swatch);

                    }


                    if (EditorController.Runtime.Configuration.AccentColour != null)
                    {
                        Swatch swatch = swatches.First((s) => s.Name == EditorController.Runtime.Configuration.AccentColour);
                        new PaletteHelper().ReplaceAccentColor(swatch);

                    }

                    if (EditorController.Runtime.Configuration.IsDarkTheme)
                    {
                        new PaletteHelper().SetLightDark(true);
                    }

                    if (EditorController.Runtime.Configuration.Theme == null)
                    {
                        //xtmfMainWindow.ThemeController.SetThemeActive(xtmfMainWindow.ThemeController.GetDefaultTheme());
                    }
                    else
                    {
                          ThemeController.Theme theme =
                                xtmfMainWindow.ThemeController.FindThemeByName(EditorController.Runtime.Configuration.Theme);

                        //xtmfMainWindow.ThemeController.SetThemeActive(theme ?? xtmfMainWindow.ThemeController.GetDefaultTheme());
                    }
                    xtmfMainWindow.UpdateRecentProjectsMenu();

                    if (EditorController.Runtime.Configuration.IsDarkTheme)
                    {
                        new PaletteHelper().SetLightDark(true);
                    }
                    xtmfMainWindow.Show();

                    EditorController.Runtime.Configuration.LoadModules(() =>
                    {
                        xtmfMainWindow.IsEnabled = true;
                        xtmfMainWindow.StatusDisplay.Text = "Ready";
                    });
                   
                }));
             
           
                
            },loadModules:false);

        }


        private void App_OnStartup(object sender, StartupEventArgs e)
        {

            this.DispatcherUnhandledException += AppGlobalDispatcherUnhandledException;
     
            xtmfMainWindow = new MainWindow();
            RegisterEditorController();
                


          
        }
        private void AppGlobalDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
        }


      

    }


}
