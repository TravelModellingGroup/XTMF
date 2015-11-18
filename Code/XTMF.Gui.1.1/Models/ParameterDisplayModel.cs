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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;

namespace XTMF.Gui.Models
{
    sealed class ParameterDisplayModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        internal readonly ParameterModel RealParameter;

        public ParameterDisplayModel(ParameterModel realParameter)
        {
            RealParameter = realParameter;
            realParameter.PropertyChanged += RealParameter_PropertyChanged;
        }

        private void RealParameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var property = e.PropertyName;
            if(e.PropertyName == "IsLinked")
            {
                property = "LinkedParameterVisibility";
            }
            ModelHelper.PropertyChanged(PropertyChanged, this, property);
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

        public string Value
        {
            get
            {
                return RealParameter.Value;
            }

            set
            {
                // only update if something changed
                if(value != RealParameter.Value)
                {
                    string error = null;
                    if(!RealParameter.SetValue(value, ref error))
                    {
                        MessageBox.Show(MainWindow.Us, "We were unable to set the parameter '" + Name + "' with the value '" + value + "'.\r\n" + error, "Unable to Set Parameter",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        public Visibility SystemParameterVisibility
        {
            get
            {
                return RealParameter.IsSystemParameter ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public Visibility LinkedParameterVisibility
        {
            get
            {
                return RealParameter.IsLinked ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public string LinkedParameterName
        {
            get
            {
                var lp = RealParameter.GetLinkedParameter();
                return lp != null ? lp.Name : String.Empty;
            }
        }

        public bool QuickParameter
        {
            get
            {
                return RealParameter.QuickParameter;
            }
            set
            {
                RealParameter.QuickParameter = value;
            }
        }

        public string QuickParameterName
        {
            get
            {
                return Name + " : " + RealParameter.BelongsTo.Name;
            }
        }

        /// <summary>
        /// Create the display model from the parameter model.
        /// </summary>
        /// <param name="parameterModel">The parameters in the model</param>
        /// <returns>An observable collection of the parameters using the display model</returns>
        internal static ObservableCollection<ParameterDisplayModel> CreateParameters(IOrderedEnumerable<ParameterModel> parameterModel)
        {
            return new ObservableCollection<ParameterDisplayModel>(parameterModel.Select(p => new ParameterDisplayModel(p)));
        }

        internal bool AddToLinkedParameter(LinkedParameterModel newLP, ref string error)
        {
            return newLP.AddParameter(RealParameter, ref error);
        }

        internal LinkedParameterModel GetLinkedParameter()
        {
            return RealParameter.GetLinkedParameter();
        }

        internal bool RemoveLinkedParameter(ref string error)
        {
            var lp = GetLinkedParameter();
            if(lp == null)
            {
                error = "This parameter is not in contained in a linked parameter";
                return false;
            }
            return lp.RemoveParameter(RealParameter, ref error);
        }

        internal bool ResetToDefault(ref string error)
        {
            return RealParameter.SetToDefault(ref error);
        }
    }
}
