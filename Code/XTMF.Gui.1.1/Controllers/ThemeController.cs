using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using Xceed.Wpf.AvalonDock.Themes;

namespace XTMF.Gui.Controllers
{



    public class ThemeController
    {
        private static readonly Uri[] RequiredResources =
        {
            new Uri("Resources/ControlStyles.xaml", UriKind.RelativeOrAbsolute),
            new Uri("Resources/ComboBoxStyle.xaml", UriKind.RelativeOrAbsolute),
           new Uri("UserControls/MenuStyles.xaml", UriKind.RelativeOrAbsolute)
        };

        public class Theme
        {
            public string Name;
            public string ThemeFile;
             public ResourceDictionary ThemeResourceDictionary;

            public Theme(string name, string themeFile, ResourceDictionary tDictionary)
            {
                this.Name = name;
                this.ThemeFile = themeFile;
                this.ThemeResourceDictionary = tDictionary;
            }
        }

        private readonly List<Theme> _themes;

        private string _configuration;

        public List<Theme> Themes
        {
            get { return _themes; }
        }

        public void LoadTheme(string theme)
        {

            Application.Current.Resources.MergedDictionaries.Clear();
            Uri NewTheme = new Uri(theme + ".thm", UriKind.Relative);
            ResourceDictionary dictionary = (ResourceDictionary)Application.LoadComponent(NewTheme);
            Application.Current.Resources.MergedDictionaries.Add(dictionary);
        }

        /// <summary>
        /// Default theme loading, resources are pulled from application resources
        /// and not from file. Loads the default (dark) theme.
        /// </summary>
        private void LoadDefaultDarkTheme()
        {

            ClearThemeDictionaries();


            Uri NewTheme = new Uri("/XTMF.Gui;component/Resources/DarkTheme.xaml", UriKind.RelativeOrAbsolute);
            ResourceDictionary dictionary = (ResourceDictionary)Application.LoadComponent(NewTheme);
            dictionary.Source = NewTheme;


            this._themes.Add(new Theme("Dark Theme Default", NewTheme.OriginalString, dictionary));


        }

        private void ClearThemeDictionaries()
        {



            foreach (var d in _themes)
            {
                Application.Current.Resources.MergedDictionaries.Remove(d.ThemeResourceDictionary);
            }
        }

        public Theme GetDefaultTheme()
        {
            return _themes[0];
        }

        public void SetThemeActive(Theme theme)
        {


            Application.Current.Resources.MergedDictionaries.RemoveAt(Application.Current.Resources.MergedDictionaries.Count-1);



            Application.Current.Resources.MergedDictionaries.Add(theme.ThemeResourceDictionary);

            

            foreach (var uri in RequiredResources)
            {
                Application.Current.Resources.MergedDictionaries.RemoveAt(0);
                   Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(uri));

            }

            /* Look up Avalon Theme if present */
            if (!theme.ThemeResourceDictionary.Contains("AvalonDockTheme"))
            {
                return;
            }
            try
            {


                string themeClass = (string)theme.ThemeResourceDictionary["AvalonDockTheme"];

                Type themeType = Type.GetType(themeClass);


                Xceed.Wpf.AvalonDock.Themes.Theme themeObj = (Xceed.Wpf.AvalonDock.Themes.Theme)Activator.CreateInstance(themeType);
                MainWindow.Us.DockManager.Theme = themeObj;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
        }

        public Theme FindThemeByName(string name)
        {
            return _themes.Find(t => t.Name == name);
        }

        /// <summary>
        /// Default theme loading, resources are pulled from application resources
        /// and not from file. Loads the default (light) theme.
        /// </summary>
        private void LoadDefaultLightTheme()
        {
            ClearThemeDictionaries();


            Uri NewTheme = new Uri("/XTMF.Gui;component/Resources/LightTheme.xaml", UriKind.RelativeOrAbsolute);
            ResourceDictionary dictionary = (ResourceDictionary)Application.LoadComponent(NewTheme);
            dictionary.Source = NewTheme;

            this._themes.Add(new Theme("Light Theme Default", NewTheme.OriginalString, dictionary));


        }

        public ThemeController(string configuration)
        {

            /* Load themes from Configuration and search for themes in the current configuration
             * directory .*/

            this._configuration = Path.Combine(configuration, "Themes");
            _themes = new List<Theme>();

            LoadDefaultDarkTheme();
            LoadDefaultLightTheme();

            if (!Directory.Exists(_configuration))
            {
                return;
            }
            foreach (var file in Directory.EnumerateFiles(_configuration))
            {

                if (file.EndsWith(".thm"))
                {

                    try
                    {
                        var themeDictionary = XamlReader.Load(new FileStream(file, FileMode.Open)) as ResourceDictionary;
                        //ResourceDictionary dictionary = (ResourceDictionary)Application.LoadComponent(themeUri);
                        Theme theme = new Theme(themeDictionary["ThemeName"].ToString(), file, themeDictionary);
                        _themes.Add(theme);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }


                }
            }
        }
    }
}
