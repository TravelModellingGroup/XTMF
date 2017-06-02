using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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


            if (MainWindow.Us.RuntimeAvailable)
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

                    x.PreviewMouseUp += (senderc, EventArgs) =>
                    {
                            MainWindow.Us.LoadProjectByName(recentProject);
                        
                    };

                    RecentProjectsStackPanel.Children.Add(x);
                }
            }
        }

     

        private void NewProject_MouseUp(object sender, MouseButtonEventArgs e)
        {

                e.Handled = true;
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

        
    }
}
