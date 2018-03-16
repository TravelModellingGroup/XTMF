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

        private readonly bool _MultipleSelected;

        public Visibility ModuledDisabledIconVisiblity
        {
            get
            {
                if (this._linkedParameterModel == null)
                {
                    var s = IsDisabledByDesdencence(RealParameter);
                    var c = (s) ? Visibility.Visible : Visibility.Collapsed;
                    return c;
                }
                else
                {
                    var parameters = this._linkedParameterModel.GetParameters();
                    foreach (var s in parameters)
                    {
                        if (!IsDisabledByDesdencence(s))
                        {
                            return Visibility.Collapsed;
                        }
                    }
                    return Visibility.Visible;
                }
            }

        }

        /// <summary>
        /// Determines if this parameter is associated with a disabled module by descendence, that is, this method returns true
        /// if the associated module or any of its ancestors are disabled.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static bool IsDisabledByDesdencence(ParameterModel parameter)
        {
            //in the case of the root module
            if(parameter.BelongsToModel.Parent == null && parameter.IsDisabled)
            {
                return true;
            }
            else if(parameter.BelongsToModel.Parent == null && !parameter.IsDisabled)
            {
                return false;
            }
            bool hasDisabledParent = false;
            ModelSystemStructureModel m = parameter.BelongsToModel;
            do
            {
                if(m.IsDisabled)
                {
                    hasDisabledParent = true;
                    break;
                }
                else
                {
                    //move up until null
                    m = m.Parent;
                }

            } while (m != null);

            return hasDisabledParent;
        }

        private LinkedParameterModel _linkedParameterModel;

        public ParameterDisplayModel(ParameterModel realParameter, bool multipleSelected = false)
        {
            RealParameter = realParameter;
            _MultipleSelected = multipleSelected;
            realParameter.PropertyChanged += RealParameter_PropertyChanged;
            FontColour = RealParameter.IsHidden ? Brushes.DarkGray : Brushes.White;


            this._linkedParameterModel = realParameter.GetLinkedParameter();


        }

        public ParameterDisplayModel(ModelSystemStructureDisplayModel realParameter, bool multipleSelected = false)
        {
            //RealParameter = realParameter.BaseModel.;
            _MultipleSelected = multipleSelected;
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
            else if (e.PropertyName == "IsHidden")
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
        internal string GetBaseName() => RealParameter.Name;

        ~ParameterDisplayModel()
        {
            Dispose();
        }

        public void Dispose()
        {
            RealParameter.PropertyChanged -= RealParameter_PropertyChanged;
            PropertyChanged = null;
        }

        public string Name => GetName(_MultipleSelected);

        public string Description => RealParameter.Description;

        public string Value
        {
            get => RealParameter.Value;
            set
            {
                // only update if something changed
                if (value != RealParameter.Value)
                {
                    string error = null;
                    if (value != null)
                    {
                        if (!RealParameter.SetValue(value, ref error))
                        {
                            MessageBox.Show(MainWindow.Us, "We were unable to set the parameter '" + Name + "' with the value '" + value + "'.\r\n" + error, "Unable to Set Parameter",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        public Visibility SystemParameterVisibility => RealParameter.IsSystemParameter ? Visibility.Visible : Visibility.Collapsed;

        public Visibility LinkedParameterVisibility => RealParameter.IsLinked ? Visibility.Visible : Visibility.Collapsed;

        public string LinkedParameterName => RealParameter.GetLinkedParameter()?.Name ?? String.Empty;

        public bool QuickParameter
        {
            get => RealParameter.QuickParameter;
            set => RealParameter.QuickParameter = value;
        }

        public string QuickParameterName => GetName(true);

        private string GetName(bool includeModuleName)
        {
            return includeModuleName ? RealParameter.Name + " : " + RealParameter.BelongsTo.Name : RealParameter.Name;
        }

        /// <summary>
        /// Change the name of the parameter
        /// </summary>
        /// <param name="newName">The new name to give the parameter</param>
        /// <param name="error">A message in case of an error</param>
        /// <returns>True if successful, otherwise returns an error message</returns>
        public bool SetName(string newName, ref string error) => RealParameter.SetName(newName, ref error);

        internal bool RevertNameToDefault(ref string error) => RealParameter.RevertNameToDefault(ref error);

        public bool SetHidden(bool hidden, ref string error) => RealParameter.SetHidden(hidden, ref error);

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




        internal bool AddToLinkedParameter(LinkedParameterModel newLP, ref string error) => newLP.AddParameter(RealParameter, ref error);

        internal LinkedParameterModel GetLinkedParameter() => RealParameter.GetLinkedParameter();

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

        internal bool ResetToDefault(ref string error) => RealParameter.SetToDefault(ref error);

        public bool IsEnumeration => RealParameter.Type.IsEnum;

        private static readonly List<string> BoolValueList = new List<string>(new string[] { "True", "False" });

        public List<string> PossibleEnumerationValues => RealParameter.Type == typeof(bool) ? BoolValueList : RealParameter.Type.GetEnumNames().ToList();

        public IModelSystemStructure BelongsTo => RealParameter.BelongsTo;

        public bool SetOnce = false;
    }

    public class ParameterTypeSelector : DataTemplateSelector
    {
        public DataTemplate Enumeration { get; set; }

        public DataTemplate Standard { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ParameterDisplayModel param)
            {
                if (param.IsEnumeration)
                {
                    return Enumeration;
                }
                if (param.RealParameter.Type == typeof(bool))
                {
                    return Enumeration;
                }
            }
            return Standard;
        }
    }
}

