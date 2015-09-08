/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Threading;
using System.Text.RegularExpressions;

namespace XTMF.Gui.UserControls.Help
{
    /// <summary>
    /// Interaction logic for HelpDialog.xaml
    /// </summary>
    public partial class HelpDialog : UserControl
    {
        /// <summary>
        /// Our link back into XTMF
        /// </summary>
        private IConfiguration Config;

        private SpinLock FullyLoaded = new SpinLock(false);

        public HelpDialog(IConfiguration xtmfConfiguration)
        {
            DataContext = this;
            Config = xtmfConfiguration;
            SearchedItems = new BindingList<ContentReference>();
            InitializeComponent();
            SearchBox.TextChanged += SearchBox_TextChanged;
            SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
            UpdateSearch(true);
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                SearchBox.Text = String.Empty;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSearch(true);
        }

        private void UpdateSearch(bool async)
        {
            //search
            try
            {
                string text = null;
                Dispatcher.Invoke(() =>
                {
                    text = SearchBox.Text;
                });
                Regex searchFor = new Regex(text, RegexOptions.IgnoreCase);
                var loadTask = Task.Run(() =>
                {
                    try
                    {
                        var results = ((from module in Config.ModelRepository.AsParallel()
                                        where searchFor.IsMatch(module.FullName)
                                        select new ContentReference(module.FullName, module))).OrderBy(c => c.ToString()).ToArray();
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            SearchedItems.Clear();
                            foreach (var result in results)
                            {
                                SearchedItems.Add(result);
                            }
                        }));
                    }
                    catch (Exception error)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show(error.Message);
                        }));
                    }
                });
                if (!async)
                {
                    loadTask.Wait();
                }
            }
            catch
            {
            }
        }

        public BindingList<ContentReference> SearchedItems { get; private set; }


        public ContentReference CurrentContent
        {
            get { return (ContentReference)GetValue(CurrentContentProperty); }
            set { SetValue(CurrentContentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CurrentContent.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CurrentContentProperty =
            DependencyProperty.Register("CurrentContent", typeof(ContentReference), typeof(HelpDialog), new PropertyMetadata(null, OnCurrentContentChanged));

        private static void OnCurrentContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var us = d as HelpDialog;
            var newContent = e.NewValue as ContentReference;
            us.ContentPresenter.Children.Clear();
            if (newContent != null && newContent.Content != null)
            {
                us.ContentPresenter.Children.Add(newContent.Content);
            }
        }

        private void ResultBox_Selected(object sender, RoutedEventArgs e)
        {
            CurrentContent = ResultBox.SelectedItem as ContentReference;
        }

        public void SelectModuleContent(ModelSystemStructureModel module)
        {
            Task.Run(() =>
           {
               if (module != null)
               {
                   var type = module.Type;
                   var selectCorrectDocument = Task.Run(() =>
                   {
                       UpdateSearch(false);
                       Dispatcher.BeginInvoke(new Action(() =>
                      {
                          var foundElement = SearchedItems.FirstOrDefault(element => element.Module == type);
                          if (foundElement != null)
                          {
                              ResultBox.SelectedItem = foundElement;
                          }
                      }));
                   });
                   OperationProgressing progressing = null;
                   Dispatcher.Invoke(new Action(() =>
                   {
                       progressing = new OperationProgressing()
                       {
                           Owner = MainWindow.Us
                       };
                   }));
                   Dispatcher.BeginInvoke(new Action(() =>
                   {
                       progressing.ShowDialog();
                   }));
                   selectCorrectDocument.Wait();
                   Dispatcher.BeginInvoke(new Action(() =>
                   {
                       progressing.Close();
                   }));
               }
           });
        }
    }
}
