/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Input;
using XTMF;
using System.Collections.Generic;

namespace Tasha.Validation.Report;

/// <summary>
/// Provides access to the Microsim Data when building a validation report.
/// </summary>
[ModuleInformation(Description = "Provides access to the Microsim Data when building a validation report.")]
public sealed class MicrosimData : IModule
{

    /// <summary>
    /// The location of the Microsim Households file.
    /// </summary>
    [SubModelInformation(Required = true, Description = "The location of the Microsim Households file.")]
    public FileLocation HouseholdsFile;

    /// <summary>
    /// The location of the Microsim Persons file.
    /// </summary>
    [SubModelInformation(Required = true, Description = "The location of the Microsim Persons file.")]
    public FileLocation PersonsFile;

    /// <summary>
    /// The location of the Microsim Trips file.
    /// </summary>
    [SubModelInformation(Required = true, Description = "The location of the Microsim Trips file.")]
    public FileLocation TripsFile;

    /// <summary>
    /// The location of the Microsim Modes file.
    /// </summary>
    [SubModelInformation(Required = true, Description = "The location of the Microsim Modes file.")]
    public FileLocation ModesFile;

    [RunParameter("Mode Choice Iterations", 10, "The number of iterations that the mode choice uses.")]
    public int ModeChoiceIterations;

    /// <summary>
    /// Gets or sets the name of the MicrosimData.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the progress of the MicrosimData.
    /// </summary>
    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    /// <summary>
    /// Performs runtime validation for the MicrosimData.
    /// </summary>
    /// <param name="error">The error message if validation fails.</param>
    /// <returns>True if validation passes, false otherwise.</returns>
    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    /// <summary>
    /// A representation of the household records from Microsim.
    /// </summary>
    internal HashSet<MicrosimHousehold> Households { get; private set; }

    /// <summary>
    /// A dictionary that maps household IDs to lists of MicrosimPerson records.
    /// </summary>
    internal Dictionary<int, List<MicrosimPerson>> Persons { get; private set; }

    /// <summary>
    /// A dictionary that maps (householdID, personID) tuples to lists of MicrosimTrip records.
    /// </summary>
    internal Dictionary<(int householdID, int personID), List<MicrosimTrip>> Trips { get; private set; }

    /// <summary>
    /// A dictionary that maps (householdID, personID, tripID) tuples to MicrosimTripMode records.
    /// </summary>
    internal Dictionary<(int householdID, int personID, int tripID), List<MicrosimTripMode>> Modes { get; private set; }

    /// <summary>
    /// Loads the MicrosimData.
    /// </summary>
    internal void Load()
    {
        Households = MicrosimHousehold.LoadHouseholds(this, HouseholdsFile);
        Persons = MicrosimPerson.LoadPersons(this, PersonsFile);
        Trips = MicrosimTrip.LoadTrips(this, TripsFile);
        Modes = MicrosimTripMode.LoadModes(this, ModesFile);
    }

    /// <summary>
    /// Unloads the MicrosimData.
    /// </summary>
    internal void Unload()
    {
        Households = null;
        Persons = null;
        Trips = null;
        Modes = null;
    }

}
