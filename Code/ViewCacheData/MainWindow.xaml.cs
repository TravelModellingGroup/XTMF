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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Datastructure;

namespace ViewCacheData;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DBox_TextChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void OBox_TextChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var open = new Microsoft.Win32.OpenFileDialog();
        open.Filter = "OD Cache (.odc)|*.odc|Zone File Cache (.zfc)|.zfc";
        open.FilterIndex = 0;
        open.CheckPathExists = true;
        open.CheckFileExists = true;
        open.AddExtension = true;
        open.DereferenceLinks = true;
        if ( open.ShowDialog() == true )
        {
            var fileName = open.FileName;
            var ext = System.IO.Path.GetExtension( fileName )?.ToLowerInvariant();
            switch ( ext )
            {
                case ".zfc":
                    {
                        OdControlGrid.IsEnabled = false;
                        Task t = new( delegate
                        {
                            PresentationGrid.DataContext = null;
                            try
                            {
                                var odc = new OdCache( fileName );
                                odc.Release();
                            }
                            catch
                            {
                                MessageBox.Show( this, "Unable to load the file", "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Error );
                            }
                        } );
                        t.Start();
                    }
                    break;

                case ".odc":
                    {
                        OdControlGrid.IsEnabled = true;
                        Task t = new( delegate
                        {
                            try
                            {
                                var odc = new OdCache( fileName );
                                var allData = odc.StoreAll().GetFlatData();
                                Dispatcher.BeginInvoke(
                                    new Action( delegate
                                    {
                                        try
                                        {
                                            PresentationGrid.Items.Clear();
                                            for ( int i = 0; i < 1; i++ )
                                            {
                                                for ( int j = 0; j < 1; j++ )
                                                {
                                                    PresentationGrid.ItemsSource = allData[i][j];
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            MessageBox.Show( this, "Unable to display the data", "Loading Fail", MessageBoxButton.OK, MessageBoxImage.Error );
                                        }
                                    } ) );
                                odc.Release();
                            }
                            catch
                            {
                                MessageBox.Show( this, "Unable to load the file", "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Error );
                            }
                        } );
                        t.Start();
                    }
                    break;

                default:
                    MessageBox.Show( this, "Unable to load the extension " + ext + ".", "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Error );
                    break;
            }
        }
    }
}