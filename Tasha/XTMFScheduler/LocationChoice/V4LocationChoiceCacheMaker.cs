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
using TMG;

namespace Tasha.XTMFScheduler.LocationChoice
{

    public class V4LocationChoiceCacheMaker : ITravelDemandModel
    {
        [RunParameter("Input Directory", "../../Input", "The directory containing the models input.")]
        public string InputBaseDirectory { get; set; }

        public string OutputBaseDirectory { get; set; }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); } }

        public IList<INetworkData> NetworkData { get;set; }

        [SubModelInformation(Required = true, Description = "")]
        public IZoneSystem ZoneSystem { get; set; }

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [SubModelInformation(Required = true, Description = "")]
        public IResource ProfessionalFullTime;
        [SubModelInformation(Required = true, Description = "")]
        public IResource ProfessionalPartTime;

        [SubModelInformation(Required = true, Description = "")]
        public IResource GeneralFullTime;
        [SubModelInformation(Required = true, Description = "")]
        public IResource GeneralPartTime;

        [SubModelInformation(Required = true, Description = "")]
        public IResource RetailFullTime;
        [SubModelInformation(Required = true, Description = "")]
        public IResource RetailPartTime;

        [SubModelInformation(Required = true, Description = "")]
        public IResource ManufacturingFullTime;
        [SubModelInformation(Required = true, Description = "")]
        public IResource ManufacturingPartTime;

        [SubModelInformation(Required = true, Description = "Generate the market information.")]
        public GenerateMarket Market;

        public class GenerateMarket : IModule
        {

            [ParentModel]
            public V4LocationChoiceCacheMaker Parent;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); } }

            public virtual void Process(int[] regions, int[] planningDistricts)
            {

            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        public void Start()
        {
            ZoneSystem.LoadData();
            var zoneSystem = ZoneSystem.ZoneArray;
            IZone[] zones = zoneSystem.GetFlatData();
            var regions = zones.Select( z => z.RegionNumber ).ToArray();
            var planningDistricts = zones.Select( z => z.PlanningDistrict ).ToArray();
            Market.Process( regions, planningDistricts );
        }
    }

}
