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
        internal readonly ModuleParameter RealParameter;

        private readonly ModelSystemEditingSession _Session;

        public ParameterModel(ModuleParameter realParameter, ModelSystemEditingSession session)
        {
            IsDirty = false;
            RealParameter = realParameter;
            _Session = session;
            _Value = _Value = RealParameter.Value != null ? RealParameter.Value.ToString() : string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsDirty { get; private set; }

        public string Name => RealParameter.Name;

        private string _Value;

        public string Value
        {
            get
            {
                return _Value;
            }
            private set
            {
                if (_Value != value)
                {
                    IsDirty = true;
                    _Value = value;
                    string error = null;
                    RealParameter.Value = ArbitraryParameterParser.ArbitraryParameterParse(RealParameter.Type, _Value, ref error);
                    ModelHelper.PropertyChanged(PropertyChanged, this, "Value");
                }
            }
        }

        public string Description => RealParameter.Description;

        public bool IsSystemParameter => RealParameter.SystemParameter;

        public bool IsLinked => _Session.ModelSystemModel.LinkedParameters.GetContained(this) != null;

        public bool IsHidden => RealParameter.IsHidden;

        public bool QuickParameter
        {
            get => RealParameter.QuickParameter;
            set
            {
                if (RealParameter.QuickParameter != value)
                {
                    string error = null;
                    _Session.RunCommand(XTMFCommand.CreateCommand(
                        value ? "Add Quick Parameter" : "Remove Quick Parameter",
                        (ref string erro) =>
                        {
                            RealParameter.QuickParameter = value;
                            ModelHelper.PropertyChanged(PropertyChanged, this, "QuickParameter");
                            return true;
                        }, (ref string erro) =>
                        {
                            RealParameter.QuickParameter = !value;
                            ModelHelper.PropertyChanged(PropertyChanged, this, "QuickParameter");
                            return true;
                        }, (ref string erro) =>
                        {
                            RealParameter.QuickParameter = value;
                            ModelHelper.PropertyChanged(PropertyChanged, this, "QuickParameter");
                            return true;
                        }), ref error);

                }
            }
        }

        public bool SetHidden(bool hide, ref string error)
        {
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                hide == true ? "Hide Parameter" : "Show Parameter",
                    (ref string erro) =>
                    {
                        RealParameter.IsHidden = hide;
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsHidden));
                        return true;
                    }, (ref string erro) =>
                    {
                        RealParameter.IsHidden = !hide;
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsHidden));
                        return true;
                    }, (ref string erro) =>
                    {
                        RealParameter.IsHidden = hide;
                        ModelHelper.PropertyChanged(PropertyChanged, this, nameof(IsHidden));
                        return true;
                    }), ref error);
        }

        public IModelSystemStructure BelongsTo => RealParameter.BelongsTo;

        /// <summary>
        /// Get the type of the parameter
        /// </summary>
        public Type Type => RealParameter.Type;

        public int Index => RealParameter.Index;

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
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                "Change Parameter",
                // do
                ((ref string e) =>
                {
                    // Check to see if we are in a linked parameter
                    change.ContainedIn = _Session.ModelSystemModel.LinkedParameters.GetContained(this);
                    if (change.ContainedIn == null)
                    {
                        change.NewValue = newValue;
                        change.OldValue = _Value;
                        if (!ArbitraryParameterParser.Check(RealParameter.Type, change.NewValue, ref e))
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
                    if (change.ContainedIn == null)
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
                    if (change.ContainedIn == null)
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
            return _Session.ModelSystemModel.LinkedParameters.GetContained(this);
        }

        /// <summary>
        /// Set the parameter to the default value
        /// </summary>
        /// <param name="error">If there is an error this will contain a message</param>
        /// <returns>If the parameter was set to it's default</returns>
        public bool SetToDefault(ref string error)
        {
            var def = RealParameter.GetDefault();
            if (def == null)
            {
                error = "We were unable to find a default value for this parameter.";
                return false;
            }
            if (_Session.ModelSystemModel.LinkedParameters.GetContained(this) != null)
            {
                error = "You can not set a parameter that is inside of a linked parameter back to default";
                return false;
            }
            return SetValue(def.ToString(), ref error);
        }

        /// <summary>
        /// Give a parameter a friendly name
        /// </summary>
        /// <param name="newName">The name to assign to the parameter</param>
        /// <param name="error">In case of an error, a message of why it occurred</param>
        /// <returns>True if the operation succeeds, false with an error message otherwise.</returns>
        public bool SetName(string newName, ref string error)
        {
            if (String.IsNullOrWhiteSpace(newName))
            {
                error = $"The parameter '{Name}'s new name must not be blank or whitespace only!";
                return false;
            }
            var oldName = Name;
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                "Set Parameter Name",
                (ref string e) =>
                {
                    return RealParameter.SetName(newName, ref e);
                },
                (ref string e) =>
                {
                    return RealParameter.SetName(oldName, ref e);
                },
                (ref string e) =>
                {
                    return RealParameter.SetName(newName, ref e);
                }), ref error);
        }

        /// <summary>
        /// Revert the name of the parameter back to the default name
        /// </summary>
        /// <param name="error">In case of an error, a message of why it occurred</param>
        /// <returns>True if the operation succeeds, false with an error message otherwise.</returns>
        public bool RevertNameToDefault(ref string error)
        {
            var oldName = Name;
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                "Revert Parameter Name",
                (ref string e) =>
                {
                    return RealParameter.SetName(RealParameter.NameOnModule, ref e);
                },
                (ref string e) =>
                {
                    return RealParameter.SetName(oldName, ref e);
                },
                (ref string e) =>
                {
                    return RealParameter.SetName(RealParameter.NameOnModule, ref e);
                }), ref error);
        }
    }
}
