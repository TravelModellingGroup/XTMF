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
using System.Windows.Forms;

namespace XTMF.Update
{
    public partial class Form1 : Form
    {
        private UpdateController Controller;

        public Form1()
        {
            InitializeComponent();
            Controller = new UpdateController();
            this.ArchitectureSelect.SelectedIndex = 0;
        }

        private void ArchitectureSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            // a different option was selected
        }

        private void EnableControls(bool setTrue)
        {
            this.ServerTextBox.Enabled = setTrue;
            this.UpdateButton.Enabled = setTrue;
            this.WebserviceCheckBox.Enabled = setTrue;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.ServerTextBox.Text = this.Controller.XTMFUpdateServerLocation;
            this.WebserviceCheckBox.Checked = this.Controller.UseWebservices;
        }

        private void ServerTextBox_TextChanged(object sender, EventArgs e)
        {
            // we could dynamically check to see if the URL is valid here and set the update button
        }

        private void SetUpdateProgress(float param)
        {
            this.UpdateProgress.Value = (int)( param * 100 );
        }

        private void SetUpdateStatus(string status)
        {
            this.StatusLabel.Text = status;
        }

        private void UpdateButton_Click(object sender, EventArgs e)
        {
            this.Controller.XTMFUpdateServerLocation = this.ServerTextBox.Text;
            bool force32 = this.ArchitectureSelect.SelectedIndex % 3 == 2;
            bool force64 = this.ArchitectureSelect.SelectedIndex % 3 == 1;
            bool xtmfOnly = this.ArchitectureSelect.SelectedIndex >= 3;
            this.Controller.UseWebservices = this.WebserviceCheckBox.Checked;
            EnableControls( false );
            var UpdateTask = new Task( new Action( delegate()
                {
                    try
                    {
                        this.Controller.UpdateAll( force32, force64, xtmfOnly, ( p => this.BeginInvoke( new Action<float>( SetUpdateProgress ), new object[] { p } ) ),
                        ( s => this.BeginInvoke( new Action<string>( SetUpdateStatus ), new object[] { s } ) ) );
                    }
                    catch ( AggregateException error )
                    {
                        MessageBox.Show( error.InnerException.Message, "XTMF Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
                    }
                    catch ( Exception error )
                    {
                        MessageBox.Show( error.Message + "\r\n" + error.StackTrace, "XTMF Update Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
                    }
                    finally
                    {
                        EnableControls( true );
                    }
                } ) );
            UpdateTask.Start();
        }
    }
}