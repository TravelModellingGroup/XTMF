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
using MahApps.Metro.Controls;

namespace XTMF.Gui.UserControls
{
    /// <summary>
    /// Interaction logic for SchedulerRunItemDisplayModel.xaml
    /// </summary>
    public partial class SchedulerRunItem : UserControl
    {

        private SchedulerWindow _schedulerWindow;

        public SchedulerRunItem()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelRunMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var item = (SchedulerWindow.SchedulerRunItemDisplayModel)_schedulerWindow.ScheduledRuns.SelectedItem;
            item.RunWindow.CancelRun();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QueuePriorityUpMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            _schedulerWindow.MoveQueueUp();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QueuePriorityDownMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            _schedulerWindow.MoveQueueDown();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScheduledRunItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var contextMenu = (sender as FrameworkElement)?.ContextMenu;
            var cancelItem = (MenuItem)contextMenu?.Items[0];
            var upItem = (MenuItem)contextMenu?.Items[1];
            var downItem = (MenuItem)contextMenu?.Items[2];

            upItem.IsEnabled = true;
            downItem.IsEnabled = true;
            cancelItem.IsEnabled = true;
            cancelItem.IsEnabled = true;
            upItem.IsEnabled = _schedulerWindow.CanMoveQueueUp();
            downItem.IsEnabled = _schedulerWindow.CanMoveQueueDown();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SchedulerRunItem_OnInitialized(object sender, EventArgs e)
        {
            AllowDrop = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SchedulerRunItem_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SchedulerRunItem_OnMouseMove(object sender, MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DataObject data = new DataObject();
                data.SetData("RunItem", this.DataContext);
                data.SetData("Index",this._schedulerWindow.ScheduledRuns.Items.IndexOf(this.DataContext));
                DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            this.HideDragAdorner();
            SchedulerWindow.SchedulerRunItemDisplayModel item = e.Data.GetData("RunItem") as SchedulerWindow.SchedulerRunItemDisplayModel;
            int draggedIndex = (int)e.Data.GetData("Index");
            var thisIndex = this._schedulerWindow.ScheduledRuns.Items.IndexOf(this.DataContext);
            this._schedulerWindow.MoveQueueInsert((SchedulerWindow.SchedulerRunItemDisplayModel)item,draggedIndex,thisIndex);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnDragLeave(DragEventArgs e)
        {
            base.OnDragLeave(e);
            this.HideDragAdorner();

        }

        /// <summary>
        /// 
        /// </summary>
        private void HideDragAdorner()
        {
            var layer = AdornerLayer.GetAdornerLayer(this);
            var moveAdorner = layer.GetAdorners(this)
                .First(t => t.GetType() == typeof(DragDropAdorner));
            if (moveAdorner != null)
            {
                ((DragDropAdorner)moveAdorner).Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            var model = DataContext as SchedulerWindow.SchedulerRunItemDisplayModel;
            if (model.IsRunStarted)
            {
                e.Effects = DragDropEffects.None;
                AllowDrop = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            SchedulerWindow.SchedulerRunItemDisplayModel model = e.Data.GetData("RunItem") as SchedulerWindow.SchedulerRunItemDisplayModel;
            int draggedIndex = (int) e.Data.GetData("Index");
            if (model.IsRunStarted)
            {
                AllowDrop = false;
                e.Handled = true;
                return;
            }

            if (model.IsRunStarted)
            {
                e.Effects = DragDropEffects.None;
            }
            var thisIndex = this._schedulerWindow.ScheduledRuns.Items.IndexOf(this.DataContext);

            bool isOrderUp = true;
            if (thisIndex == draggedIndex)
            {
                return;
            }
            else if (thisIndex > draggedIndex)
            {
                isOrderUp = false;
            }

            var layer = AdornerLayer.GetAdornerLayer(this);
            DragDropAdorner moveAdorner = (DragDropAdorner)layer.GetAdorners(this)
                .First(t => t.GetType() == typeof(DragDropAdorner));
            if (moveAdorner != null)
            {
                
                if (isOrderUp)
                {
                    moveAdorner.SetMoveUpAdorner();
                }
                else
                {
                    moveAdorner.SetMoveDownAdorner();
                }
                moveAdorner.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SchedulerRunItem_OnLoaded(object sender, RoutedEventArgs e)
        {
            this._schedulerWindow = this.Tag as SchedulerWindow;
            var layer = AdornerLayer.GetAdornerLayer(this);
            DragDropAdorner moveAdorner = new DragDropAdorner(this);
            layer.Add(moveAdorner);
        }
    }
}
