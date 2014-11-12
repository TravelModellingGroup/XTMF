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
using XTMF;

namespace TMG.AgentBased.Tours
{
    /// <summary>
    /// An activity episode defines a particular instance of
    /// an activity in a person's schedule
    /// </summary>
    public interface IActivityEpisode
    {
        /// <summary>
        /// What type of activity is this?
        /// </summary>
        IActivity Activity { get; }

        /// <summary>
        /// The end time of the activity
        /// </summary>
        Time End { get; set; }

        /// <summary>
        /// The duration that this activity originally had
        /// </summary>
        Time OriginalDuration { get; }

        /// <summary>
        /// The start time of the activity
        /// </summary>
        Time Start { get; set; }
    }
}