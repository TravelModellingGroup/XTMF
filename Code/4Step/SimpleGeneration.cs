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
using Datastructure;
using TMG;
using XTMF;

namespace James.UTDM
{
    public class SimpleGeneration : IGeneration
    {
        public string Name { get; set; }
        public float Progress { get; set; }

        [RunParameter("EmploymentFactor", 2f, "The factor to multiply against the total employment")]
        public float EmploymentFactor;

        [RunParameter("PopulationFactor", 2f, "The factor to multiply against the total population")]
        public float PopulationFactor;

        [RootModule]
        public ITravelDemandModel Root;

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if (EmploymentFactor <= 0)
            {
                error = "The value for EmploymentFactor must be greater than 0!";
                return false;
            }
            else if (PopulationFactor <= 0)
            {
                error = "The value for PopulationFactor must be greater than 0!";
                return false;
            }
            return true;
        }

        public void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
            float totalProduction = 0;
            float totalAttraction = 0;
            foreach (var zone in production.ValidIndexies())
            {
                float prod, attr;
                production[zone] = (prod = this.PopulationFactor * production[zone]);
                attractions[zone] = (attr = this.EmploymentFactor * attractions[zone]);
                totalProduction += prod;
                totalAttraction += attr;
            }
            if (totalAttraction <= 0)
            {
                throw new XTMF.XTMFRuntimeException("There is no employment in the zone system!");
            }
            else if (totalProduction <= 0)
            {
                throw new XTMF.XTMFRuntimeException("There is no population in the zone system!");
            }
            // Normalize the attractions
            var inverseTotalAttraction = 1 / totalAttraction; // inverse totalAttraction to save on divisions
            foreach (var zone in Root.ZoneSystem.ZoneArray.ValidIndexies())
            {
                attractions[zone] = (attractions[zone] * inverseTotalAttraction) * totalProduction;
            }
        }
    }
}
