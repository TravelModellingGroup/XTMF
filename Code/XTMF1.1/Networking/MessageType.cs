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
using System.IO;

namespace XTMF.Networking
{
    internal enum MessageType
    {
        Quit = 0,
        PostProgess = 1,
        PostCancel = 2,
        PostResource = 3,
        PostComplete = 4,
        PostMachineName = 5,
        RequestProgress = 6,
        RequestResource = 7,
        ReturningResource = 8,
        SendModelSystem = 9,
        SendCustomMessage = 10,
        ReceiveCustomMessage = 11,
        WriteToHostConsole = 12,
        Length = 13
    }

    internal class ReceiveCustomMessageMessage
    {
        internal int CustomMessageNumber;
        internal MemoryStream Stream;
    }

    internal class SendCustomMessageMessage
    {
        internal int CustomMessageNumber;
        internal object Data;
    }
}