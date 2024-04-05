/*
    Copyright 2021 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
internal sealed class MicrosimTrip
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
    /// A string representation of the purpose of the trip they are leaving from
    /// </summary>
    internal readonly string OriginPurpose;
    /// <summary>
    /// The zone number of where the trip starts
    /// </summary>
    internal readonly int OriginZone;
    /// <summary>
    /// A string representation of the purpose that the trip is going to.
    /// </summary>
    internal readonly string DestinationPurpose;
    /// <summary>
    /// The zone number of where the trip ends
    /// </summary>
    internal readonly int DestinationZone;
    // Ignore weight

    private MicrosimTrip(int householdID, int personID, int tripID, string originPurpose, int originZone, string destinationPurpose, int destinationZone)
    {
        HouseholdID = householdID;
        PersonID = personID;
        TripID = tripID;
        OriginPurpose = originPurpose;
        OriginZone = originZone;
        DestinationPurpose = destinationPurpose;
        DestinationZone = destinationZone;
    }

    /// <summary>
    /// Read in a dictionary of trip records from the given Microsim file
    /// </summary>
    /// <param name="callingModule">The module invoking the call</param>
    /// <param name="tripFile">The location of the trips file to load.</param>
    /// <returns>A dictionary of all of the loaded trips indexed by the combination of the household, person, and trip ids.</returns>
    internal static Dictionary<(int householdID, int personID), List<MicrosimTrip>> LoadTrips(IModule callingModule, FileLocation tripFile)
    {
        var fileInfo = new FileInfo(tripFile.GetFilePath());
        if (!fileInfo.Exists)
        {
            throw new XTMFRuntimeException(callingModule, $"The file \"{fileInfo.FullName}\" does not exist!");
        }
        var ret = new Dictionary<(int householdID, int personID), List<MicrosimTrip>>(10000000);
        using(var reader = new CsvReader(fileInfo))
        {
            // burn the header
            reader.LoadLine();
            while(reader.LoadLine(out int columns))
            {
                if (columns >= 7)
                {
                    reader.Get(out int householdID, 0);
                    reader.Get(out int personID, 1);
                    reader.Get(out int tripID, 2);
                    reader.Get(out string originPurpose, 3);
                    reader.Get(out int originZone, 4);
                    reader.Get(out string destinationPurpose, 5);
                    reader.Get(out int destinationZone, 6);
                    if (!ret.TryGetValue((householdID, personID), out var trips))
                    {
                        ret[(householdID, personID)] = trips = new List<MicrosimTrip>(4);
                    }
                    trips.Add(new MicrosimTrip(householdID, personID, tripID, originPurpose, originZone,
                        destinationPurpose, destinationZone));
                }
            }
        }
        return ret;
    }
}
