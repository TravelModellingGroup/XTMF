/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace XTMFUpdate
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int offset = 0;

        private float scale = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Update(float percent)
        {
            this.Progress.Dispatcher.Invoke( new Action( delegate() { this.Progress.Value = ( percent * 100 / scale ) + offset; } ) );
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            this.UpdateButton.IsEnabled = false;
            Task download = new Task( delegate()
                {
                    try
                    {
                        XTMFUpdateController controller = new XTMFUpdateController();
                        this.Dispatcher.Invoke( new Action( delegate()
                            {
                                try
                                {
                                    controller.XTMFDirectory = Directory.GetCurrentDirectory();
                                    controller.XTMFUpdateServerPort = int.Parse( this.PortBox.Text );
                                    controller.XTMFUpdateServerLocation = this.ServerAddressBox.Text;
                                }
                                catch
                                {
                                }
                            } ) );
                        offset = 0;
                        scale = 0.5f;
                        controller.UpdateCore( Update );
                        offset = 50;
                        controller.UpdateModules( Update );
                        MessageBox.Show( "Update Complete!" );
                        this.Dispatcher.Invoke( new Action( delegate() { this.Close(); } ) );
                    }
                    catch ( AggregateException error )
                    {
                        if ( error.InnerException == null )
                        {
                            MessageBox.Show( error.Message );
                        }
                        else
                        {
                            MessageBox.Show( error.InnerException.Message );
                        }
                    }
                    catch ( Exception error )
                    {
                        MessageBox.Show( error.Message );
                    }
                    this.Dispatcher.Invoke( new Action( delegate() { this.UpdateButton.IsEnabled = true; } ) );
                } );
            try
            {
                download.Start();
            }
            catch ( Exception error )
            {
                MessageBox.Show( error.Message );
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.PortBox.Text = "1448";
            this.ServerAddressBox.Text = "128.100.14.206";
        }
    }
}