using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for XtmfApplication.xaml
    /// </summary>
    public partial class App : Application
    {

    


        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var xtmfMainWindow = new MainWindow();


            xtmfMainWindow.Show();
        }
    }


}
