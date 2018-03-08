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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
using MaterialDesignThemes.Wpf;
using XTMF.Annotations;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for SelectRunDateTimeDialog.xaml
    /// </summary>
    public partial class SelectRunDateTimeDialog : UserControl


    {
        private DialogSession _dialogSession;


        public bool DidComplete { get; set; }

    
        public SelectRunDateTimeDialog()
        {
            InitializeComponent();

           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<object> ShowAsync(DialogHost host = null)
        {

            if (host == null)
            {
                return await DialogHost.Show(this, "RootDialog", OpenedEventHandler, ClosingEventHandler);
            }
            else
            {
                return await host.ShowDialog(this, OpenedEventHandler, ClosingEventHandler);
            }
        }

        private void OpenedEventHandler(object sender, DialogOpenedEventArgs eventargs)
        {
            this._dialogSession = eventargs.Session;


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void ClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectRunDateTimeDialog_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DidComplete = true;
                e.Handled = true;
                this._dialogSession.Close(false);
            }
            else if (e.Key == Key.Escape)
            {
                DidComplete = false;
                e.Handled = true;
                this._dialogSession.Close(false);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            RunConfigurationDisplayModel context = this.DataContext as RunConfigurationDisplayModel;

            if (RadioSchedule != null)
            {
                if (RadioSchedule.IsChecked != null && (bool) RadioSchedule.IsChecked)
                {
                    context.SelectScheduleEnabled = true;
                }
                else
                {
                    context.SelectScheduleEnabled = false;
                }
            }

        }

        public bool IsQueueRun => RadioQueue.IsChecked != null && (bool)RadioQueue.IsChecked;

        public bool IsImmediateRun => RadioImmediate.IsChecked != null && (bool) RadioImmediate.IsChecked;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunButton_OnClick(object sender, RoutedEventArgs e)
        {
            DidComplete = true;
            e.Handled = true;
            //if (RadioQueue.IsChecked != null) IsQueueRun = (bool) RadioQueue.IsChecked;
           // if (RadioImmediate.IsChecked != null) IsImmediateRun = (bool) RadioImmediate.IsChecked;

            this._dialogSession.Close(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            DidComplete = false;
            e.Handled = true;
            this._dialogSession.Close(false);
        }

       
    }

    public class XtmfDialog
    {
        public bool DidComplete;
    }


    /// <summary>
    /// 
    /// </summary>
    public class RunConfigurationDisplayModel : INotifyPropertyChanged
  
    {
        private bool _selectScheduleEnabled = false;

        private bool _useAdvanced = false;

        public bool UseAdvanced
        {
            get => _useAdvanced;
            set
            {
                _useAdvanced = true;
                OnPropertyChanged(nameof(UseAdvanced));
            }
        }

        public string UserInput { get; set; }

        public DateTime ScheduleTime { get; set; } = DateTime.Now;

        public DateTime ScheduleDate { get; set; } = DateTime.Today;

        public bool SelectScheduleEnabled
        {
            get => _selectScheduleEnabled;
            set
            {
                _selectScheduleEnabled = value;
                OnPropertyChanged(nameof(SelectScheduleEnabled));


            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        }
    }

}
