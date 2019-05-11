using System;
using System.Linq;
using System.Threading.Tasks;
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
                    var swatches = new SwatchesProvider().Swatches.ToList();
                    var paletteHelper = new PaletteHelper();

                    if (EditorController.Runtime.Configuration.PrimaryColour != null)
                    {
                        var swatch = swatches.First(s => s.Name.Equals(EditorController.Runtime.Configuration.PrimaryColour, StringComparison.InvariantCultureIgnoreCase));
                        paletteHelper.ReplacePrimaryColor(swatch);
                    }
                    else
                    {
                        var swatch = swatches.First(s => s.Name.Equals("blue", StringComparison.InvariantCultureIgnoreCase));
                        paletteHelper.ReplacePrimaryColor(swatch);
                        EditorController.Runtime.Configuration.PrimaryColour = swatch.Name;
                    }


                    if (EditorController.Runtime.Configuration.AccentColour != null)
                    {
                        var swatch = swatches.First(s => s.Name.Equals(EditorController.Runtime.Configuration.AccentColour, StringComparison.InvariantCultureIgnoreCase));
                        paletteHelper.ReplaceAccentColor(swatch);
                        EditorController.Runtime.Configuration.AccentColour = swatch.Name;
                    }
                    else
                    {
                        var swatch = swatches.First(s => s.Name.Equals("amber", StringComparison.InvariantCultureIgnoreCase));
                        paletteHelper.ReplaceAccentColor(swatch);
                    }
                    // Setting this to true enables the dark theme
                    paletteHelper.SetLightDark(EditorController.Runtime.Configuration.IsDarkTheme);


                    if (EditorController.Runtime.Configuration.IsDisableTransitionAnimations)
                    {
                        TransitionAssist.SetDisableTransitions(Gui.MainWindow.Us, false);
                    }
                    xtmfMainWindow.UpdateRecentProjectsMenu();
                    xtmfMainWindow.Show();
                    Task.Run(() =>
                    {
                        EditorController.Runtime.Configuration.LoadModules(() =>
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                xtmfMainWindow.IsEnabled = true;
                                xtmfMainWindow.StatusDisplay.Text = "Ready";
                            }));
                        });
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