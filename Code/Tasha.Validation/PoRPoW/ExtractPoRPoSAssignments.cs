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
using System.Threading;
using Tasha.Common;
using XTMF;
using TMG;
using TMG.Input;
using System.IO;
using Datastructure;

namespace Tasha.Validation.PoRPoW
{
    public class ExtractPoRPoSAssignments : IPostHousehold
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

        [RunParameter("Elementary Ages", "0-14", typeof(RangeSet), "The ages that go to an elementary school.")]
        public RangeSet ElementaryAges;

        [RunParameter("Highschool Ages", "15-18", typeof(RangeSet), "The ages that go to a highschool.")]
        public RangeSet HighschoolAges;

        [RunParameter("Minimum Age", 11, "The youngest a person can be and still be recorded.")]
        public int MinimumAge;

        SpinLock WriteLock = new SpinLock(false);
        Dictionary<int, Dictionary<StudentStatus, float[][]>> Data = new Dictionary<int, Dictionary<StudentStatus, float[][]>>();
        public void Execute(ITashaHousehold household, int iteration)
        {
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var homeIndex = zoneSystem.GetFlatIndex(household.HomeZone.ZoneNumber);
            foreach(var person in household.Persons)
            {
                if(person.Age >= MinimumAge)
                {
                    var expansionFactor = person.ExpansionFactor;
                    // if this person is not a student we can just continue
                    if (person.StudentStatus != StudentStatus.NotStudent)
                    {
                        var gradeTeir = GetGradeTier(person.Age);
                        Dictionary<StudentStatus, float[][]> teirDictionary;
                        if (Data.TryGetValue(gradeTeir, out teirDictionary))
                        {
                            float[][] studentData;
                            if (teirDictionary.TryGetValue(person.StudentStatus, out studentData))
                            {
                                var zone = person.SchoolZone;
                                if (zone != null)
                                {
                                    var schoolZones = zoneSystem.GetFlatIndex(zone.ZoneNumber);
                                    if (schoolZones >= 0)
                                    {
                                        var row = studentData[homeIndex];
                                        bool taken = false;
                                        WriteLock.Enter(ref taken);
                                        row[schoolZones] += expansionFactor;
                                        if (taken) WriteLock.Exit(true);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private int GetGradeTier(int age)
        {
            if(ElementaryAges.Contains(age))
            {
                return 0;
            }
            else if(HighschoolAges.Contains(age))
            {
                return 1;
            }
            return 2;
        }

        public void IterationFinished(int iteration)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            foreach(var occDict in Data)
            {
                var gradeTeir = occDict.Key;
                foreach(var empStatData in occDict.Value)
                {
                    var studentStatus = empStatData.Key;
                    TMG.Functions.SaveData.SaveMatrix(zones, empStatData.Value, BuildFileName(gradeTeir, studentStatus));
                }
            }
        }

        [SubModelInformation(Required = true, Description = "The directory in which we will save the data.")]
        public FileLocation DirectoryLocation;

        private string BuildFileName(int gradeTeir, StudentStatus stuStatus)
        {
            var dirPath = DirectoryLocation.GetFilePath();
            var info = new DirectoryInfo(dirPath);
            if(!info.Exists)
            {
                info.Create();
            }
            StringBuilder buildFileName = new StringBuilder();
            switch(gradeTeir)
            {
                case 0:
                    buildFileName.Append("Elementary-");
                    break;
                case 1:
                    buildFileName.Append("Highschool-");
                    break;
                case 2:
                    buildFileName.Append("PostSecondary-");
                    break;
            }
            switch(stuStatus)
            {
                case StudentStatus.FullTime:
                    buildFileName.Append("FullTime.csv");
                    break;
                case StudentStatus.PartTime:
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
        public ITashaRuntime Root;

        private void AddStudentStatus(int gradeTeir)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var stuDic = new Dictionary<StudentStatus, float[][]>();
            Data[gradeTeir] = stuDic;
            stuDic[StudentStatus.FullTime] = Create2DArray<float>(zones.Length);
            stuDic[StudentStatus.PartTime] = Create2DArray<float>(zones.Length);
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
            // Add the data for the three age teirs for school
            AddStudentStatus(0);
            AddStudentStatus(1);
            AddStudentStatus(2);
        }
    }
}
