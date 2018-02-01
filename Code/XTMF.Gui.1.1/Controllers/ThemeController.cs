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
            new Uri("Resources/ComboBoxStyle.xaml", UriKind.RelativeOrAbsolute),
         
        };

        public class Theme
        {
            public string Name;
            public string ThemeFile;
            public ResourceDictionary ThemeResourceDictionary;

            public Theme(string name, string themeFile, ResourceDictionary tDictionary)
            {
                Name = name;
                ThemeFile = themeFile;
                ThemeResourceDictionary = tDictionary;
            }
        }

        internal List<Theme> Themes { get; } = new List<Theme>();

        private string _Configuration;

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
            return;
        }

        private void ClearThemeDictionaries()
        {
            foreach (var d in Themes)
            {
                Application.Current.Resources.MergedDictionaries.Remove(d.ThemeResourceDictionary);
            }
        }

        public Theme GetDefaultTheme()
        {
            return Themes[0];
        }

        public void SetThemeActive(Theme theme)
        {
            Application.Current.Resources.MergedDictionaries.RemoveAt(Application.Current.Resources.MergedDictionaries.Count - 1);
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
                //MainWindow.Us.DockManager.Theme = themeObj;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public Theme FindThemeByName(string name) => Themes.Find(t => t.Name == name);

        /// <summary>
        /// Default theme loading, resources are pulled from application resources
        /// and not from file. Loads the default (light) theme.
        /// </summary>
        private void LoadDefaultLightTheme()
        {
          
        }

        private void LoadDefaultWarmTheme()
        {

        }

        private void LoadDefaultForestTheme()
        {
           
        }

        public ThemeController(string configuration)
        {
            /* Load themes from Configuration and search for themes in the current configuration
             * directory .*/
            _Configuration = Path.Combine(configuration, "Themes");
            LoadDefaultDarkTheme();
            LoadDefaultLightTheme();
            LoadDefaultWarmTheme();
            LoadDefaultForestTheme();
            if (!Directory.Exists(_Configuration))
            {
                return;
            }
            foreach (var file in Directory.EnumerateFiles(_Configuration))
            {
                if (file.EndsWith(".thm"))
                {
                    try
                    {
                        var themeDictionary = XamlReader.Load(new FileStream(file, FileMode.Open)) as ResourceDictionary;
                        //ResourceDictionary dictionary = (ResourceDictionary)Application.LoadComponent(themeUri);
                        Theme theme = new Theme(themeDictionary["ThemeName"].ToString(), file, themeDictionary);
                        Themes.Add(theme);
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
