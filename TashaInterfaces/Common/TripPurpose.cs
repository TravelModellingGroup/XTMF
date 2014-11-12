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
namespace Tasha.Common
{
    /// <summary>
    /// Describes what activities are possible for an
    /// ActivityEpisode
    /// </summary>
    public enum Activity
    {
        /// <summary>
        ///
        /// </summary>
        PrimaryWork,

        /// <summary>
        ///
        /// </summary>
        School,

        /// <summary>
        /// Or other types of shopping
        /// </summary>
        Market,

        /// <summary>
        /// Market with a familly member
        /// </summary>
        JointMarket,

        /// <summary>
        ///
        /// </summary>
        IndividualOther,

        /// <summary>
        ///
        /// </summary>
        WorkBasedBusiness,

        /// <summary>
        ///
        /// </summary>
        WorkAtHomeBusiness,

        /// <summary>
        ///
        /// </summary>
        SecondaryWork,

        /// <summary>
        ///
        /// </summary>
        StayAtHome,

        /// <summary>
        ///
        /// </summary>
        Home,

        /// <summary>
        /// Lunch perhaps
        /// </summary>
        ReturnFromWork,

        /// <summary>
        ///
        /// </summary>
        ReturnFromSchool,

        /// <summary>
        ///
        /// </summary>
        JointOther,

        /// <summary>
        ///
        /// </summary>
        ServeDespendents,

        /// <summary>
        ///
        /// </summary>
        NullActivity,

        /// <summary>
        ///
        /// </summary>
        FacilitatePassenger,

        /// <summary>
        ///
        /// </summary>
        Unknown,

        /// <summary>
        ///
        /// </summary>
        Daycare,

        /// <summary>
        ///
        /// </summary>
        PickupAndReturn,

        /// <summary>
        ///
        /// </summary>
        DropoffAndReturn,

        /// <summary>
        ///
        /// </summary>
        Pickup,

        /// <summary>
        ///
        /// </summary>
        Dropoff,

        /// <summary>
        /// access station
        /// </summary>
        Intermediate,

        /// <summary>
        /// Used in the scheduler to describe a trip episode
        /// before it is scheduled
        /// </summary>
        Travel,

        /// <summary>
        /// The number of different activities that are defined
        /// </summary>
        NumberOfActivities
    }
}