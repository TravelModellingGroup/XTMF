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
namespace XTMF.Controller
{
    /// <summary>
    /// This call provides access to the functionality of running model systems
    /// </summary>
    public class RunController
    {
        /// <summary>
        /// Our link to the XTMF configuration
        /// </summary>
        private Configuration Configuration;

        /// <summary>
        /// Our link to XTMF
        /// </summary>
        private XTMFRuntime XTMFRuntime;

        /// <summary>
        ///
        /// </summary>
        /// <param name="xtmfRuntime"></param>
        internal RunController(XTMFRuntime xtmfRuntime)
        {
            // TODO: Complete member initialization
            this.XTMFRuntime = xtmfRuntime;
            this.Configuration = this.XTMFRuntime.Configuration;
        }

        /// <summary>
        /// Create a new run
        /// </summary>
        /// <param name="name">The name of the run to create</param>
        /// <param name="project">The project this run belongs to</param>
        /// <param name="modelSystemIndex">The model syste structure's index to use from this project</param>
        /// <param name="error">Any errors when trying to start this run</param>
        /// <returns>A link to the run, null if it failed where error will contain the reason why.</returns>
        public XTMFRun CreateRun(string name, IProject project, int modelSystemIndex, ref string error)
        {
            if ( !this.Configuration.ValidateProjectDirectory( name, ref error ) )
            {
                return null;
            }
            // then return the ability to analyze it back
            this.XTMFRuntime.ProjectController.SetActiveProject(project);
            return new XTMFRun( project, modelSystemIndex, this.Configuration, name ); ;
        }
    }
}