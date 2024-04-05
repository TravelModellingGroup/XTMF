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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;
using TMG.Input;
using Datastructure;

namespace TMG.Tasha.MicrosimLoader;

/// <summary>
/// A record storing the results of the household data from Microsim
/// </summary>
internal sealed class MicrosimHousehold
{
    /// <summary>
    /// The unique identifier for the household
    /// </summary>
    internal readonly int HouseholdID;

    /// <summary>
    /// The zone number that the household resides in
    /// </summary>
    internal readonly int HomeZone;

    /// <summary>
    /// The expansion factor for this household
    /// </summary>
    internal readonly float Weight;

    /// <summary>
    /// The Dwelling type index for the household
    /// </summary>
    internal readonly int DwellingType;

    /// <summary>
    /// The number of vehicles that the household has
    /// </summary>
    internal readonly int Vehicles;

    /// <summary>
    /// The TTS income class for the household
    /// </summary>
    internal readonly int IncomeClass;

    private MicrosimHousehold(int householdID, int homeZone, float weight, int dwellingType, int vehicles, int incomeClass)
    {
        HouseholdID = householdID;
        HomeZone = homeZone;
        Weight = weight;
        DwellingType = dwellingType;
        Vehicles = vehicles;
        IncomeClass = incomeClass;
    }

    /// <summary>
    /// Read in a dictionary of household records from the given Microsim file
    /// </summary>
    /// <param name="callingModule">The module requesting this read.</param>
    /// <param name="householdsFile">The location to read from.</param>
    /// <returns>A mapping based on household number to get the record.</returns>
    internal static HashSet<MicrosimHousehold> LoadHouseholds(IModule callingModule, FileLocation householdsFile)
    {
        var fileInfo = new FileInfo(householdsFile.GetFilePath());
        if (!fileInfo.Exists)
        {
            throw new XTMFRuntimeException(callingModule, $"The file \"{fileInfo.FullName}\" does not exist!");
        }
        var ret = new HashSet<MicrosimHousehold>();
        using (var reader = new CsvReader(fileInfo))
        {
            // burn the header
            reader.LoadLine();
            try
            {
                while (reader.LoadLine(out var columns))
                {
                    if (columns >= 7)
                    {
                        reader.Get(out int householdID, 0);
                        reader.Get(out int homeZone, 1);
                        reader.Get(out float weight, 2);
                        reader.Get(out int dwellingType, 4);
                        reader.Get(out int vehicles, 5);
                        reader.Get(out int incomeClass, 6);
                        ret.Add(new MicrosimHousehold(householdID, homeZone, weight, dwellingType, vehicles, incomeClass));
                    }
                }
            }
            catch(IOException e)
            {
                throw new XTMFRuntimeException(callingModule, e, $"Failed when reading in the households in file \"{fileInfo.FullName}\" on line number {reader.LineNumber}!\r\n{e.Message}");
            }
        }
        return ret;
    }
}
