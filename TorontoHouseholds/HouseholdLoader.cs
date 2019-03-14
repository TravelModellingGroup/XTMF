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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datastructure;
using Tasha.Common;
using TMG.Input;
using XTMF;

namespace TMG.Tasha
{
    [ModuleInformation(Description = "This class is designed to load households from TTS like trip records.")]
    public sealed class HouseholdLoader : IDataLoader<ITashaHousehold>, IDisposable
    {
        [RunParameter("Auto Mode Name", "Auto", "The name of the mode that will turn into rideshare, this is ignored if there is no rideshare mode selected.")]
        public string AutoModeName;

        [RunParameter("Calculate Joint Trips", true, "Set true if you want the loader to detect joint trips.")]
        public bool CalculateJointTrips;

        [RunParameter("IncomeCol", -1, "The 0 indexed column that the household's income level is located at.")]
        public int IncomeCol;

        [RunParameter("HouseholdID", 0, "The 0 indexed column that the household's id is located at.")]
        public int HouseholdIDCol;

        [RunParameter("ZoneCol", 1, "The 0 indexed column that the household's id is located at.")]
        public int ZoneCol;

        [RunParameter("ExpansionFactorCol", 2, "The 0 indexed column that the household's id is located at.")]
        public int ExpansionFactorCol;

        [RunParameter("PeopleCol", 3, "The 0 indexed column that the household's id is located at.")]
        public int PeopleCol;

        [RunParameter("DwellingTypeCol", 4, "The 0 indexed column that the household's id is located at.")]
        public int DwellingTypeCol;

        [RunParameter("CarsCol", 5, "The 0 indexed column that the household's id is located at.")]
        public int CarsCol;

        [RunParameter("Header", false, "True if the csv file contains a header.")]
        public bool ContainsHeader;

        [RunParameter("FileName", "Households/Households.csv", "The csv file containing all of the household information.")]
        public string FileName;

        [RunParameter("Skip Single Trip Chain Households", false, "Should we continue loading households even if we find an invalid trip chain?")]
        public bool JustSkipSingleTripTripChainHouseholds;

        [RunParameter("MaxTripsInChain", 0, "Set the maximum number of trips allowed in a trip chain, 0 means any.")]
        public int MaxNumberOfTripsInChain;

        [RunParameter("MinAge", 0, "(0 means any) The youngest any person can be in the dataset.")]
        public int MinAge;

        [RunParameter("ObservedMode", "ObservedMode", "The name of the attachment for observed modes.")]
        public string ObservedMode;

        [RunParameter("Passenger Mode Name", "Passenger", "The name of the mode that will turn into rideshare, this is ignored if there is no rideshare mode selected.")]
        public string PassengerModeName;

        [SubModelInformation(Description = "The next model that loads in person information for the household.", Required = true)]
        public IDatachainLoader<ITashaHousehold, ITashaPerson> PersonLoader;

        [RunParameter("Rideshare Mode Name", "RideShare", "The name of the rideshare mode, leave blank to not process.  This is required if you are building joint tours.")]
        public string RideshareModeName;

        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("SecondVehicleType", "", "The name of the secondary vehicle type (e.g. motorcycle, bicycle, etc.)")]
        public string SecondaryVehicleTypeName;

        [RunParameter("Second Vehicle", -1, "The column number of secondary vehicle information. Set to '-1' to disable.")]
        public int SecondVehicleColumnNumber;

        [RunParameter("Load Once", false, "When loading all households, setting this to true will skip reloading households.")]
        public bool LoadOnce;

        private bool AllDataLoaded = true;
        private IVehicleType AutoType;
        private ITashaHousehold[] Households;
        private CsvReader Reader;
        private IVehicleType SecondaryType;

        ~HouseholdLoader()
        {
            Household.ReleaseHouseholdPool();
        }

        public int Count
        {
            get { return Households.Length; }
        }

        public bool IsSynchronized
        {
            get { return true; }
        }

        public string Name
        {
            get;
            set;
        }

        public bool OutOfData
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(50, 150, 50); }
        }

        public object SyncRoot
        {
            get { return this; }
        }

        private ITashaMode AutoMode
        {
            set
            {
                JointTripGenerator.Auto = value;
            }
        }

        private ITashaMode PassengerMode
        {
            set
            {
                JointTripGenerator.Passenger = value;
            }
        }

        private ISharedMode Rideshare
        {
            set
            {
                JointTripGenerator.RideShare = value;
            }
        }

        public void CopyTo(ITashaHousehold[] array, int index)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        private bool NeedsReset;

        [RunParameter("Skip Bad Households", false, "Should we continue to process skipping households with bad data?")]
        public bool SkipBadHouseholds;

        public IEnumerator<ITashaHousehold> GetEnumerator()
        {
            var blockingBuffer = new BlockingCollection<ITashaHousehold>(Environment.ProcessorCount * Environment.ProcessorCount);
            if (NeedsReset)
            {
                Reset();
            }
            Exception terminalException = null;
            var parallelLoader = Task.Factory.StartNew(() =>
                {
                    ITashaHousehold next;
                    try
                    {
                        EnsureReader();
                        if (SkipBadHouseholds)
                        {
                            do
                            {
                                try
                                {
                                    next = LoadNextHousehold();
                                    if (next == null)
                                    {
                                        break;
                                    }
                                    blockingBuffer.Add(next);
                                }
                                catch (XTMFRuntimeException)
                                {

                                }
                            } while (true);
                        }
                        else
                        {
                            do
                            {
                                next = LoadNextHousehold();
                                if (next == null)
                                {
                                    break;
                                }
                                blockingBuffer.Add(next);
                            } while (true);
                        }
                    }
                    catch (Exception e)
                    {
                        terminalException = e;
                    }
                    finally
                    {
                        blockingBuffer.CompleteAdding();
                        if (RefreshHouseholdData)
                        {
                            Unload();
                        }
                    }
                });
            foreach (var h in blockingBuffer.GetConsumingEnumerable())
            {
                yield return h;
            }
            parallelLoader.Wait();
            if (terminalException != null)
            {
                throw terminalException;
            }
            NeedsReset = true;
        }

        public void LoadData()
        {
            if (!LoadOnce || Households == null)
            {

                List<ITashaHousehold> ourList = new List<ITashaHousehold>(100000);
                LoadAll(ourList);
                Households = ourList.ToArray();
            }
        }

        public void Reset()
        {
            lock (this)
            {
                PersonLoader.Reset();
                if (Reader != null)
                {
                    Reader.Reset();
                    if (ContainsHeader)
                    {
                        Reader.LoadLine();
                    }
                    AllDataLoaded = Reader.EndOfFile;
                }
                NeedsReset = false;
            }
        }

        private void Unload()
        {
            PersonLoader.Unload();
            if (Reader != null)
            {
                Reader.Close();
                Reader = null;
            }
            NeedsReset = false;
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            JointTripGenerator.ObsMode = ObservedMode;
            var ibd = Root.InputBaseDirectory;
            if (ibd == null)
            {
                error = "The model system's input base directory was null!";
                return false;
            }
            AutoType = Root.AutoType;

            if (SecondVehicleColumnNumber >= 0)
            {
                if (Root.VehicleTypes == null)
                {
                    error = "In '" + Name + "' we were unable to get the alternative vehicle types for the secondary vehicle!";
                    return false;
                }
                foreach (var vt in Root.VehicleTypes)
                {
                    if (vt.VehicleName == SecondaryVehicleTypeName)
                    {
                        SecondaryType = vt;
                        break;
                    }
                }

                if (SecondaryType == null)
                {
                    error = "Could not find vehicle type '" + SecondaryVehicleTypeName + "'.";
                    return false;
                }
            }
            if (CalculateJointTrips && !string.IsNullOrWhiteSpace(RideshareModeName))
            {
                bool found = false;
                if (Root.SharedModes == null)
                {
                    error = "In '" + Name + "' we were unable to access any Shared Modes inside of '" + Root.Name + "' Please either turn off rideshare's mode swap or fix the model system!";
                    return false;
                }
                foreach (var sharedMode in Root.SharedModes)
                {
                    if (sharedMode.ModeName == RideshareModeName)
                    {
                        Rideshare = sharedMode;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    error = "In '" + Name + "' we were unable to find a shared mode called '" + RideshareModeName + "' to use for rideshare";
                    return false;
                }
                found = false;
                foreach (var nonSharedMode in Root.NonSharedModes)
                {
                    if (nonSharedMode.ModeName == AutoModeName)
                    {
                        AutoMode = nonSharedMode;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    error = "In '" + Name + "' we were unable to find a non shared mode called '" + AutoModeName + "' to use replace with rideshare";
                    return false;
                }
                found = false;
                foreach (var nonSharedMode in Root.AllModes)
                {
                    if (nonSharedMode.ModeName == PassengerModeName)
                    {
                        PassengerMode = nonSharedMode;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    error = "In '" + Name + "' we were unable to find a non shared mode called '" + PassengerModeName + "' to use replace with rideshare";
                    return false;
                }
            }
            return true;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public ITashaHousehold[] ToArray()
        {
            return Households;
        }

        public bool TryAdd(ITashaHousehold item)
        {
            throw new NotImplementedException();
        }

        public bool TryTake(out ITashaHousehold item)
        {
            EnsureReader();
            item = LoadNextHousehold();
            return item != null;
        }

        private static bool AgeDifferenceLessThan(int span, IList<ITashaPerson> people)
        {
            int age = -1;
            foreach (ITashaPerson p in people)
            {
                if (p.Adult)
                {
                    if (age == -1)
                    {
                        age = p.Age;
                    }
                    else
                    {
                        return (Math.Abs(age - p.Age) < span);
                    }
                }
            }
            return false;
        }

        private void AssertType(Household h)
        {
            if (h.Persons == null)
            {
                throw new XTMFRuntimeException(this, "Persons was null #" + h.HouseholdId + ".");
            }
            if (h.NumberOfChildren == 0)
            {
                GetTypeWithNoChildren(h);
            }
            else
            {
                GetTypeWithChildren(h);
            }
        }

        private static void GetTypeWithChildren(Household h)
        {
            if (h.NumberOfAdults == 1)
            {
                h.HhType = HouseholdType.LoneParentFamily;
            }
            else if (h.NumberOfAdults == 2)
            {
                if (h.Persons[0].Female == h.Persons[1].Female)
                {
                    h.HhType = HouseholdType.OtherFamily;
                }
                else
                {
                    if (AgeDifferenceLessThan(20, h.Persons))
                    {
                        h.HhType = HouseholdType.CoupleWithChildren;
                    }
                    else
                    {
                        h.HhType = HouseholdType.OtherFamily;
                    }
                }
            }
            else
            {
                h.HhType = HouseholdType.OtherFamily;
            }
        }

        private static void GetTypeWithNoChildren(Household h)
        {
            if (h.NumberOfAdults == 1)
            {
                h.HhType = HouseholdType.OnePerson;
            }
            else if (h.NumberOfAdults == 2)
            {
                if (h.Persons[0].Female == h.Persons[1].Female)
                {
                    h.HhType = HouseholdType.TwoOrMorePerson;
                }
                else
                {
                    if (AgeDifferenceLessThan(20, h.Persons))
                    {
                        h.HhType = HouseholdType.CoupleWithoutChildren;
                    }
                    else
                    {
                        h.HhType = HouseholdType.TwoOrMorePerson;
                    }
                }
            }
            else
            {
                h.HhType = HouseholdType.TwoOrMorePerson;
            }
        }

        private void EnsureReader()
        {
            if (Reader == null)
            {
                lock (this)
                {
                    if (Reader == null)
                    {
                        Reader = new CsvReader(Path.Combine(Root.InputBaseDirectory, FileName));
                        if (ContainsHeader)
                        {
                            Reader.LoadLine();
                        }
                        AllDataLoaded = Reader.EndOfFile;
                    }
                }
            }
        }

        private void LoadAll(List<ITashaHousehold> list)
        {
            lock (this)
            {
                if (Reader == null)
                {
                    Reader = new CsvReader(Path.Combine(Root.InputBaseDirectory, FileName));
                }
                Reset();
                if (ContainsHeader)
                {
                    Reader.LoadLine();
                }
                NeedsReset = true;
                AllDataLoaded = Reader.EndOfFile;
                while (LoadNextHousehold(list) && !AllDataLoaded)
                {
                }
            }
        }

        private bool LoadNextHousehold(IList<ITashaHousehold> list)
        {
            if (SkipBadHouseholds)
            {
                while (true)
                {
                    try
                    {
                        var h = LoadNextHousehold();
                        if (h != null)
                        {
                            list.Add(h);
                        }
                        return (h != null);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            else
            {
                var h = LoadNextHousehold();
                if (h != null)
                {
                    list.Add(h);
                }
                return (h != null);
            }
        }

        [SubModelInformation(Required = false, Description = "Saves the trips that were successfully loaded back to the given file, leave empty to not save.")]
        public FileLocation LoadedTripDump;

        private ITashaHousehold LoadNextHousehold()
        {
            Household h;
            bool loadnext;
            do
            {
                h = Household.MakeHousehold();
                if (Reader.LoadLine() == 0)
                {
                    AllDataLoaded = true;
                    OutOfData = true;
                    return null;
                }
                Reader.Get(out int tempInt, HouseholdIDCol);
                h.HouseholdId = tempInt;
                Reader.Get(out tempInt, ZoneCol);
                h.HomeZone = Root.ZoneSystem.Get(tempInt);
                if (h.HomeZone == null)
                {
                    Console.WriteLine("We were unable to find a household zone '" + tempInt.ToString() + "'");
                    throw new XTMFRuntimeException(this, "We were unable to find a household zone '" + tempInt.ToString() + "' for household #" + h.HouseholdId);
                }
                Reader.Get(out float tempFloat, ExpansionFactorCol);
                h.ExpansionFactor = tempFloat;
                Reader.Get(out int dwellingType, DwellingTypeCol);
                h.DwellingType = (DwellingType)dwellingType;

                Reader.Get(out tempInt, CarsCol);
                int numCars = tempInt;
                var tempVehicles = new List<IVehicle>(numCars);
                for (int i = 0; i < numCars; i++)
                {
                    tempVehicles.Add(Vehicle.MakeVehicle(AutoType));
                }
                if (SecondVehicleColumnNumber >= 0)
                {
                    Reader.Get(out tempInt, SecondVehicleColumnNumber);
                    int numSecondaryVechiles = tempInt;
                    for (int i = 0; i < numSecondaryVechiles; i++)
                    {
                        tempVehicles.Add(Vehicle.MakeVehicle(SecondaryType));
                    }
                }
                if (h.Vehicles == null || h.Vehicles.Length != tempVehicles.Count)
                {
                    h.Vehicles = tempVehicles.ToArray();
                }
                else
                {
                    for (int i = 0; i < h.Vehicles.Length; i++)
                    {
                        h.Vehicles[i] = tempVehicles[i];
                    }
                }
                if(IncomeCol >= 0)
                {
                    Reader.Get(out int incomeClass, IncomeCol);
                    h.IncomeClass = incomeClass;
                }
                PersonLoader.Load(h);
                AssertType(h);
                if (CalculateJointTrips)
                {
                    JointTripGenerator.Convert(h);
                }
                loadnext = false;
                var persons = h.Persons;
                for (int i = 0; i < persons.Length; i++)
                {
                    var person = persons[i];
                    if ((MinAge != 0) & (person.Age < MinAge))
                    {
                        loadnext = true;
                        break;
                    }
                    if (person.ExpansionFactor <= 0)
                    {
                        person.ExpansionFactor = h.ExpansionFactor;
                    }
                    var tripChains = person.TripChains;
                    for (int j = 0; j < tripChains.Count; j++)
                    {
                        var tc = person.TripChains[j];
                        if ((tc.Trips.Count == 1) | ((MaxNumberOfTripsInChain > 0) & (MaxNumberOfTripsInChain < tc.Trips.Count)))
                        {
                            if (!JustSkipSingleTripTripChainHouseholds)
                            {
                                if (tc.Trips.Count <= 1)
                                {
                                    throw new XTMFRuntimeException(this, "We found an invalid trip for Household '" + h.HouseholdId
                                        + "' Person '" + person.Id + "' From '" + tc.Trips[0].OriginalZone.ZoneNumber + "' To '"
                                        + tc.Trips[0].DestinationZone.ZoneNumber + "'.  Please check your data!");
                                }
                            }
                            loadnext = true;
                            break;
                        }
                    }
                    if (loadnext)
                    {
                        break;
                    }
                }
                if (loadnext)
                {
                    h.Recycle();
                }
            } while (loadnext);
            if (LoadedTripDump != null)
            {
                SaveHouseholdTrips(h);
            }
            return h;
        }
        private StreamWriter TripDump;

        [RunParameter("Refresh Household Data", false, "Resets the household data so that you can load a different population between runs in the multi-run framework.  Otherwise leave this as false.")]
        public bool RefreshHouseholdData;

        private void SaveHouseholdTrips(Household h)
        {
            if (TripDump == null)
            {
                TripDump = new StreamWriter(LoadedTripDump);
                TripDump.WriteLine("HouseholdID,PersonID,TripChain,TripNumber,StartTime,ObservedMode,OriginRegion,DesitinationRegion,OriginPD,DestinationPD,OriginZone,DestinationZone,Activity,Expansion Factor");
            }
            var writer = TripDump;
            for (int i = 0; i < h.Persons.Length; i++)
            {
                var person = (Person) h.Persons[i];
                for (int j = 0; j < person.TripChains.Count; j++)
                {
                    var tc = (TripChain) person.TripChains[j];
                    for (int k = 0; k < tc.Trips.Count; k++)
                    {
                        var trip = (Trip) tc.Trips[k];
                        var obsMode = (ITashaMode) trip["ObservedMode"];
                        writer.Write(h.HouseholdId);
                        writer.Write(',');
                        // person number
                        writer.Write(i);
                        writer.Write(',');
                        //trip chain number
                        writer.Write(j);
                        writer.Write(',');
                        // trip number
                        writer.Write(k);
                        writer.Write(',');
                        writer.Write(trip.TripStartTime);
                        writer.Write(',');
                        writer.Write(obsMode == null ? "NO_OBS" : obsMode.ModeName);
                        writer.Write(',');
                        //region
                        writer.Write(trip.OriginalZone.RegionNumber);
                        writer.Write(',');
                        writer.Write(trip.DestinationZone.RegionNumber);
                        writer.Write(',');
                        //pd
                        writer.Write(trip.OriginalZone.PlanningDistrict);
                        writer.Write(',');
                        writer.Write(trip.DestinationZone.PlanningDistrict);
                        writer.Write(',');
                        //zone
                        writer.Write(trip.OriginalZone.ZoneNumber);
                        writer.Write(',');
                        writer.Write(trip.DestinationZone.ZoneNumber);
                        writer.Write(',');
                        writer.Write(Enum.GetName(typeof(Activity), trip.Purpose));
                        writer.Write(',');
                        writer.WriteLine(person.ExpansionFactor);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Reader != null)
            {
                Reader.Dispose();
                Reader = null;
            }
            if (TripDump != null)
            {
                TripDump.Dispose();
                TripDump = null;
            }
        }
    }
}