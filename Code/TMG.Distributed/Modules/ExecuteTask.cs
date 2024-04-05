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
using System;
using XTMF;

namespace TMG.Distributed.Modules;

[ModuleInformation(
    Description = "This module is designed to execute a task with the given name from the IHostDistributionManager.  In order to wait for it to complete use a module to WaitForAll."
    )]
public class ExecuteTask : ISelfContainedModule
{
    [RunParameter("Task Name", "TheName", "The name of the task to execute.")]
    public string TaskName;

    [RootModule]
    public IHostDistributionManager Root;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public void Start()
    {
        Root.AddTask(TaskName);
    }

    public bool RuntimeValidation(ref string error)
    {
        if(!Root.Client.HasTaskWithName(TaskName))
        {
            error = "In '" + Name + "' a task with the name '" + TaskName + "' was requested however no client side task with that name was found!";
            return false;
        }
        return true;
    }
}
