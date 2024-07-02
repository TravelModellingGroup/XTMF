/*
    Copyright 2021-2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.Report;

/// <summary>
/// A record storing the results of the Trip data from Microsim
/// </summary>
internal sealed class MicrosimTripMode
{
    /// <summary>
    /// The unique id for the household
    /// </summary>
    internal readonly int HouseholdID;

    /// <summary>
    /// The unique id for the person within the household
    /// </summary>
    internal readonly int PersonID;

    /// <summary>
    /// The unique id for the trip in the person's day
    /// </summary>
    internal readonly int TripID;

    /// <summary>
    /// The mode that was used for this instance of a trip.
    /// </summary>
    internal readonly string Mode;

    /// <summary>
    /// The start time of the trip.
    /// </summary>
    internal readonly float DepartureTime;

    /// <summary>
    /// The end time of the trip.
    /// </summary>
    internal readonly float ArrivalTime;

    /// <summary>
    /// The number of times this mode was used.
    /// </summary>
    internal readonly int Weight;

    private MicrosimTripMode(int householdID, int personID, int tripID, string mode, float departureTime, float arrivalTime, int weight)
    {
        HouseholdID = householdID;
        PersonID = personID;
        TripID = tripID;
        Mode = mode;
        DepartureTime = departureTime;
        ArrivalTime = arrivalTime;
        Weight = weight;
    }

    /// <summary>
    /// Read in a dictionary of mode records from the given Microsim file
    /// </summary>
    /// <param name="callingModule">The module invoking the call</param>
    /// <param name="tripFile">The location of the trips file to load.</param>
    /// <returns>A dictionary of all of the loaded trips indexed by the combination of the household, person, trip, and mode ids.</returns>
    internal static Dictionary<(int householdID, int personID, int tripID), List<MicrosimTripMode>> LoadModes(IModule callingModule, FileLocation modesFile)
    {
        var fileInfo = new FileInfo(modesFile.GetFilePath());
        if (!fileInfo.Exists)
        {
            throw new XTMFRuntimeException(callingModule, $"The file \"{fileInfo.FullName}\" does not exist!");
        }
        var ret = new Dictionary<(int householdID, int personID, int tripID), List<MicrosimTripMode>>(10000000);
        using var reader = new CsvReader(fileInfo);

        // burn the header
        reader.LoadLine();
        while (reader.LoadLine(out int columns))
        {
            if (columns < 7)
            {
                continue;
            }
            reader.Get(out int householdID, 0);
            reader.Get(out int personID, 1);
            reader.Get(out int tripID, 2);
            reader.Get(out string mode, 3);
            reader.Get(out float departureTime, 4);
            reader.Get(out float arrivalTime, 5);
            reader.Get(out int weight, 6);
            // We only need to get 1 of these records
            var key = (householdID, personID, tripID);
            var toAdd = new MicrosimTripMode(householdID, personID, tripID, mode, departureTime, arrivalTime, weight);
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(ret, key, out bool exists);
            if (!exists)
            {
                list = [toAdd];
            }
            else
            {
                list.Add(toAdd);
            }
        }

        return ret;
    }

}
