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

namespace TMG;

public interface INetworkAssignment : IModule
{
    /// <summary>
    /// This is to be executed before the zone system is loaded
    /// </summary>
    void RunModelSystemSetup();

    /// <summary>
    /// This is run after the zone system is loaded
    /// </summary>
    void RunInitialAssignments();

    /// <summary>
    /// This is run every iteration
    /// </summary>
    void RunNetworkAssignment();

    /// <summary>
    /// This is run after all iterations have been completed
    /// </summary>
    void RunPostAssignments();
}