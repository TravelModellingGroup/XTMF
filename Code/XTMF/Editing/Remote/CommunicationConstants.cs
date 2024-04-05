/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace XTMF.Editing.Remote;

/// <summary>
/// Component definitions
/// </summary>
internal enum ComponentTypes
{
    LinkedParameterModel = 0,
    LinkedParametersModel = 1,
    ModelSystemEditingSession = 2,
    ModelSystemModel = 3,
    ModelSystemStructureModel = 4,
    ModuleParameter = 5,
    ModuleParameters = 6,
    ParameterModel = 7,
    ParametersModel = 8,
    ProjectEditingSession = 9
}

internal enum LinkedParameterModelCommands
{
}
internal enum LinkedParametersModelCommands
{
}
internal enum ModelSystemEditingSessionCommands
{
}
internal enum ModelSystemModelCommands
{
}
internal enum ModelSystemStructureModelCommands
{
}
internal enum ModuleParameterCommands
{
}
internal enum ModuleParametersCommands
{
}
internal enum ParameterModelCommands
{
}
internal enum ParametersModelCommands
{
}
internal enum ProjectEditingSessionCommands
{
}


internal enum LinkedParameterModelUpdate
{
}
internal enum LinkedParametersModelUpdate
{
}
internal enum ModelSystemEditingSessionUpdate
{
}
internal enum ModelSystemModelUpdate
{
}
internal enum ModelSystemStructureModelUpdate
{
}
internal enum ModuleParameterUpdate
{
}
internal enum ModuleParametersUpdate
{
}
internal enum ParameterModelUpdate
{
}
internal enum ParametersModelUpdate
{
}
internal enum ProjectEditingSessionUpdate
{
}
