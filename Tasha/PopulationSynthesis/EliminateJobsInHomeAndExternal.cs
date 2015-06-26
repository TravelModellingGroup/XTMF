/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;
using Datastructure;
using TMG;

namespace Tasha.PopulationSynthesis
{

    public class EliminateJobsInHomeAndExternal : IDataSource<SparseArray<float>>
    {
        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public IResource EmploymentByZoneForOccEmpStat;

        public IResource WorkAtHomeRateByZoneForOccEmpStat;

        public IResource ExternalWorkerRateByZoneForByOccEmpStat;

        [RootModule]
        public ITravelDemandModel Root;

        public SparseArray<float> GiveData()
        {
            return Data;
        }

        private SparseArray<float> Data;

        public void LoadData()
        {
            var employmentForZone = EmploymentByZoneForOccEmpStat.AquireResource<SparseArray<float>>().GetFlatData();
            var workAtHomeRates = WorkAtHomeRateByZoneForOccEmpStat.AquireResource<SparseArray<float>>().GetFlatData();
            var externalWorkerRates = ExternalWorkerRateByZoneForByOccEmpStat.AquireResource<SparseArray<float>>().GetFlatData();
            var data = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
            var flat = data.GetFlatData();
            for(int i = 0; i < flat.Length; i++)
            {
                var postWaHEmployment = employmentForZone[i] * (1.0f - workAtHomeRates[i]);
                flat[i] = postWaHEmployment * (1.0f - externalWorkerRates[i]);
            }
            Data = data;
        }

        public bool RuntimeValidation(ref string error)
        {
            if(!EmploymentByZoneForOccEmpStat.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' EmploymentByZoneForOccEmpStat needs to be of type SparseArray<float>!";
                return false;
            }

            if(!ExternalWorkerRateByZoneForByOccEmpStat.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' ExternalWorkerRateByZoneForByOccEmpStat needs to be of type SparseArray<float>!";
                return false;
            }

            if(!WorkAtHomeRateByZoneForOccEmpStat.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' WorkAtHomeRateByZoneForOccEmpStat needs to be of type SparseArray<float>!";
                return false;
            }
            return true;
        }

        public void UnloadData()
        {
            Data = null;
        }
    }

}
