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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Gui.Models
{
    /// <summary>
    /// This class is designed to be the model for the different pages and
    /// how they interact with the main window.
    /// </summary>
    public class ActiveEditingSessionDisplayModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public virtual bool CanUndo => false;

        public virtual bool CanRedo => false;

        public virtual bool CanSave => false;

        public virtual bool CanSaveAs => false;

        public virtual string SaveName => "_Save (Ctrl+S)";
        public virtual string SaveAsName => "Save _As";

        public virtual string UndoName => "Undo (Ctrl+Z)";
        public virtual string RedoName => "Redo (Ctrl+Y)";

        public ActiveEditingSessionDisplayModel(bool canClose)
        {
            CanClose = canClose;
        }

        public bool CanClose { get; private set; }

        public virtual bool CanExecuteRun => true;

        protected void InvokeParameterChanged(string parameterName)
        {
            ModelHelper.PropertyChanged(PropertyChanged, this, parameterName);
        }

        public virtual void Save() => throw new InvalidOperationException();

        public virtual void SaveAs() => throw new InvalidOperationException();

        internal virtual void Redo() => throw new InvalidOperationException();

        internal virtual void Undo() => throw new InvalidOperationException();
    }
}
