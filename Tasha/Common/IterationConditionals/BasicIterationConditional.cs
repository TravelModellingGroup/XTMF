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
using XTMF;

namespace Tasha.Common.IterationConditionals;

[ModuleInformation(Description = "This module is designed to allow a conditional flow when executing")]
public sealed class BasicIterationConditional : IterationConditional, ISelfContainedModule
{
    [SubModelInformation(Required = false, Description = "Executed if true")]
    public ISelfContainedModule[] IfTrue;

    [SubModelInformation(Required = false, Description = "Executed if false")]
    public ISelfContainedModule[] IfFalse;

    public void Start()
    {
        var toExecute = DoesIterationPass() ? IfTrue : IfFalse;

        if (toExecute != null)
        {
            foreach (var child in toExecute)
            {
                child.Start();
            }
        }
    }
}
