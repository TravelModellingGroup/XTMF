using MaterialDesignThemes.Wpf;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using XTMF.Gui.Controllers;
using XTMF.Gui.Helpers;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Resources;

namespace XTMF.Gui;

/// <summary>
///     Interaction logic for XtmfApplication.xaml
/// </summary>
public partial class App : Application
{
    private MainWindow xtmfMainWindow;

    public const String APP_ID = "TMG.Xtmf";
    private static readonly ResourceManager ResManager = new("XTMF.Gui.Properties.Resources", typeof(App).Assembly);
    private void SetupXTMF()
    {
        EditorController.SetupRuntime();
    }

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
                var colourOptions = ThemeHelper.ColourOptions;
                if (EditorController.Runtime.Configuration.PrimaryColour != null)
                {
                    var swatch = colourOptions.FirstOrDefault(s => s.Name.Equals(EditorController.Runtime.Configuration.PrimaryColour, StringComparison.InvariantCultureIgnoreCase));
                    if (swatch != null)
                    {
                        ThemeHelper.SetThemePrimaryColour(new PaletteHelper(), swatch.Name, EditorController.Runtime.Configuration.IsDarkTheme);
                    }
                    else
                    {
                        ThemeHelper.SetThemePrimaryColour(new PaletteHelper(), "Blue", EditorController.Runtime.Configuration.IsDarkTheme); ;
                    }

                }
                else
                {
                    var swatch = colourOptions.First(s => s.Name.Equals("Blue", StringComparison.InvariantCultureIgnoreCase));
                    ThemeHelper.SetThemePrimaryColour(new PaletteHelper(), swatch.Name, EditorController.Runtime.Configuration.IsDarkTheme);
                    EditorController.Runtime.Configuration.PrimaryColour = swatch.Name;
                }


                if (EditorController.Runtime.Configuration.AccentColour != null)
                {
                    var swatch = colourOptions.FirstOrDefault(s => s.Name.Equals(EditorController.Runtime.Configuration.AccentColour, StringComparison.InvariantCultureIgnoreCase));
                    if (swatch != null)
                    {
                        ThemeHelper.SetThemeSecondaryColour(new PaletteHelper(), swatch.Name, EditorController.Runtime.Configuration.IsDarkTheme);
                    }
                    else
                    {
                        ThemeHelper.SetThemeSecondaryColour(new PaletteHelper(), "Amber", EditorController.Runtime.Configuration.IsDarkTheme);
                    }
                    ThemeHelper.SetThemeSecondaryColour(new PaletteHelper(), swatch.Name, EditorController.Runtime.Configuration.IsDarkTheme);
                    EditorController.Runtime.Configuration.AccentColour = swatch.Name;
                }
                else
                {
                    var swatch = colourOptions.First(s => s.Name.Equals("Amber", StringComparison.InvariantCultureIgnoreCase));
                    ThemeHelper.SetThemeSecondaryColour(new PaletteHelper(), swatch.Name, EditorController.Runtime.Configuration.IsDarkTheme);

                }

                TransitionAssist.SetDisableTransitions(Gui.MainWindow.Us, EditorController.Runtime.Configuration.IsDisableTransitionAnimations);
                xtmfMainWindow.UpdateRecentProjectsMenu();
                xtmfMainWindow.Show();
                var readyText = ResManager.GetString("StatusReady") ?? "Ready";
                var loadingText = ResManager.GetString("StatusLoadingXTMF") ?? "Loading XTMF";
                if (!EditorController.Runtime.Configuration.ModulesLoaded)
                {
                    Task.Run(() =>
                    {

                        EditorController.Runtime.Configuration.LoadModules(() =>
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                xtmfMainWindow.IsEnabled = true;
                                xtmfMainWindow.StatusDisplay.Text = readyText;
                            }));
                        });
                    });
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        xtmfMainWindow.IsEnabled = true;
                        xtmfMainWindow.StatusDisplay.Text = readyText;
                    }));
                }
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
        SetupXTMF();
        DispatcherUnhandledException += AppGlobalDispatcherUnhandledException;
        RegisterEditorController(e);
        xtmfMainWindow = new MainWindow();
        SetupXTMFErrorCallbacks(xtmfMainWindow);
    }

    private void SetupXTMFErrorCallbacks(MainWindow xtmfMainWindow)
    {
        EditorController.SetupXTMFCallbacks(xtmfMainWindow);
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