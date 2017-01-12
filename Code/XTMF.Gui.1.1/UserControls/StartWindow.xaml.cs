using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for StartWindow.xaml
    /// </summary>
    public partial class StartWindow : UserControl
    {

        private List<Label> recentProjectLabels = new List<Label>();
        public StartWindow()
        {
            InitializeComponent();

            MainWindow.Us.RecentProjectsUpdated += Us_RecentProjectsUpdated;
            

           if(MainWindow.Us.RuntimeAvailable)
            {
                Us_RecentProjectsUpdated(null, null);
            }


        }

        private void Us_RecentProjectsUpdated(object sender, EventArgs e)
        {
            var k = Application.Current.FindResource("HoverLabel");


            if (RecentProjectsStackPanel.Children.Count != MainWindow.Us.RecentProjects.Count)
            {
                RecentProjectsStackPanel.Children.Clear();


                foreach (var recentProject in MainWindow.Us.RecentProjects)
                {
                    Label x = new Label();
                    x.Content = recentProject;
                    x.Style = (Style)k;

                    x.MouseDown += (senderc, EventArgs) =>
                    {
                        MainWindow.Us.LoadProjectByName(recentProject);
                    };

                    RecentProjectsStackPanel.Children.Add(x);
                }
            }
        }

        private void RecentProjectMouseDown(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void NewProject_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.NewProject();
        }

        private void OpenProject_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.OpenProject();
        }

        private void NewModelSystem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.NewModelSystem();
        }

        private void OpenModelSystem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Us.OpenModelSystem();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
           
        }
    }
}
