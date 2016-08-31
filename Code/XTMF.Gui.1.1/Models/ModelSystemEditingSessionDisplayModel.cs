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
using XTMF.Gui.UserControls;

namespace XTMF.Gui.Models
{
    public sealed class ModelSystemEditingSessionDisplayModel : ActiveEditingSessionDisplayModel
    {

        private readonly ModelSystemEditingSession Session;

        private readonly ModelSystemDisplay Display;

        public ModelSystemEditingSessionDisplayModel(ModelSystemDisplay display) : base(true)
        {
            var session = display.Session;
            Display = display;
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
            RebuildCommandList(UndoList, Session.CopyOnUndo());
            RebuildCommandList(RedoList, Session.CopyOnRedo());
            SetUndo(Session.CanUndo);
            SetRedo(Session.CanRedo);
            InvokeParameterChanged(nameof(CanUndo));
            InvokeParameterChanged(nameof(CanRedo));
            InvokeParameterChanged(nameof(UndoName));
            InvokeParameterChanged(nameof(RedoName));
        }

        public override string UndoName
        {
            get
            {
                return $"Undo {(UndoList.Count > 0 ? MaxLength(UndoList[0].Command.Name, 20) : String.Empty)} (Ctrl+Z)";
            }
        }

        public override string RedoName
        {
            get
            {
                return $"Redo {(RedoList.Count > 0 ? MaxLength(RedoList[0].Command.Name, 20) : String.Empty)} (Ctrl+Y)";
            }
        }

        private string MaxLength(string str, int length)
        {
            if(str.Length < length)
            {
                return str;
            }
            return str.Substring(0, length - 3) + "...";
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
        override public bool CanUndo
        {
            get
            {
                return _CanUndo;
            }
        }

        override public bool CanRedo
        {
            get
            {
                return _CanRedo;
            }
        }

        private void SetUndo(bool value)
        {
            if (_CanUndo != value)
            {
                _CanUndo = value;
                InvokeParameterChanged(nameof(CanUndo));
            }
        }

        private void SetRedo(bool value)
        {
            if (_CanRedo != value)
            {
                _CanRedo = value;
                InvokeParameterChanged(nameof(CanRedo));
            }
        }

        public override bool CanSave
        {
            get
            {
                return true;
            }
        }

        public override bool CanSaveAs
        {
            get
            {
                return true;
            }
        }

        internal override void Undo()
        {
            Display.Undo();
        }

        internal override void Redo()
        {
            Display.Redo();
        }

        public override void Save()
        {
            Display.SaveRequested(false);
        }

        public override void SaveAs()
        {
            Display.SaveRequested(true);
        }

        public override bool CanExecuteRun
        {
            get
            {
                return !Session.IsRunning;
            }
        }

    }
}
