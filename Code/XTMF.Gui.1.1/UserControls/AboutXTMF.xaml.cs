using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IOPath = System.IO.Path;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for AboutXTMF.xaml
    /// </summary>
    public partial class AboutXTMF : Window
    {
        public AboutXTMF()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            VersionBlock.Text = GetVersionText();
        }

        private string GetVersionText()
        {
            var assemblyLocation = Assembly.GetEntryAssembly().CodeBase.Replace("file:///", "");
            var licenseFile = IOPath.Combine(IOPath.GetDirectoryName(assemblyLocation), "license.txt");
            var versionFile = IOPath.Combine(IOPath.GetDirectoryName(assemblyLocation), "version.txt");
            try
            {
                using (StreamReader reader = new StreamReader(versionFile))
                {
                    return reader.ReadLine();
                }
            }
            catch
            {
                return "Unknown Version";
            }
        }

        private void TextBlock_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("http://tmg.utoronto.ca/Default.aspx");
        }
    }
}
