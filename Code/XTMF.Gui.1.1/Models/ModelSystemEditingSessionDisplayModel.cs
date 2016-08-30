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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF.Editing;

namespace XTMF.Gui.Models
{
    public sealed class ModelSystemEditingSessionDisplayModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly ModelSystemEditingSession Session;

        public ModelSystemEditingSessionDisplayModel(ModelSystemEditingSession session)
        {
            Session = session;
            _CanUndo = Session.CanUndo;
            _CanRedo = Session.CanRedo;
            UndoList = new ObservableCollection<CommandDisplayModel>();
            RedoList = new ObservableCollection<CommandDisplayModel>();
            Session.CommandExecuted += Session_CommandExecuted;
        }

        public ObservableCollection<CommandDisplayModel> UndoList;

        public ObservableCollection<CommandDisplayModel> RedoList;

        private void Session_CommandExecuted(object sender, EventArgs e)
        {
            CanUndo = Session.CanUndo;
            CanRedo = Session.CanRedo;
            RebuildCommandList(UndoList, Session.CopyOnUndo());
            RebuildCommandList(RedoList, Session.CopyOnRedo());
        }

        private static void RebuildCommandList(ObservableCollection<CommandDisplayModel> list,
            List<XTMFCommand> commands)
        {
            //Step 1 find things that are removed
            var removed = list.Where(e => !commands.Contains(e.Command)).ToList();
            //Step 2 remove
            foreach (var toRemove in removed)
            {
                list.Remove(toRemove);
            }
            //Step 3 find new things
            var newCommands = commands.Where(c => !list.Any(m => m.Command == c))
                                .Select((c, i)=>new { Command = c, Index = i })
                                .OrderBy(c => c.Index)
                                .ToList();
            //Step 4 add new things
            foreach(var toAdd in newCommands)
            {
                list.Insert(toAdd.Index, new CommandDisplayModel(toAdd.Command));
            }
        }

        private bool _CanUndo;
        private bool _CanRedo;
        public bool CanUndo
        {
            get
            {
                return _CanUndo;
            }
            private set
            {
                if(_CanUndo != value)
                {
                    _CanUndo = value;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(CanUndo));
                }
            }
        }

        public bool CanRedo
        {
            get
            {
                return _CanRedo;
            }
            private set
            {
                if (_CanRedo != value)
                {
                    _CanRedo = value;
                    ModelHelper.PropertyChanged(PropertyChanged, this, nameof(CanRedo));
                }
            }
        }

    }
}
