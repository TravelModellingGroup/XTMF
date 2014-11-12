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
using System.ComponentModel;
using System.Linq;
using System.Text;
using XTMF.Editing;

namespace XTMF
{
    /// <summary>
    /// The linked parameter model provides an interface into the logic of a linked parameter without directly changing them
    /// </summary>
    public class LinkedParameterModel
    {
        /// <summary>
        /// The parameters that are attached to this linked parameter
        /// </summary>
        private List<ParameterModel> ParameterModels;

        private ModelSystemEditingSession Session;
        private ModelSystemModel ModelSystem;
        public LinkedParameterModel(ILinkedParameter linkedParameter, ModelSystemEditingSession session, ModelSystemModel modelSystem)
        {
            Session = session;
            ModelSystem = modelSystem;
            ParameterModels = CreateInitialParameterModels(linkedParameter, ModelSystem);
            RealLinkedParameter = linkedParameter as LinkedParameter;
        }

        private static List<ParameterModel> CreateInitialParameterModels(ILinkedParameter linkedParameter, ModelSystemModel modelSystem)
        {
            var ret = new List<ParameterModel>();
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

        private object ParameterModelsLock = new object();


        /// <summary>
        /// The linked parameters being represented
        /// </summary>
        internal LinkedParameter RealLinkedParameter { get; set; }


        public List<ParameterModel> GetParameters()
        {
            lock (this.ParameterModelsLock)
            {
                var models = this.ParameterModels;
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
            lock (this.ParameterModelsLock)
            {
                return this.ParameterModels.Contains(toCheck);
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
            return Session.RunCommand(XTMFCommand.CreateCommand(
                // do
                (ref string e) =>
                {
                    if(this.ParameterModels.Contains(toAdd))
                    {
                        e = "The parameter was already contained in the linked parameter!";
                        return false;
                    }
                    // remove from the linked parameter it was already in
                    if((change.OriginalContainedIn = ModelSystem.LinkedParameters.LinkedParameters.FirstOrDefault((lp) => lp.Contains(toAdd))) != null)
                    {
                        change.OriginalIndex = change.OriginalContainedIn.NoCommandRemove(toAdd);
                    }
                    this.NoCommandAdd(toAdd, (change.Index = this.ParameterModels.Count));
                    return true;
                },
                // undo
                (ref string e) =>
                {
                    if(change.OriginalContainedIn != null)
                    {
                        change.OriginalContainedIn.NoCommandAdd(toAdd, change.OriginalIndex);
                    }
                    this.NoCommandRemove(toAdd);
                    return true;
                },
                // redo
                (ref string e) =>
                {
                    if(change.OriginalContainedIn != null)
                    {
                        change.OriginalContainedIn.NoCommandRemove(toAdd);
                    }
                    this.NoCommandAdd(toAdd, change.Index);
                    return true;
                }
                ), ref error);
        }

        private void NoCommandAdd(ParameterModel toAdd, int index)
        {
            lock (this.ParameterModelsLock)
            {
                this.ParameterModels.Insert(index, toAdd);
                this.RealLinkedParameter.Add(toAdd.RealParameter, ref string error = null);
            }
        }

        private int NoCommandRemove(ParameterModel toRemove)
        {
            lock (this.ParameterModelsLock)
            {
                var index = this.ParameterModels.IndexOf(toRemove);
                this.ParameterModels.RemoveAt(index);
                this.RealLinkedParameter.Remove(toRemove.RealParameter, ref string error = null);
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
            return Session.RunCommand(XTMFCommand.CreateCommand(
                // do
                (ref string e) =>
                {
                    // we need this outer lock to make sure that it doesn't change while we are checking to make sure that it is contained
                    lock (this.ParameterModelsLock)
                    {
                        if(!this.ParameterModels.Contains(toRemove))
                        {
                            e = "The parameter does not exist inside of the linked parameter!";
                            return false;
                        }
                        change.Index = this.NoCommandRemove(toRemove);
                    }
                    return true;
                },
                // undo
                (ref string e) =>
                {
                    this.NoCommandAdd(toRemove, change.Index);
                    return true;
                },
                // redo
                (ref string e) =>
                {
                    this.NoCommandRemove(toRemove);
                    return true;
                }
                ), ref error);
        }

        /// <summary>
        /// This will set the value of the linked parameter and all contained parameter to the given value
        /// </summary>
        /// <param name="newValue">The value to set it to.</param>
        /// <param name="error">Contains a message in case of an error.</param>
        /// <returns>True if successful, false in case of failure.</returns>
        public bool SetValue(string newValue, ref string error)
        {
            string oldValue = this.RealLinkedParameter.Value;
            return Session.RunCommand(
                XTMFCommand.CreateCommand(
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
            lock (this.ParameterModelsLock)
            {
                foreach(var parameter in this.ParameterModels)
                {
                    if(!ArbitraryParameterParser.Check(parameter.RealParameter.Type, newValue, ref error))
                    {
                        return false;
                    }
                }
                if(!this.RealLinkedParameter.SetValue(newValue, ref error))
                {
                    return false;
                }
                foreach(var parameter in this.ParameterModels)
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
        public string GetValue()
        {
            return this.RealLinkedParameter.Value;
        }
    }
}