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
using System.Text;
using System.Threading;
using Tasha.Common;
using XTMF;
using TMG;
using TMG.Input;
using System.IO;

namespace Tasha.Validation.PoRPoW
{
    // ReSharper disable once InconsistentNaming
    public class PostSchedulePoRPoWAssignments : IPostScheduler
    {
        public string Name { get; set; }

        public float Progress
        {
            get
            {
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return null;
            }
        }

        SpinLock WriteLock = new(false);
        Dictionary<Occupation, Dictionary<TTSEmploymentStatus, float[][]>> Data = [];
        public void Execute(ITashaHousehold household)
        {
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var homeIndex = zoneSystem.GetFlatIndex(household.HomeZone.ZoneNumber);
            foreach(var person in household.Persons)
            {
                var expansionFactor = person.ExpansionFactor;
                var occ = person.Occupation;
                if (occ != Occupation.NotEmployed && Data.TryGetValue(occ, out Dictionary<TTSEmploymentStatus, float[][]> occDictionary))
                {
                    if (occDictionary.TryGetValue(person.EmploymentStatus, out float[][] empData))
                    {
                        var employmentZone = zoneSystem.GetFlatIndex(person.EmploymentZone.ZoneNumber);
                        if (employmentZone >= 0)
                        {
                            var row = empData[homeIndex];
                            bool taken = false;
                            WriteLock.Enter(ref taken);
                            row[employmentZone] += expansionFactor;
                            if (taken) WriteLock.Exit(true);
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            foreach(var occDict in Data)
            {
                var occ = occDict.Key;
                foreach(var empStatData in occDict.Value)
                {
                    var empStat = empStatData.Key;
                    TMG.Functions.SaveData.SaveMatrix(zones, empStatData.Value, BuildFileName(occ, empStat));
                }
            }
        }

        [SubModelInformation(Required = true, Description = "The directory in which we will save the data.")]
        public FileLocation DirectoryLocation;

        private string BuildFileName(Occupation occ, TTSEmploymentStatus empStat)
        {
            var dirPath = DirectoryLocation.GetFilePath();
            var info = new DirectoryInfo(dirPath);
            if(!info.Exists)
            {
                info.Create();
            }
            StringBuilder buildFileName = new();
            switch(occ)
            {
                case Occupation.Professional:
                    buildFileName.Append("Professional-");
                    break;
                case Occupation.Office:
                    buildFileName.Append("General-");
                    break;
                case Occupation.Retail:
                    buildFileName.Append("Sales-");
                    break;
                case Occupation.Manufacturing:
                    buildFileName.Append("Manufacturing-");
                    break;
            }
            switch(empStat)
            {
                case TTSEmploymentStatus.FullTime:
                    buildFileName.Append("FullTime.csv");
                    break;
                case TTSEmploymentStatus.PartTime:
                    buildFileName.Append("PartTime.csv");
                    break;
            }
            return Path.Combine(dirPath, buildFileName.ToString());
        }

        public void Load(int maxIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [RootModule]
        public ITravelDemandModel Root;

        private void AddOccupation(Occupation occ)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var occDic = new Dictionary<TTSEmploymentStatus, float[][]>();
            Data[occ] = occDic;
            occDic[TTSEmploymentStatus.FullTime] = Create2DArray<float>(zones.Length);
            occDic[TTSEmploymentStatus.PartTime] = Create2DArray<float>(zones.Length);
        }

        private static T[][] Create2DArray<T>(int length)
        {
            T[][] ret = new T[length][];
            for(int i = 0; i < ret.Length; i++)
            {
                ret[i] = new T[length];
            }
            return ret;
        }

        public void IterationStarting(int iteration)
        {
            if(iteration > 0)
            {
                Data.Clear();
            }
            AddOccupation(Occupation.Professional);
            AddOccupation(Occupation.Office);
            AddOccupation(Occupation.Retail);
            AddOccupation(Occupation.Manufacturing);
        }
    }
}
