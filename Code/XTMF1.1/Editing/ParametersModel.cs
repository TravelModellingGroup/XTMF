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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace XTMF
{
    public class ParametersModel : INotifyPropertyChanged
    {
        private ModelSystemEditingSession Session;

        public ParametersModel(ModelSystemStructureModel modelSystemStructure, ModelSystemEditingSession session)
        {
            Session = session;
            ModelSystemStructure = modelSystemStructure;
            Parameters = CreateParameterModels(modelSystemStructure, Session);
        }

        public bool IsDirty
        {
            get
            {
                var parameters = this.Parameters;
                if(parameters == null) return false;
                for(int i = 0; i < parameters.Count; i++)
                {
                    if(parameters[i].IsDirty) return true;
                }
                return false;
            }
        }

        public ModelSystemStructureModel ModelSystemStructure { get; private set; }
        internal List<ParameterModel> Parameters { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private static List<ParameterModel> CreateParameterModels(ModelSystemStructureModel modelSystemStructure, ModelSystemEditingSession Session)
        {
            if(modelSystemStructure.RealModelSystemStructure.Parameters == null) return null;
            var realParameters = modelSystemStructure.RealModelSystemStructure.Parameters.Parameters;
            if(realParameters == null) return null;
            var ret = new List<ParameterModel>(realParameters.Count);
            for(int i = 0; i < realParameters.Count; i++)
            {
                ret.Add(new ParameterModel(realParameters[i] as ModuleParameter, Session));
            }
            return ret;
        }

        public ObservableCollection<ParameterModel> GetParameters()
        {
            //return this.Parameters != null ? this.Parameters.ToList() : new List<ParameterModel>();
            return this.Parameters != null ? this.Parameters.ToObservableCollection() : new ObservableCollection<ParameterModel>();
        }
    }
}