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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datastructure;
using XTMF;

namespace TMG.Frameworks.Data.Synthesis.Gibbs
{
    public sealed class ConditionalByPD : Conditional
    {
        public override bool RequiresReloadingPerZone
        {
            get
            {
                return true;
            }
        }

        [SubModelInformation(Required = false, Description = "An optional source to load the zone system from.  If left blank the Travel Demand Model Zone system will be used.")]
        public IDataSource<IZoneSystem> ZoneSystemSource;

        private IZoneSystem ZoneSystem;

        private IConfiguration Config;

        public ConditionalByPD(IConfiguration config)
        {
            Config = config;
        }

        public override bool RuntimeValidation(ref string error)
        {
            // Get the zone system from the travel demand model
            if (ZoneSystemSource != null)
            {
                ZoneSystemSource.LoadData();
                ZoneSystem = ZoneSystemSource.GiveData();
            }
            else
            {
                IModelSystemStructure tdm;
                if (TMG.Functions.ModelSystemReflection.GetRootOfType(Config, typeof(ITravelDemandModel), this, out tdm))
                {
                    ZoneSystem = ((ITravelDemandModel)tdm.Module).ZoneSystem;
                    if (ZoneSystem != null && !ZoneSystem.Loaded)
                    {
                        ZoneSystem.LoadData();
                    }
                }
            }
            if (ZoneSystem == null)
            {
                error = $"In {Name} we were unable to load a zone system for our calculations!";
                return false;
            }
            return base.RuntimeValidation(ref error);
        }


        public override void LoadConditionalsData(int currentZone)
        {
            var zone = ZoneSystem.ZoneArray.GetFlatData()[currentZone];
            if(zone == null)
            {
                throw new XTMFRuntimeException($"In {Name} we were asked to process a zone that we do not have defined! Zone#{currentZone}!");
            }
            var pdToProcess = zone.PlanningDistrict;
            var prob = GenerateBackendData();
            // an extra column since the first one is for the planning district
            int expectedColumns = ColumnIndex.Length + 2;
            var currentIndex = new int[expectedColumns - 2];
            bool any = false;
            using (var reader = new CsvReader(ConditionalSource))
            {
                int columns;
                reader.LoadLine();
                while (reader.LoadLine(out columns))
                {
                    if (columns >= expectedColumns)
                    {
                        any = true;
                        int pd;
                        reader.Get(out pd, 0);
                        // ignore data rows not for our PD
                        if(pdToProcess != pd)
                        {
                            continue;
                        }
                        for (int i = 0; i < currentIndex.Length; i++)
                        {
                            reader.Get(out currentIndex[i], i + 1);
                        }
                        var probIndex = GetIndex(currentIndex);
                        if (probIndex < prob.Length)
                        {
                            reader.Get(out prob[probIndex], currentIndex.Length + 1);
                        }
                        else
                        {
                            throw new XTMFRuntimeException($"In '{Name}' we found an invalid index to assign to {probIndex} but the max index was only {prob.Length}!");
                        }
                    }
                }
            }
            CDF = ConvertToCDF(prob);
            if (!any)
            {
                throw new XTMFRuntimeException($@"In {Name} we did not load any conditionals from the file '{ConditionalSource.GetFilePath()}'!  
This could be because the data does not have the expected number of columns ({expectedColumns}) as interpreted by the given attributes.");
            }
            Loaded = true;
        }
    }
}
