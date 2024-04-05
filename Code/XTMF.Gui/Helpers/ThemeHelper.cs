using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MaterialDesignColors.ColorManipulation;
using ControlzEx.Theming;
using System.Windows.Forms;
using XTMF.Gui.UserControls;

namespace XTMF.Gui.Helpers
{
    public static class ThemeHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="color"></param>
        public static void SetThemePrimaryColour(PaletteHelper paletteHelper, string schemeName, bool isDark)
        {
            ITheme theme = paletteHelper.GetTheme();
            var color = GetThemeColor(schemeName);
            theme.PrimaryLight = new ColorPair(color.Lighten());
            theme.PrimaryMid = new ColorPair(color);
            theme.PrimaryDark = new ColorPair(color.Darken());
            if (schemeName == null)
            {
                schemeName = "Blue";
            }
            paletteHelper.SetTheme(theme);
            SetDarkTheme(isDark, schemeName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isDarkTheme"></param>
        /// <param name="schemeName"></param>
        public static void SetDarkTheme(bool isDarkTheme, string schemeName)
        {
            if (isDarkTheme)
            {
               ThemeManager.Current.ChangeTheme(System.Windows.Application.Current, "Dark." + schemeName);
                PaletteHelper _paletteHelper = new PaletteHelper();
                ITheme theme = _paletteHelper.GetTheme();
                IBaseTheme baseTheme = new MaterialDesignDarkTheme();
                theme.SetBaseTheme(baseTheme);
                _paletteHelper.SetTheme(theme);
            }
            else
            {
                ThemeManager.Current.ChangeTheme(System.Windows.Application.Current, "Light." + schemeName);
                PaletteHelper _paletteHelper = new PaletteHelper();
                ITheme theme = _paletteHelper.GetTheme();
                IBaseTheme baseTheme = new MaterialDesignLightTheme();
                theme.SetBaseTheme(baseTheme);
                _paletteHelper.SetTheme(theme);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="color"></param>
        public static void SetThemeSecondaryColour(PaletteHelper paletteHelper, string schemeName, bool isDark)
        {
            ITheme theme = paletteHelper.GetTheme();
            var color = GetThemeColor(schemeName);
            theme.SecondaryLight = new ColorPair(color.Lighten());
            theme.SecondaryMid = new ColorPair(color);
            theme.SecondaryDark = new ColorPair(color.Darken());
            paletteHelper.SetTheme(theme);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="schemeName"></param>
        /// <returns></returns>
        public static Color GetThemeColor(string schemeName)
        {
            var scheme = ThemeManager.Current.Themes.FirstOrDefault(x => x.ColorScheme == schemeName);
            if (scheme != null)
            {
                return scheme.PrimaryAccentColor;
            }
            else
            {
                return ThemeManager.Current.Themes.First().PrimaryAccentColor;
            }
        }

        private static List<ColourOption> _colourOptions = null;

        /// <summary>
        /// Returns a list of colour options available to the program.
        /// </summary>
        public static List<ColourOption> ColourOptions
        {
            get
            {
                if (_colourOptions != null)
                {
                    return _colourOptions;
                }
                _colourOptions = [];
                foreach (var colour in ThemeManager.Current.ColorSchemes)
                {
                    _colourOptions.Add(new ColourOption()
                    {
                        Name = colour,
                        Colour = ThemeHelper.GetThemeColor(colour)
                    }); ;
                }

                return _colourOptions;

            }
        }

    }
}
