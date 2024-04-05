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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Gui.Models;

public class ValidationErrorDisplayModel : INotifyPropertyChanged
{
    #pragma warning disable CS0067
    public event PropertyChangedEventHandler PropertyChanged;

    public string ErrorString { get; private set; }

    public string ModuleName => DisplayModule?.Name ?? "Unknown Module";

    public ModelSystemStructureDisplayModel DisplayModule { get; }

    public ValidationErrorDisplayModel(ModelSystemStructureDisplayModel root, string error, IReadOnlyList<int> path)
    {
        ErrorString = error;
        if(path == null)
        {
            DisplayModule = null;
        }
        else if(path.Count == 0)
        {
            DisplayModule = root;
        }
        else
        {
            // make a copy of the path in case something else is also going to use it
            DisplayModule = MapModuleWithPath(root, [.. path]);
        }
    }

    private ModelSystemStructureDisplayModel MapModuleWithPath(ModelSystemStructureDisplayModel root, List<int> path)
    {
        var current = root;
        for (int i = 0; i < path.Count; i++)
        {
            current = current.Children[path[i]];
        }
        return current;
    }
}
