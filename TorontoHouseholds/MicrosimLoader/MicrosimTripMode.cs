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
using System.Collections.Generic;
using System.IO;
using XTMF;
using TMG.Input;
using Datastructure;

namespace TMG.Tasha.MicrosimLoader;

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
    /// The mode of the trip
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

    private MicrosimTripMode(int householdID, int personID, int tripID, string mode, float departureTime, float arrivalTime)
    {
        HouseholdID = householdID;
        PersonID = personID;
        TripID = tripID;
        Mode = mode;
        DepartureTime = departureTime;
        ArrivalTime = arrivalTime;
    }

    /// <summary>
    /// Read in a dictionary of mode records from the given Microsim file
    /// </summary>
    /// <param name="callingModule">The module invoking the call</param>
    /// <param name="tripFile">The location of the trips file to load.</param>
    /// <returns>A dictionary of all of the loaded trips indexed by the combination of the household, person, trip, and mode ids.</returns>
    internal static Dictionary<(int householdID, int personID, int tripID), MicrosimTripMode> LoadModes(IModule callingModule, FileLocation modesFile)
    {
        var fileInfo = new FileInfo(modesFile.GetFilePath());
        if (!fileInfo.Exists)
        {
            throw new XTMFRuntimeException(callingModule, $"The file \"{fileInfo.FullName}\" does not exist!");
        }
        var ret = new Dictionary<(int householdID, int personID, int tripID), MicrosimTripMode>(10000000);
        using (var reader = new CsvReader(fileInfo))
        {
            // burn the header
            reader.LoadLine();
            while (reader.LoadLine(out int columns))
            {
                if (columns >= 7)
                {
                    reader.Get(out int householdID, 0);
                    reader.Get(out int personID, 1);
                    reader.Get(out int tripID, 2);
                    reader.Get(out string mode, 3);
                    reader.Get(out float departureTime, 4);
                    reader.Get(out float arrivalTime, 5);
                    // We only need to get 1 of these records
                    if (!ret.ContainsKey((householdID, personID, tripID)))
                    {
                        ret[(householdID, personID, tripID)] = new MicrosimTripMode(householdID, personID, tripID, mode, departureTime, arrivalTime);
                    }
                }
            }
        }
        return ret;
    }
}
