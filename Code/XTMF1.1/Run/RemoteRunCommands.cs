/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace XTMF.Run
{
    public enum ToHost
    {
        Heartbeat = 0,
        ClientReady = 1,
        ClientExiting = 2,
        ClientFinishedModelSystem = 3,
        ClientErrorWhenRunningModelSystem = 4,
        ClientErrorValidatingModelSystem = 5,
        ClientReportedProgress = 6,
        SendModelSystemResult = 7,
        ClientReportedStatus = 8,
        RuntimeError = 9
    }

    public enum ToClient
    {
        Heartbeat = 0,
        RunModelSystem = 1,
        CancelModelRun = 2,
        KillModelRun = 3,
        RequestProgress = 4,
        RequestStatus = 5
    }
}
