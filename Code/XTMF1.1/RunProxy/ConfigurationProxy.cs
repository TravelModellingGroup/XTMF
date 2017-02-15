/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF.Networking;

namespace XTMF.RunProxy
{
    /// <summary>
    /// This class is used to proxy for the true configuration during a run.
    /// This is required to make sure that the active project during a run is correct in
    /// replicating the behaviour of XTMF 1.0.
    /// </summary>
    public class ConfigurationProxy : IConfiguration
    {
        /// <summary>
        /// The configuration we are proxying for
        /// </summary>
        private Configuration RealConfiguration;

        private class ProgressReport : IProgressReport
        {
            public Tuple<byte, byte, byte> Colour
            {
                get;
                set;
            }

            public Func<float> GetProgress
            {
                get;
                internal set;
            }

            public string Name
            {
                get;
                internal set;
            }
        }

        public ConfigurationProxy(Configuration realConfig, IProject activeProject)
        {
            RealConfiguration = realConfig;
            ProjectRepository = new ProjectRepositoryProxy(RealConfiguration.ProjectRepository, activeProject);
            ProgressReports = new BindingListWithRemoving<IProgressReport>();
        }

        public string ConfigurationDirectory
        {
            get
            {
                return RealConfiguration.ConfigurationDirectory;
            }
        }

        public IModuleRepository ModelRepository
        {
            get
            {
                return RealConfiguration.ModelRepository;
            }
        }

        public string ModelSystemDirectory
        {
            get
            {
                return RealConfiguration.ModelSystemDirectory;
            }
        }

        public IModelSystemRepository ModelSystemRepository
        {
            get
            {
                return RealConfiguration.ModelSystemRepository;
            }
        }

        public IModelSystemTemplateRepository ModelSystemTemplateRepository
        {
            get
            {
                return RealConfiguration.ModelSystemTemplateRepository;
            }
        }

        public BindingListWithRemoving<IProgressReport> ProgressReports
        {
            get;
            private set;
        }

        public void CreateProgressReport(string name, Func<float> ReportProgress, Tuple<byte, byte, byte> Color = null)
        {
            lock (this)
            {
                foreach (var report in ProgressReports)
                {
                    if (report.Name == name)
                    {
                        report.Colour = Color;
                        return;
                    }
                }
                ProgressReports.Add(new ProgressReport() { Name = name, GetProgress = ReportProgress, Colour = Color });
            }
        }

        public void DeleteAllProgressReport()
        {
            lock (this)
            {
                ProgressReports.Clear();
            }
        }

        public void DeleteProgressReport(string name)
        {
            lock (this)
            {
                for (int i = 0; i < ProgressReports.Count; i++)
                {
                    if (ProgressReports[i].Name == name)
                    {
                        ProgressReports.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public bool InstallModule(string moduleFileName)
        {
            throw new NotSupportedException("Installing modules is not supported from a model run!");
        }

        public IClient RetriveCurrentNetworkingClient()
        {
            return RealConfiguration.RetriveCurrentNetworkingClient();
        }

        public void Save()
        {
            RealConfiguration.Save();
        }

        public bool StartupNetworkingClient(out IClient networkingClient, ref string error)
        {
            return RealConfiguration.StartupNetworkingClient(out networkingClient, ref error);
        }

        public bool StartupNetworkingHost(out IHost networkingHost, ref string error)
        {
            return RealConfiguration.StartupNetworkingHost(out networkingHost, ref error);
        }

        public void UpdateProgressReportColour(string name, Tuple<byte, byte, byte> Color)
        {
            throw new NotImplementedException();
        }

        public string ProjectDirectory
        {
            get
            {
                return RealConfiguration.ProjectDirectory;
            }
        }

        public IProjectRepository ProjectRepository
        {
            get;
            private set;
        }

        public event Action OnModelSystemExit;

        internal void ModelSystemExited()
        {
            if (OnModelSystemExit != null)
            {
                try
                {
                    OnModelSystemExit();
                }
                catch
                {
                }
                var dels = OnModelSystemExit.GetInvocationList();
                foreach (Delegate d in dels)
                {
                    OnModelSystemExit -= (Action)d;
                }
                OnModelSystemExit = null;
            }
        }
    }
}