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
    /// The LinkedParametersModel provides a clean interface into the operations of LinkedParameters for editing.
    /// </summary>
    public class LinkedParametersModel
    {
        private ModelSystemEditingSession Session;
        private ModelSystemModel ModelSystem;
        private List<ILinkedParameter> RealLinkedParameters;
        public LinkedParametersModel(ModelSystemEditingSession session, ModelSystemModel modelSystem, List<ILinkedParameter> realLinkedParameters)
        {
            Session = session;
            ModelSystem = modelSystem;
            RealLinkedParameters = realLinkedParameters;
            LinkedParameters = CreateLinkedParameters(RealLinkedParameters, Session, ModelSystem);
        }
        internal BindingListWithRemoving<LinkedParameterModel> LinkedParameters { get; private set; }

        /// <summary>
        /// Stores the data
        /// </summary>
        private class LinkedParameterChange
        {
            internal LinkedParameterModel Model;
            internal int Index;
        }

        /// <summary>
        /// Create a new Linked Parameter with the given name
        /// </summary>
        /// <param name="name">The name of the linked parameter</param>
        /// <param name="error">If an error occurs this will contain a message to describe it.</param>
        /// <returns>True if it executed successfully</returns>
        public bool NewLinkedParameter(string name, ref string error)
        {
            var lp = new LinkedParameterChange();
            return this.Session.RunCommand(
                XTMFCommand.CreateCommand((ref string e) =>
                {
                    LinkedParameter linkedParameter = new LinkedParameter(name);
                    LinkedParameterModel newModel = new LinkedParameterModel(linkedParameter, Session, ModelSystem);
                    RealLinkedParameters.Add(linkedParameter);
                    LinkedParameters.Add(newModel);
                    lp.Model = newModel;
                    lp.Index = this.LinkedParameters.Count - 1;
                    return true;
                },
                (ref string e) =>
                {
                    LinkedParameters.RemoveAt(lp.Index);
                    RealLinkedParameters.RemoveAt(lp.Index);
                    return true;
                },
                (ref string e) =>
                {
                    LinkedParameters.Insert(lp.Index, lp.Model);
                    RealLinkedParameters.Insert(lp.Index, lp.Model.RealLinkedParameter);
                    return true;
                }),
                ref error
                );
        }

        internal LinkedParameterModel GetContained(ParameterModel parameterModel)
        {
            return this.LinkedParameters.FirstOrDefault((model) => model.Contains(parameterModel));
        }


        /// <summary>
        /// Remove a linked parameter from the linked parameters
        /// </summary>
        /// <param name="linkedParameter">The linked parameter to remove</param>
        /// <param name="error">If an error occurs this will contain a message to describe it.</param>
        /// <returns>If the command was successful or not.</returns>
        public bool RemoveLinkedParameter(LinkedParameterModel linkedParameter, ref string error)
        {
            var lp = new LinkedParameterChange();
            return this.Session.RunCommand(
                XTMFCommand.CreateCommand((ref string e) =>
                {
                    if((lp.Index = this.LinkedParameters.IndexOf(linkedParameter)) < 0)
                    {
                        e = "The linked parameter was not found!";
                        return false;
                    }
                    lp.Model = this.LinkedParameters[lp.Index];
                    LinkedParameters.RemoveAt(lp.Index);
                    RealLinkedParameters.RemoveAt(lp.Index);
                    return true;
                },
                (ref string e) =>
                {
                    LinkedParameters.Insert(lp.Index, lp.Model);
                    RealLinkedParameters.Insert(lp.Index, lp.Model.RealLinkedParameter);
                    return true;
                },
                (ref string e) =>
                {
                    LinkedParameters.RemoveAt(lp.Index);
                    RealLinkedParameters.RemoveAt(lp.Index);
                    return true;
                }),
                ref error
                );
        }

        private static BindingListWithRemoving<LinkedParameterModel> CreateLinkedParameters(List<ILinkedParameter> linkedParameters, ModelSystemEditingSession session, ModelSystemModel ModelSystem)
        {
            var ret = new BindingListWithRemoving<LinkedParameterModel>();
            if(linkedParameters == null) return ret;
            for(int i = 0; i < linkedParameters.Count; i++)
            {
                ret.Add(new LinkedParameterModel(linkedParameters[i], session, ModelSystem));
            }
            return ret;
        }

        /// <summary>
        /// Retrieve the linked parameters as they currently stand
        /// </summary>
        /// <returns>A list of the linked parameters</returns>
        public List<LinkedParameterModel> GetLinkedParameters()
        {
            return this.LinkedParameters.ToList();
        }

        /// <summary>
        /// The number of linked parameters in the mode.
        /// </summary>
        /// <returns>Number of Linked parameters</returns>
        public int Count { get { return this.LinkedParameters.Count; } }
    }
}