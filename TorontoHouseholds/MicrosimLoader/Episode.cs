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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using Tasha.Scheduler;
using XTMF;

namespace TMG.Tasha.MicrosimLoader
{
    /// <summary>
    /// This class will be used to re-run the LocationChoice for RemoveActivities
    /// </summary>
    public sealed class Episode : IEpisode
    {
        public ISchedule ContainingSchedule { get; set; }

        public ITashaPerson Owner { get; set; }

        public Activity ActivityType { get; set; }

        public Time EndTime { get; set; }

        public Time StartTime { get; set; }

        public Time Duration { get; set; }

        public Time OriginalDuration { get; set; }

        public Time TravelTime { get; set; }

        public IZone Zone { get; set; }
    }
}
