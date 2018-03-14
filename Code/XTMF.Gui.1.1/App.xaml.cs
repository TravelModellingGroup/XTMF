using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using MaterialDesignThemes.Wpf.Transitions;
using XTMF.Gui.Controllers;

namespace XTMF.Gui
{
    /// <summary>
    ///     Interaction logic for XtmfApplication.xaml
    /// </summary>
    public partial class App : Application
    {
        private MainWindow xtmfMainWindow;

        public const String APP_ID = "TMG.Xtmf";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        private void RegisterEditorController(StartupEventArgs args)
        {
            EditorController.Register(xtmfMainWindow, () =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var swatches = new SwatchesProvider().Swatches;

                    if (EditorController.Runtime.Configuration.PrimaryColour != null)
                    {
                        var swatch = swatches.First(s =>
                            s.Name == EditorController.Runtime.Configuration.PrimaryColour);
                        new PaletteHelper().ReplacePrimaryColor(swatch);
                    }


                    if (EditorController.Runtime.Configuration.AccentColour != null)
                    {
                        var swatch = swatches.First(s => s.Name == EditorController.Runtime.Configuration.AccentColour);
                        new PaletteHelper().ReplaceAccentColor(swatch);
                    }

                    if (EditorController.Runtime.Configuration.IsDarkTheme) new PaletteHelper().SetLightDark(true);

                    if (EditorController.Runtime.Configuration.IsDisableTransitionAnimations)
                        TransitionAssist.SetDisableTransitions(Gui.MainWindow.Us, false);


                    if (EditorController.Runtime.Configuration.Theme == null)
                    {
                        //xtmfMainWindow.ThemeController.SetThemeActive(xtmfMainWindow.ThemeController.GetDefaultTheme());
                    }
                    else
                    {
                        var theme =
                            xtmfMainWindow.ThemeController.FindThemeByName(EditorController.Runtime.Configuration
                                .Theme);

                        //xtmfMainWindow.ThemeController.SetThemeActive(theme ?? xtmfMainWindow.ThemeController.GetDefaultTheme());
                    }

                    xtmfMainWindow.UpdateRecentProjectsMenu();

                    if (EditorController.Runtime.Configuration.IsDarkTheme) new PaletteHelper().SetLightDark(true);
                    xtmfMainWindow.Show();
                    if (args.Args.Contains("--remote-host"))
                    {
                        //use remote host even though a debugger may be attached. 
                        EditorController.Runtime.Configuration.RemoteHost = true;

                    }

                    EditorController.Runtime.Configuration.LoadModules(() =>
                    {
                        xtmfMainWindow.IsEnabled = true;
                        xtmfMainWindow.StatusDisplay.Text = "Ready";
                    });
                }));
            }, false);
           
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            DispatcherUnhandledException += AppGlobalDispatcherUnhandledException;

            xtmfMainWindow = new MainWindow();
            RegisterEditorController(e);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AppGlobalDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
        }
    }
}