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

namespace Tasha.Common;

public class ActivityConverter : IActivityConverter
{
    #region IActivityConverter Members

    public static IActivityConverter Converter = new ActivityConverter();

    public Activity GetActivity(char destination)
    {
        switch ( destination )
        {
            case 'W':
                return Activity.PrimaryWork;
            case 'H':
                return Activity.Home;
            case 'C':
            case 'S':
                return Activity.School;
            case 'F':
                return Activity.FacilitatePassenger;
            case 'M':
                return Activity.Market;
            case 'O':
                return Activity.IndividualOther;
            case 'D':
                return Activity.Daycare;
            case '9':
                return Activity.Unknown;
            case 'R':
                return Activity.SecondaryWork;
            case 'L':
                return Activity.ReturnFromWork;
            case 'B':
                return Activity.WorkBasedBusiness;
            default:
                return Activity.IndividualOther;
        }
    }

    public char GetActivityChar(Activity activity)
    {
        switch ( activity )
        {
            case Activity.PrimaryWork:
                return 'W';

            case Activity.Home:
                return 'H';

            case Activity.School:
                return 'S';

            case Activity.FacilitatePassenger:
                return 'F';

            case Activity.Market:
                return 'M';

            case Activity.IndividualOther:
                return 'O';

            case Activity.Intermediate:
                return 'I';

            case Activity.Daycare:
                return 'D';

            case Activity.SecondaryWork:
                return 'R';

            case Activity.Unknown:
                return '9';

            case Activity.WorkBasedBusiness:
                return 'B';

            case Activity.ReturnFromWork:
                return 'L';

            default:
                return 'O';
        }
    }

    public void GetTripActivities(ITrip trip, ITripChain chain, out char origin, out char destination)
    {
        destination = GetActivityChar( trip.Purpose );

        List<ITrip> trips = chain.Trips;

        for ( int i = 0; i < trips.Count; i++ )
        {
            if ( trips[i] == trip )
            {
                if ( i == 0 )
                {
                    origin = GetActivityChar( Activity.Home );
                }
                else
                {
                    origin = GetActivityChar( trips[i - 1].Purpose );
                }

                return;
            }
        }

        origin = '0';
    }

    #endregion IActivityConverter Members
}