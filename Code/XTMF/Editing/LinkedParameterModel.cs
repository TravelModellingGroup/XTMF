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
using System.Runtime.CompilerServices;
using System.Text;
using XTMF.Editing;

namespace XTMF
{
    /// <summary>
    /// The linked parameter model provides an interface into the logic of a linked parameter without directly changing them
    /// </summary>
    public class LinkedParameterModel : INotifyPropertyChanged
    {
        /// <summary>
        /// The parameters that are attached to this linked parameter
        /// </summary>
        private ObservableCollection<ParameterModel> _ParameterModels;

        private ModelSystemEditingSession _Session;

        private ModelSystemModel _ModelSystem;

        public LinkedParameterModel(ILinkedParameter linkedParameter, ModelSystemEditingSession session, ModelSystemModel modelSystem)
        {
            _Session = session;
            _ModelSystem = modelSystem;
            _ParameterModels = CreateInitialParameterModels(linkedParameter, _ModelSystem);
            RealLinkedParameter = linkedParameter as LinkedParameter;
        }

        private static ObservableCollection<ParameterModel> CreateInitialParameterModels(ILinkedParameter linkedParameter, ModelSystemModel modelSystem)
        {
            var ret = new ObservableCollection<ParameterModel>();
            var real = linkedParameter.Parameters;
            if(real != null)
            {
                for(int i = 0; i < real.Count; i++)
                {
                    ParameterModel pm = modelSystem.GetParameterModel(real[i]);
                    if(pm != null)
                    {
                        ret.Add(pm);
                    }
                }
            }
            return ret;
        }

        private object _ParameterModelsLock = new object();

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The linked parameters being represented
        /// </summary>
        internal LinkedParameter RealLinkedParameter { get; set; }

        /// <summary>
        /// Gets the name of the linked parameter
        /// </summary>
        /// <returns>The name of the linked parameter</returns>
        public string Name => RealLinkedParameter.Name;

        /// <summary>
        /// Sets the name of the linked parameter
        /// </summary>
        /// <param name="newName">The desired new name</param>
        /// <param name="error">A message describing why the operation failed.</param>
        /// <returns>True if the name was changed, false otherwise.</returns>
        public bool SetName(string newName, ref string error)
        {
            lock (_ParameterModelsLock)
            {
                string oldName = RealLinkedParameter.Name;
                return _Session.RunCommand(XTMFCommand.CreateCommand("Set Name", (ref string e) =>
                {
                    RealLinkedParameter.Name = newName;
                    return true;
                },
                (ref string e) =>
                {
                    RealLinkedParameter.Name = oldName;
                    return true;
                },
                (ref string e) =>
                {
                    RealLinkedParameter.Name = newName;
                    return true;
                }), ref error);
            }
        }

        public List<ParameterModel> GetParameters()
        {
            lock (_ParameterModelsLock)
            {
                var models = _ParameterModels;
                return models != null ? models.ToList() : new List<ParameterModel>();
            }
        }



        private sealed class LinkedParameterChange
        {
            internal LinkedParameterModel OriginalContainedIn;
            internal int OriginalIndex;
            internal int Index;
        }

        internal bool Contains(ParameterModel toCheck)
        {
            lock (_ParameterModelsLock)
            {
                return _ParameterModels.Contains(toCheck);
            }
        }

        /// <summary>
        /// Add a new parameter to this linked parameter
        /// </summary>
        /// <param name="toAdd">The parameter to add</param>
        /// <param name="error">This contains an error message if this returns false</param>
        /// <returns>True if we added the parameter to the linked parameter, false if it failed.</returns>
        public bool AddParameter(ParameterModel toAdd, ref string error)
        {
            LinkedParameterChange change = new LinkedParameterChange();
            var originalValue = toAdd.Value;
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                "Add Parameter to Linked Parameter",
                // do
                (ref string e) =>
                {
                    if(_ParameterModels.Contains(toAdd))
                    {
                        e = "The parameter was already contained in the linked parameter!";
                        return false;
                    }
                    // remove from the linked parameter it was already in
                    if((change.OriginalContainedIn = _ModelSystem.LinkedParameters.LinkedParameters.FirstOrDefault((lp) => lp.Contains(toAdd))) != null)
                    {
                        change.OriginalIndex = change.OriginalContainedIn.NoCommandRemove(toAdd);
                    }
                    return NoCommandAdd(toAdd, (change.Index = _ParameterModels.Count), ref e);
                },
                // undo
                (ref string e) =>
                {
                    NoCommandRemove(toAdd);
                    if(change.OriginalContainedIn != null)
                    {
                        return change.OriginalContainedIn.NoCommandAdd(toAdd, change.OriginalIndex, ref e);
                    }
                    else
                    {
                        // if it isn't part of another linked parameter just add the value back
                        return toAdd.SetValue(originalValue, ref e);
                    }
                },
                // redo
                (ref string e) =>
                {
                    if(change.OriginalContainedIn != null)
                    {
                        change.OriginalContainedIn.NoCommandRemove(toAdd);
                    }
                    return NoCommandAdd(toAdd, change.Index, ref e);
                }
                ), ref error);
        }

        private bool NoCommandAdd(ParameterModel toAdd, int index, ref string error)
        {
            lock (_ParameterModelsLock)
            {
                _ParameterModels.Insert(index, toAdd);
                // Try to add the linked parameter
                if(!RealLinkedParameter.Add(toAdd.RealParameter, ref error))
                {
                    //if the add failed return that fact
                    _ParameterModels.RemoveAt(index);
                    return false;
                }
                toAdd.UpdateValueFromReal();
                toAdd.SignalIsLinkedChanged();
                return true;
            }
        }

        private int NoCommandRemove(ParameterModel toRemove)
        {
            lock (_ParameterModelsLock)
            {
                var index = _ParameterModels.IndexOf(toRemove);
                string error = null;
                _ParameterModels.RemoveAt(index);
                RealLinkedParameter.Remove(toRemove.RealParameter, ref error);
                toRemove.UpdateValueFromReal();
                toRemove.SignalIsLinkedChanged();
                return index;
            }
        }

        /// <summary>
        /// Removes a parameter from this linked parameter
        /// </summary>
        /// <param name="toRemove">The parameter to remove</param>
        /// <param name="error">An error message if this is not possible</param>
        /// <returns>True if the parameter was removed, false if it was not.</returns>
        public bool RemoveParameter(ParameterModel toRemove, ref string error)
        {
            LinkedParameterChange change = new LinkedParameterChange();
            return _Session.RunCommand(XTMFCommand.CreateCommand(
                "Remove Parameter from Linked Parameter",
                // do
                (ref string e) =>
                {
                    // we need this outer lock to make sure that it doesn't change while we are checking to make sure that it is contained
                    lock (_ParameterModelsLock)
                    {
                        if(!_ParameterModels.Contains(toRemove))
                        {
                            e = "The parameter does not exist inside of the linked parameter!";
                            return false;
                        }
                        change.Index = NoCommandRemove(toRemove);
                    }
                    return true;
                },
                // undo
                (ref string e) =>
                {
                    return NoCommandAdd(toRemove, change.Index, ref e);
                },
                // redo
                (ref string e) =>
                {
                    NoCommandRemove(toRemove);
                    return true;
                }
                ), ref error);
        }

        internal bool AddParameterWithoutCommand(ParameterModel parameterModel, ref string error) => NoCommandAdd(parameterModel, _ParameterModels.Count, ref error);

        internal void RemoveParameterWithoutCommand(ParameterModel parameterToRemove) => NoCommandRemove(parameterToRemove);

        /// <summary>
        /// Check to see if this linked parameter has a reference to the given module.
        /// </summary>
        /// <param name="child">The module to test against</param>
        /// <returns>If the given module is referenced</returns>
        internal bool HasContainedModule(ModelSystemStructureModel child) => RealLinkedParameter.Parameters.Any(p => p.BelongsTo == child.RealModelSystemStructure);

        /// <summary>
        /// This will set the value of the linked parameter and all contained parameter to the given value
        /// </summary>
        /// <param name="newValue">The value to set it to.</param>
        /// <param name="error">Contains a message in case of an error.</param>
        /// <returns>True if successful, false in case of failure.</returns>
        public bool SetValue(string newValue, ref string error)
        {
            string oldValue = RealLinkedParameter.Value;
            return _Session.RunCommand(
                XTMFCommand.CreateCommand(
                    "Set Linked Parameter Value",
                    // do
                    (ref string e) =>
                    {
                        return SetWithoutCommand(newValue, ref e);
                    },
                    // undo
                    (ref string e) =>
                    {
                        return SetWithoutCommand(oldValue, ref e);
                    },
                    // redo
                    (ref string e) =>
                    {
                        return SetWithoutCommand(newValue, ref e);
                    })
                , ref error);
        }

        /// <summary>
        /// Internally set the value of the linked parameter without using a command in the session.
        /// </summary>
        /// <param name="newValue">The value to set it to</param>
        /// <param name="error">An error message in case of failure</param>
        /// <returns>True if successful, false if there is an error.</returns>
        internal bool SetWithoutCommand(string newValue, ref string error)
        {
            lock (_ParameterModelsLock)
            {
                foreach(var parameter in _ParameterModels)
                {
                    if(!ArbitraryParameterParser.Check(parameter.RealParameter.Type, newValue, ref error))
                    {
                        return false;
                    }
                }
                if(!RealLinkedParameter.SetValue(newValue, ref error))
                {
                    return false;
                }
                foreach(var parameter in _ParameterModels)
                {
                    parameter.UpdateValueFromReal();
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the value of the parameter
        /// </summary>
        /// <returns></returns>
        public string GetValue() => RealLinkedParameter.Value;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}