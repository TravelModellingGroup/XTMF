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
    public class ModelSystemEditingSessionDisplayModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly ModelSystemEditingSession Session;

        public ModelSystemEditingSessionDisplayModel(ModelSystemEditingSession session)
        {
            Session = session;
            _CanUndo = Session.CanUndo;
            _CanRedo = Session.CanRedo;
            Session.CommandExecuted += Session_CommandExecuted;
        }

        private void Session_CommandExecuted(object sender, EventArgs e)
        {
            CanUndo = Session.CanUndo;
            CanRedo = Session.CanRedo;
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
