/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
        private readonly ModelSystemEditingSession _Session;

        public ParametersModel(ModelSystemStructureModel modelSystemStructure, ModelSystemEditingSession session)
        {
            _Session = session;
            ModelSystemStructure = modelSystemStructure;
            Parameters = CreateParameterModels(modelSystemStructure, _Session);
        }

        public bool IsDirty => Parameters == null || Parameters.Any(p => p.IsDirty);

        public ModelSystemStructureModel ModelSystemStructure { get; private set; }

        internal List<ParameterModel> Parameters { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private static List<ParameterModel> CreateParameterModels(ModelSystemStructureModel modelSystemStructure, ModelSystemEditingSession Session)
        {
            var realParameters = modelSystemStructure.RealModelSystemStructure.Parameters?.Parameters;
            if (realParameters == null) return null;
            var ret = new List<ParameterModel>(realParameters.Count);
            for (int i = 0; i < realParameters.Count; i++)
            {
                ret.Add(new ParameterModel(realParameters[i] as ModuleParameter, Session, modelSystemStructure));
            }
            return ret;
        }

        public ObservableCollection<ParameterModel> GetParameters()
        {
            return Parameters != null ? Parameters.ToObservableCollection() : new ObservableCollection<ParameterModel>();
        }

        /// <summary>
        /// Call this to check if there is a parameter name or
        /// a parameter value that contains the given filter text
        /// </summary>
        /// <param name="filterText">The text to filter for.</param>
        /// <returns>True if it was found, false otherwise.</returns>
        public bool HasParameterContaining(string filterText)
        {
            if (Parameters is null) return false;
            foreach (var param in Parameters)
            {
                if (param.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)
                    || param.Value.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        internal void Update()
        {
            var parameters = Parameters;
            var modelSystemStructure = ModelSystemStructure;
            if (modelSystemStructure.RealModelSystemStructure.Parameters == null) return;
            var realParameters = modelSystemStructure.RealModelSystemStructure.Parameters.Parameters;
            if (realParameters == null) return;
            if (Parameters != null)
            {
                Parameters.Clear();
            }
            else
            {
                Parameters = parameters = new List<ParameterModel>(realParameters.Count);
            }
            for (int i = 0; i < realParameters.Count; i++)
            {
                parameters.Add(new ParameterModel(realParameters[i] as ModuleParameter, _Session, modelSystemStructure));
            }
            ModelHelper.PropertyChanged(PropertyChanged, this, nameof(Parameters));
        }
    }
}