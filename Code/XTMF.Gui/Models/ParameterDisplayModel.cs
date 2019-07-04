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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XTMF.Gui.Models
{
    internal sealed class ParameterDisplayModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly List<string> BoolValueList = new List<string>(new[] { "True", "False" });

        private readonly LinkedParameterModel _linkedParameterModel;

        private readonly bool _MultipleSelected;

        internal readonly ParameterModel RealParameter;

        public bool SetOnce = false;

        /// <summary>
        /// </summary>
        /// <param name="realParameter"></param>
        /// <param name="multipleSelected"></param>
        public ParameterDisplayModel(ParameterModel realParameter, bool multipleSelected = false)
        {
            RealParameter = realParameter;
            _MultipleSelected = multipleSelected;
            realParameter.PropertyChanged += RealParameter_PropertyChanged;
            FontColour = RealParameter.IsHidden ? Brushes.DarkGray : Brushes.White;


            _linkedParameterModel = realParameter.GetLinkedParameter();
        }

        public ParameterDisplayModel(ModelSystemStructureDisplayModel realParameter, bool multipleSelected = false)
        {
            //RealParameter = realParameter.BaseModel.;
            _MultipleSelected = multipleSelected;
            realParameter.PropertyChanged += RealParameter_PropertyChanged;
            FontColour = RealParameter.IsHidden ? Brushes.DarkGray : Brushes.White;
        }

        public Type ParameterType => RealParameter.Type;

        /// <summary>
        /// </summary>
        public Visibility ModuledDisabledIconVisiblity
        {
            get
            {
                if (_linkedParameterModel == null)
                {
                    var s = IsDisabledByDesdencence(RealParameter);
                    var c = s ? Visibility.Visible : Visibility.Collapsed;
                    return c;
                }

                var parameters = _linkedParameterModel.GetParameters();
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

        /// <summary>
        /// </summary>
        public string OwnerModuleName => RealParameter.BelongsToModel.Name;

        public string Name => GetName(_MultipleSelected);

        public string Description => RealParameter.Description;

        /// <summary>
        /// </summary>
        public string Value
        {
            get
            {
                if (RealParameter.IsLinked)
                {
                    if (RealParameter.Type == typeof(bool))
                    {
                        bool outParse = false;
                        if (bool.TryParse(RealParameter.GetLinkedParameter()?.GetValue(), out outParse))
                        {
                            return outParse.ToString();
                        }

                    }

                    return RealParameter.GetLinkedParameter()?.GetValue();


                }

                return RealParameter.Value;
            }
            set => SetValue(value, out _);
        }

        public Visibility SystemParameterVisibility =>
            RealParameter.IsSystemParameter ? Visibility.Visible : Visibility.Collapsed;

        public Visibility LinkedParameterVisibility =>
            RealParameter.IsLinked ? Visibility.Visible : Visibility.Collapsed;

        public string LinkedParameterName => RealParameter.GetLinkedParameter()?.Name ?? string.Empty;

        public bool QuickParameter
        {
            get => RealParameter.QuickParameter;
            set => RealParameter.QuickParameter = value;
        }

        public string QuickParameterName => GetName(true);

        public Brush FontColour { get; set; }

        public bool IsEnumeration => RealParameter.Type.IsEnum;

        public List<string> PossibleEnumerationValues => RealParameter.Type == typeof(bool)
            ? BoolValueList
            : RealParameter.Type.GetEnumNames().ToList();

        public IModelSystemStructure BelongsTo => RealParameter.BelongsTo;

        public void Dispose()
        {
            RealParameter.PropertyChanged -= RealParameter_PropertyChanged;
            PropertyChanged = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool SetValue(string value, out string error)
        {

            error = null;
            // only update if something changed
            if (value != RealParameter.Value)
            {
                if (value != null && !RealParameter.SetValue(value, ref error))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Determines if this parameter is associated with a disabled module by descendence, that is, this method returns true
        ///     if the associated module or any of its ancestors are disabled.
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static bool IsDisabledByDesdencence(ParameterModel parameter)
        {
            //in the case of the root module
            if (parameter.BelongsToModel.Parent == null && parameter.IsDisabled)
            {
                return true;
            }

            if (parameter.BelongsToModel.Parent == null && !parameter.IsDisabled)
            {
                return false;
            }

            var hasDisabledParent = false;
            var m = parameter.BelongsToModel;
            do
            {
                if (m.IsDisabled)
                {
                    hasDisabledParent = true;
                    break;
                }

                //move up until null
                m = m.Parent;
            } while (m != null);

            return hasDisabledParent;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RealParameter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var property = e.PropertyName;
            if (e.PropertyName == "IsLinked")
            {
            }
            else if (e.PropertyName == "IsHidden")
            {
                FontColour = RealParameter.IsHidden ? Brushes.DarkGray : Brushes.White;
                property = nameof(FontColour);
            }
            else if (property == "QuickParameter" && sender != null)
            {
                //RealParameter.QuickParameter = !RealParameter.QuickParameter;
            }

            ModelHelper.PropertyChanged(PropertyChanged, this, property);
        }

        /// <summary>
        ///     Get the true name of the parameter without any module information
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

        private string GetName(bool includeModuleName)
        {
            return includeModuleName ? RealParameter.Name + " : " + RealParameter.BelongsTo.Name : RealParameter.Name;
        }

        /// <summary>
        ///     Change the name of the parameter
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

        /// <summary>
        ///     Create the display model from the parameter model.
        /// </summary>
        /// <param name="parameterModel">The parameters in the model</param>
        /// <returns>An observable collection of the parameters using the display model</returns>
        internal static ObservableCollection<ParameterDisplayModel> CreateParameters(
            IEnumerable<ParameterModel> parameterModel, bool multipleSelected = false)
        {
            return new ObservableCollection<ParameterDisplayModel>(parameterModel.Select(p =>
                new ParameterDisplayModel(p, multipleSelected)));
        }

        /// <summary>
        /// </summary>
        /// <param name="newLP"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        internal bool AddToLinkedParameter(LinkedParameterModel newLP, ref string error)
        {
            return newLP.AddParameter(RealParameter, ref error);
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        internal LinkedParameterModel GetLinkedParameter()
        {
            return RealParameter.GetLinkedParameter();
        }

        /// <summary>
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
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