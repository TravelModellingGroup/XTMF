/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;

namespace XTMF.Run
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length != 3)
            {
                Console.WriteLine("Usage: [ProjectName] [ModelSystemName] [RunName]");
                return;
            }
            string projectName = args[0];
            string modelSystemName = args[1];
            string runName = args[2];
            var runtime = new XTMFRuntime();
            string error = null;
            Project project;
            if((project = runtime.ProjectController.Load(projectName, ref error)) == null)
            {
                Console.WriteLine("Error loading project\r\n" + error);
                return;
            }
            using (var projectSession = runtime.ProjectController.EditProject(project))
            {
                var modelSystems = projectSession.Project.ModelSystemStructure.Select( (m, i) => new { MSS = m, Index = i }).Where( (m, i) => m.MSS.Name == modelSystemName).ToList();
                switch(modelSystems.Count)
                {
                    case 0:
                        Console.WriteLine("There was no model system in the project " + project.Name + " called " + modelSystemName + "!");
                        return;
                    case 1:
                        Run(modelSystems[0].Index, projectSession, runName);
                        break;
                    default:
                        Console.WriteLine("There were multiple model systems in the project " + project.Name + " called " + modelSystemName + "!");
                        return;
                }
            }
        }

        private static void Run(int index, ProjectEditingSession projectSession, string runName)
        {
            using (var modelSystemSession = projectSession.EditModelSystem(index))
            {
                string error = null;
                XTMFRun run;
                if((run = modelSystemSession.Run(runName, ref error)) == null)
                {
                    Console.WriteLine("Unable to run \r\n" + error);
                    return;
                }
                run.RunComplete += Run_RunComplete;
                run.Start();
                run.Wait();
            }
        }

        private static void Run_RunComplete()
        {
            Environment.Exit(0);
        }
    }
}
