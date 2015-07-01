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
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF.Editing;

namespace XTMF
{
    public class ParameterModel : INotifyPropertyChanged
    {
        internal ModuleParameter RealParameter;
        private ModelSystemEditingSession Session;

        public ParameterModel(ModuleParameter realParameter, ModelSystemEditingSession session)
        {
            IsDirty = false;
            RealParameter = realParameter;
            Session = session;
            Name = RealParameter.Name;
            _Value = _Value = RealParameter.Value != null ? RealParameter.Value.ToString() : string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsDirty { get;  private set; }

        public string Name { get; private set; }

        private string _Value;

        public string Value
        {
            get
            {
                return _Value;
            }
            private set
            {
                if(_Value != value)
                {
                    IsDirty = true;
                    _Value = value;
                    string error = null;
                    RealParameter.Value = ArbitraryParameterParser.ArbitraryParameterParse(RealParameter.Type, _Value, ref error);
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Value");
                }
            }
        }

        public string Description
        {
            get
            {
                return RealParameter.Description;
            }
        }

        public bool IsSystemParameter
        {
            get
            {
                return RealParameter.SystemParameter;
            }
        }

        public bool IsLinked
        {
            get
            {
                return Session.ModelSystemModel.LinkedParameters.GetContained(this) != null;
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
                if(RealParameter.QuickParameter != value)
                {
                    RealParameter.QuickParameter = value;
                    ModelHelper.PropertyChanged(PropertyChanged, this, "QuickParameter");
                }
            }
        }

        internal void SignalIsLinkedChanged()
        {
            ModelHelper.PropertyChanged(PropertyChanged, this, "IsLinked");
        }

        /// <summary>
        /// Update the value from the real parameter without issuing a command.
        /// </summary>
        internal void UpdateValueFromReal()
        {
            Value = RealParameter.Value.ToString();
        }

        private class ParameterChange
        {
            internal string OldValue;
            internal string NewValue;
            internal LinkedParameterModel ContainedIn;
        }

        /// <summary>
        /// Attempts to set the value of the parameter to the new given value
        /// </summary>
        /// <param name="newValue">The value to change the parameter to</param>
        /// <param name="error">If the value is invalid this will contain a message as to why.</param>
        /// <returns>True if the parameter was set to the new value, false otherwise with an error message in error.</returns>
        public bool SetValue(string newValue, ref string error)
        {
            ParameterChange change = new ParameterChange();
            return Session.RunCommand(XTMFCommand.CreateCommand(
                // do
                ((ref string e) =>
                {
                    // Check to see if we are in a linked parameter
                    change.ContainedIn = Session.ModelSystemModel.LinkedParameters.GetContained(this);
                    if(change.ContainedIn == null)
                    {
                        change.NewValue = newValue;
                        change.OldValue = _Value;
                        if(!ArbitraryParameterParser.Check(RealParameter.Type, change.NewValue, ref e))
                        {
                            return false;
                        }
                        Value = change.NewValue;
                    }
                    else
                    {
                        change.NewValue = newValue;
                        change.OldValue = change.ContainedIn.GetValue();
                        return change.ContainedIn.SetWithoutCommand(change.NewValue, ref e);
                    }
                    return true;
                }),
                // undo
                (ref string e) =>
                {
                    if(change.ContainedIn == null)
                    {
                        Value = change.OldValue;
                    }
                    else
                    {
                        return change.ContainedIn.SetWithoutCommand(change.OldValue, ref e);
                    }
                    return true;
                },
                // redo
                (ref string e) =>
                {
                    if(change.ContainedIn == null)
                    {
                        Value = change.NewValue;
                    }
                    else
                    {
                        return change.ContainedIn.SetWithoutCommand(change.NewValue, ref e);
                    }
                    return true;
                }
                ), ref error);
        }

        /// <summary>
        /// Get the linked parameter model that contains this parameter.
        /// </summary>
        /// <returns>The linked parameter model, null if this parameter is not contained.</returns>
        public LinkedParameterModel GetLinkedParameter()
        {
            return Session.ModelSystemModel.LinkedParameters.GetContained(this);
        }


        /// <summary>
        /// Set the parameter to the default value
        /// </summary>
        /// <param name="error">If there is an error this will contain a message</param>
        /// <returns>If the parameter was set to it's default</returns>
        public bool SetToDefault(ref string error)
        {
            var def = RealParameter.GetDefault();
            if(def == null)
            {
                error = "We were unable to find a default value for this parameter.";
                return false;
            }
            if(Session.ModelSystemModel.LinkedParameters.GetContained(this) != null)
            {
                error = "You can not set a parameter that is inside of a linked parameter back to default";
                return false;
            }
            return SetValue(def.ToString(), ref error);
        }
    }
}