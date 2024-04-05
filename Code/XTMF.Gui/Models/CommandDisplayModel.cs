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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using XTMF.Editing;

namespace XTMF.Gui.Models;

public sealed class CommandDisplayModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public string Name { get; private set; }

    internal XTMFCommand Command { get; private set; }

    public CommandDisplayModel(XTMFCommand command)
    {
        Command = command;
        Name = command.Name;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
