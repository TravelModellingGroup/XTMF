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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using XTMF.Networking;

namespace James.UTDM
{
    public class TestHost : IModelSystemTemplate
    {
        public IHost Host;

        [RunParameter("Model System", "Network MS", "The name of the model system to execute remotely.")]
        public string ModelSystemName;

        [RunParameter("Iterations", 10, "How many times do you want to run it remotely?")]
        public int Iterations;

        public string InputBaseDirectory
        {
            get;
            set;
        }

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        public bool ExitRequest()
        {
            return false;
        }

        volatile bool NoneConnected = true;

        IConfiguration Configuration;

        public TestHost(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        int completed = 0;

        public void Start()
        {
            this.Host.NewClientConnected += new Action<IRemoteXTMF>(HostModule_NewClientConnected);
            this.Host.ClientDisconnected += new Action<IRemoteXTMF>(Host_ClientDisconnected);
            this.Host.ProgressUpdated += new Action<IRemoteXTMF, float>(Host_ProgressUpdated);
            this.Host.ClientRunComplete += new Action<IRemoteXTMF, int, string>(Host_ClientRunComplete);
            while (NoneConnected)
            {
                System.Threading.Thread.Sleep(250);
            }
            string error = null;
            // The Model System "New MS" is actually Tasha without GO and drive to transit
            var tasha = this.Host.CreateModelSystem(this.ModelSystemName, ref error);
            if (tasha == null)
            {
                throw new XTMF.XTMFRuntimeException("Unable to load the Model System!");
            }
            for (int i = 0; i < this.Iterations; i++)
            {
                this.Host.ExecuteModelSystemAsync(tasha);
            }
            while (completed != this.Iterations)
            {
                System.Threading.Thread.Sleep(250);
            }
            this.Progress = 1f;
        }

        void Host_ClientRunComplete(IRemoteXTMF arg1, int arg2, string arg3)
        {
            lock (this)
            {
                System.Threading.Thread.MemoryBarrier();
                this.Progress += 1.0f/this.Iterations;
                this.completed++;
                System.Threading.Thread.MemoryBarrier();
            }
        }

        void Host_ProgressUpdated(IRemoteXTMF origin, float progress)
        {
            // we don't need to monitor the progress since we will periodically poll the clients
        }

        void Host_ClientDisconnected(IRemoteXTMF obj)
        {
            this.NoneConnected = true;
        }

        void HostModule_NewClientConnected(IRemoteXTMF obj)
        {
            this.NoneConnected = false;
            this.Configuration.CreateProgressReport(obj.UniqueID == null ? "Remote Host" : obj.UniqueID, delegate()
            {
                return obj.Progress;
            }, new Tuple<byte, byte, byte>(150, 50, 50));
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(50, 150, 50);
        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if (this.Host == null)
            {
                error = "We were not loaded with a host module!";
                return false;
            }
            if (this.Configuration == null)
            {
                error = "We were not loaded with the XTMF Configuration!";
                return false;
            }
            return true;
        }
    }
}
