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
using System.IO;
using TMG.Estimation;
using TMG.Input;
using XTMF;

namespace TMG.NetworkEstimation
{
    [ModuleInformation(Description= "Produces a report file which lists a unique ID for each Task assigned to a client, along with the parameters " +
                                    "estimated for that task. This is useful in conjunction with modules which produce outputs for each Parameter Set.")]
    public class ReportClientTasks : ISelfContainedModule
    {

        [RootModule]
        public IEstimationClientModelSystem Root;

        [RunParameter("Report File", "", typeof(FileFromOutputDirectory), "A report file to save/append to. Saved in the Output directory.")]
        public FileFromOutputDirectory ReportFile;

        private static Tuple<byte, byte, byte> _ProgressColour = new(100, 100, 150);

        public void Start()
        {
            var exists = File.Exists(ReportFile.GetFileName());

            using var writer = new StreamWriter(ReportFile.GetFileName(), true);
            if (!exists)
            {
                var s1 = "Generation,Index";
                foreach (var ps in Root.Parameters)
                {
                    s1 += "," + string.Join(" ", ps.Names, 0, ps.Names.Length);
                }
                writer.WriteLine(s1);
            }

            var s2 = string.Join(",", Root.CurrentTask.Generation, Root.CurrentTask.Index);
            foreach (var val in Root.CurrentTask.ParameterValues)
            {
                s2 += "," + val;
            }
            writer.WriteLine(s2);
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

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
