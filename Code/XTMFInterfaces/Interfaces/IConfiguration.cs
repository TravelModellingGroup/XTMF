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

using XTMF.Networking;

namespace XTMF
{
    /// <summary>
    /// Provides access to the configuration and systems
    /// in XTMF
    /// </summary>
    public interface IConfiguration
    {
        /// <summary>
        /// This event is used after a model system exits.
        /// </summary>
        event Action OnModelSystemExit;

        string ConfigurationDirectory { get; }

        /// <summary>
        /// Provides access to all of the models in this
        /// XTMF installation
        /// </summary>
        IModuleRepository ModelRepository { get; }

        /// <summary>
        /// The directory that the model systems are stored in
        /// </summary>
        string ModelSystemDirectory { get; }

        /// <summary>
        ///
        /// </summary>
        IModelSystemRepository ModelSystemRepository { get; }

        /// <summary>
        ///
        /// </summary>
        IModelSystemTemplateRepository ModelSystemTemplateRepository { get; }

        /// <summary>
        ///
        /// </summary>
        BindingListWithRemoving<IProgressReport> ProgressReports { get; }

        /// <summary>
        /// The directory that the projects are stored in
        /// </summary>
        string ProjectDirectory { get; }

        /// <summary>
        /// Provides accesss to all of the Project's in this
        /// installation of XTMF
        /// </summary>
        IProjectRepository ProjectRepository { get; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ReportProgress"></param>
        /// <param name="Color"></param>
        void CreateProgressReport(string name, Func<float> ReportProgress, Tuple<byte, byte, byte> Color = null);

        /// <summary>
        ///
        /// </summary>
        void DeleteAllProgressReport();

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        void DeleteProgressReport(string name);

        /// <summary>
        /// Installs a new module into XTMF
        /// </summary>
        /// <param name="moduleFileName">The module to install</param>
        /// <remarks>Avoid using this if you can, if you do this a client without administrative permissions could fail to run</remarks>
        /// <returns>True if we could successfully install the module, false otherwise</returns>
        bool InstallModule(string moduleFileName);

        /// <summary>
        /// Retrive the Networking Client however, do not initialize a new one if it doesn't exist already.
        /// </summary>
        /// <returns>The networking client module</returns>
        IClient RetriveCurrentNetworkingClient();

        /// <summary>
        /// Save the Configuration
        /// </summary>
        void Save();

        /// <summary>
        ///
        /// </summary>
        /// <param name="networkingClient"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        bool StartupNetworkingClient(out IClient networkingClient, ref string error);

        /// <summary>
        ///
        /// </summary>
        /// <param name="networkingHost"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        bool StartupNetworkingHost(out IHost networkingHost, ref string error);

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <param name="Color"></param>
        void UpdateProgressReportColour(string name, Tuple<byte, byte, byte> Color);
    }
}