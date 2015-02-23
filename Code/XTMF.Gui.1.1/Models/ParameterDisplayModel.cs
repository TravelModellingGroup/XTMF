/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace XTMF.Gui.Models
{
    sealed class ParameterDisplayModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ParameterModel RealParameter;

        public ParameterDisplayModel(ParameterModel realParameter)
        {
            RealParameter = realParameter;
            realParameter.PropertyChanged += RealParameter_PropertyChanged;
        }

        private void RealParameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ModelHelper.PropertyChanged(PropertyChanged, this, e.PropertyName);
        }

        ~ParameterDisplayModel()
        {
            Dispose();
        }

        public void Dispose()
        {
            RealParameter.PropertyChanged -= RealParameter_PropertyChanged;
            PropertyChanged = null;
        }

        public string Name { get { return RealParameter.Name; } }

        public string Description { get { return RealParameter.Description; } }

        public string Value { get { return RealParameter.Value; } }

    }
}
