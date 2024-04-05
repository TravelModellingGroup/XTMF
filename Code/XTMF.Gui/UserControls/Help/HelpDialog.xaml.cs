/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xtmfConfiguration"></param>
        public HelpDialog(IConfiguration xtmfConfiguration)
        {
            DataContext = this;
            Config = xtmfConfiguration;
            SearchedItems = [];
            InitializeComponent();
            //SearchBox += SearchBox_TextChanged;
            SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
            SearchBox.Filter = Filter;
            SearchBox.RefreshFilter();

            SearchBox.Box.TextChanged += SearchBox_TextChanged;

            UpdateSearch(true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="o"></param>
        /// <param name="s"></param>
        /// <returns></returns>
        private bool Filter(object o, string s)
        {
            UpdateSearch(true);
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                SearchBox.Box.Text = String.Empty;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSearch(true);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="async"></param>
        private void UpdateSearch(bool async)
        {
            //search
            try
            {
                string text = null;
                Dispatcher.Invoke(() =>
                {
                    text = SearchBox.Box.Text;
                });
                Regex searchFor = new Regex(text, RegexOptions.IgnoreCase);
                var loadTask = Task.Run(() =>
                {
                    try
                    {
                        var results = ((from module in Config.ModelRepository.AsParallel()
                                        where searchFor.IsMatch(module.FullName)
                                        select CreateContentReference(module))).OrderBy(c => c.ToString()).ToArray();
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        private static ContentReference CreateContentReference(Type module)
        {
            foreach (var at in module.GetCustomAttributes(true))
            {
                if (at is ModuleInformationAttribute mi)
                {
                    return new ContentReference(module.FullName, module, mi.DocURL);
                }
            }
            return new ContentReference(module.FullName, module);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnCurrentContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var us = d as HelpDialog;
            var newContent = e.NewValue as ContentReference;
            us.ContentPresenter.Content = null;
            if (newContent == null) return;
            if (!String.IsNullOrWhiteSpace(newContent.DocURL))
            {
                var browser = new WebBrowser();
                browser.Navigate(newContent.DocURL);
                us.ContentPresenter.Content = browser;
            }
            else
            {
                if (newContent != null)
                {
                    var content = newContent.Content;
                    us.ContentPresenter.Content = content;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResultBox_Selected(object sender, SelectionChangedEventArgs e)
        {
            if (ResultBox.SelectedItem is ContentReference cr)
            {
                CurrentContent = cr;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="module"></param>
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
                   Dispatcher.Invoke(() =>
                   {
                       progressing = new OperationProgressing
                       {
                           Owner = MainWindow.Us
                       };
                   });
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

        private void FilterModelSystemsBox_OnEnterPressed(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }
    }
}
