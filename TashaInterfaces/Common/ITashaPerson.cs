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
using System.Collections.Generic;
using TMG;

namespace Tasha.Common
{
    /// <summary>
    /// Represents how to interface with a person
    /// </summary>
    public interface ITashaPerson : IAttachable
    {
        /// <summary>
        /// Is this person an adult
        /// </summary>
        bool Adult { get; }

        /// <summary>
        /// How old is the person
        /// </summary>
        int Age { get; }

        /// <summary>
        /// These are trip chains that serve passengers
        /// </summary>
        List<ITripChain> AuxTripChains { get; set; }

        /// <summary>
        /// Is this person a child (Age = [0,10])
        /// </summary>
        bool Child { get; }

        /// <summary>
        /// How is this person currently employed
        /// </summary>
        TTSEmploymentStatus EmploymentStatus { get; }

        /// <summary>
        /// Get the zone that this person is employed at,
        /// null if they are not employed
        /// </summary>
        IZone EmploymentZone { get; }

        /// <summary>
        /// The expansion factor for this individual
        /// </summary>
        float ExpansionFactor { get; set; }

        /// <summary>
        /// Is this person Female?
        /// </summary>
        bool Female { get; }

        bool FreeParking { get; }

        /// <summary>
        /// The household that this person belongs to
        /// </summary>
        ITashaHousehold Household { get; }

        /// <summary>
        /// What is the identifier for this person
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Does this person have a driver's license
        /// </summary>
        bool Licence { get; }

        /// <summary>
        /// Is this person Male?
        /// </summary>
        bool Male { get; }

        /// <summary>
        /// What type of job does this person have
        /// </summary>
        Occupation Occupation { get; }

        /// <summary>
        /// Get which zone this person goes to school at.
        /// null if they do not go to school
        /// </summary>
        IZone SchoolZone { get; set; }

        //List<ITripChain>[] AuxTripChainsPerIteration { get; set; }
        /// <summary>
        /// How this person is a student
        /// </summary>
        StudentStatus StudentStatus { get; }

        TransitPass TransitPass { get; }

        /// <summary>
        /// These are the trip chains this person will go on
        /// </summary>
        List<ITripChain> TripChains { get; }

        /// <summary>
        /// Is this person a YoungAdult (Age = [16, 19])
        /// </summary>
        bool YoungAdult { get; }

        /// <summary>
        /// Is this person a Youth (Age = [11, 15])
        /// </summary>
        bool Youth { get; }

        ITashaPerson Clone();

        void Recycle();
    }
}