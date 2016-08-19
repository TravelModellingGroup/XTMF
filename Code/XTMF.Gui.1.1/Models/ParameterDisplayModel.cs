/*
    Copyright 2015-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Windows.Controls;
using System.Windows.Media;

namespace XTMF.Gui.Models
{
    sealed class ParameterDisplayModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        internal readonly ParameterModel RealParameter;

        private readonly bool MultipleSelected;

        public ParameterDisplayModel(ParameterModel realParameter, bool multipleSelected = false)
        {
            RealParameter = realParameter;
            MultipleSelected = multipleSelected;
            realParameter.PropertyChanged += RealParameter_PropertyChanged;
            FontColour = RealParameter.IsHidden ? Brushes.DarkGray : Brushes.White;
        }

        private void RealParameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var property = e.PropertyName;
            if (e.PropertyName == "IsLinked")
            {
                property = nameof(LinkedParameterVisibility);
            }
            else if(e.PropertyName == "IsHidden")
            {
                FontColour = RealParameter.IsHidden ? Brushes.DarkGray : Brushes.White;
                property = nameof(FontColour);
            }
            ModelHelper.PropertyChanged(PropertyChanged, this, property);
        }

        /// <summary>
        /// Get the true name of the parameter without any module information
        /// </summary>
        /// <returns></returns>
        internal string GetBaseName()
        {
            return RealParameter.Name;
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

        public string Name { get { return GetName(MultipleSelected); } }

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
                if (value != RealParameter.Value)
                {
                    string error = null;
                    if (!RealParameter.SetValue(value, ref error))
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
                return GetName(true);
            }
        }

        private string GetName(bool includeModuleName)
        {
            if (includeModuleName)
            {
                return RealParameter.Name + " : " + RealParameter.BelongsTo.Name;
            }
            else
            {
                return RealParameter.Name;
            }
        }

        /// <summary>
        /// Change the name of the parameter
        /// </summary>
        /// <param name="newName">The new name to give the parameter</param>
        /// <param name="error">A message in case of an error</param>
        /// <returns>True if successful, otherwise returns an error message</returns>
        public bool SetName(string newName, ref string error)
        {
            return RealParameter.SetName(newName, ref error);
        }

        internal bool RevertNameToDefault(ref string error)
        {
            return RealParameter.RevertNameToDefault(ref error);
        }

        public bool SetHidden(bool hidden, ref string error)
        {
            return RealParameter.SetHidden(hidden, ref error);
        }

        public Brush FontColour { get; set; }

        /// <summary>
        /// Create the display model from the parameter model.
        /// </summary>
        /// <param name="parameterModel">The parameters in the model</param>
        /// <returns>An observable collection of the parameters using the display model</returns>
        internal static ObservableCollection<ParameterDisplayModel> CreateParameters(IOrderedEnumerable<ParameterModel> parameterModel, bool multipleSelected = false)
        {
            return new ObservableCollection<ParameterDisplayModel>(parameterModel.Select(p => new ParameterDisplayModel(p, multipleSelected)));
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
            if (lp == null)
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

        public bool IsEnumeration
        {
            get
            {
                return RealParameter.Type.IsEnum;
            }
        }

        public List<string> PossibleEnumerationValues
        {
            get
            {
                return RealParameter.Type.GetEnumNames().ToList();
            }
        }

        public IModelSystemStructure BelongsTo
        {
            get
            {
                return RealParameter.BelongsTo;
            }
        }
    }

    public class ParameterTypeSelector : DataTemplateSelector
    {
        public DataTemplate Enumeration { get; set; }

        public DataTemplate Standard { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var param = item as ParameterDisplayModel;
            if (param != null)
            {
                if (param.IsEnumeration)
                {
                    return Enumeration;
                }
            }
            return Standard;
        }
    }
}

