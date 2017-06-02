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
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
// ReSharper disable LocalizableElement

namespace XTMF.Update
{
    public partial class XTMFUpdateForm : Form
    {
        private UpdateController Controller;

        public XTMFUpdateForm()
        {
            InitializeComponent();
            Controller = new UpdateController();
            ArchitectureSelect.SelectedIndex = 0;
            if (ParentProcess != null)
            {
                UpdateButton.Enabled = false;
                Task.Factory.StartNew(() =>
               {
                   if (!ParentProcess.HasExited)
                   {
                       ParentProcess.WaitForExit();
                   }
                   Invoke(new Action(() =>
                   {
                       UpdateButton.Enabled = true;
                   }));
               });
            }
        }

        public string LaunchPoint { get; internal set; }
        public Process ParentProcess { get; internal set; }

        private void ArchitectureSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            // a different option was selected
        }

        private void EnableControls(bool setTrue)
        {
            ServerTextBox.Enabled = setTrue;
            UpdateButton.Enabled = setTrue;
            WebserviceCheckBox.Enabled = setTrue;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ServerTextBox.Text = Controller.XTMFUpdateServerLocation;
            WebserviceCheckBox.Checked = Controller.UseWebservices;
        }

        private void ServerTextBox_TextChanged(object sender, EventArgs e)
        {
            // we could dynamically check to see if the URL is valid here and set the update button
        }

        private void SetUpdateProgress(float param)
        {
            UpdateProgress.Value = (int)(param * 100);
        }

        private void SetUpdateStatus(string status)
        {
            StatusLabel.Text = status;
        }

        private void UpdateButton_Click(object sender, EventArgs e)
        {
            Controller.XTMFUpdateServerLocation = ServerTextBox.Text;
            bool force32 = ArchitectureSelect.SelectedIndex % 3 == 2;
            bool force64 = ArchitectureSelect.SelectedIndex % 3 == 1;
            bool xtmfOnly = ArchitectureSelect.SelectedIndex >= 3;
            Controller.UseWebservices = WebserviceCheckBox.Checked;
            EnableControls(false);
            var updateTask = new Task(delegate
            {
                try
                {
                    Controller.UpdateAll(force32, force64, xtmfOnly, (p => BeginInvoke(new Action<float>(SetUpdateProgress), p)),
                        (s => BeginInvoke(new Action<string>(SetUpdateStatus), s)), LaunchPoint);
                }
                catch (AggregateException error)
                {
                    MessageBox.Show(error.InnerException?.Message, "XTMF Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.Message + "\r\n" + error.StackTrace, "XTMF Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    EnableControls(true);
                }
            });
            updateTask.Start();
        }
    }
}