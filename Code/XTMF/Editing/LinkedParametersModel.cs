﻿/*
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using XTMF.Editing;

namespace XTMF;

/// <summary>
/// The LinkedParametersModel provides a clean interface into the operations of LinkedParameters for editing.
/// </summary>
public class LinkedParametersModel : INotifyPropertyChanged
{
    private ModelSystemEditingSession _Session;

    private ModelSystemModel _ModelSystem;

    private List<ILinkedParameter> _RealLinkedParameters;

    public List<ILinkedParameter> GetRealLinkedParameters()
    {
        return [.. _RealLinkedParameters];
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public event CollectionChangeEventHandler LinkedParameterRemoved;

    public LinkedParametersModel(ModelSystemEditingSession session, ModelSystemModel modelSystem, List<ILinkedParameter> realLinkedParameters)
    {
        _Session = session;
        _ModelSystem = modelSystem;
        _RealLinkedParameters = realLinkedParameters;
        LinkedParameters = CreateLinkedParameters(_RealLinkedParameters, _Session, _ModelSystem);
    }

    internal ObservableCollection<LinkedParameterModel> LinkedParameters { get; private set; }

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
        return _Session.RunCommand(
            XTMFCommand.CreateCommand("New Linked Parameter",(ref string e) =>
            {
                LinkedParameter linkedParameter = new(name);
                LinkedParameterModel newModel = new(linkedParameter, _Session, _ModelSystem);
                _RealLinkedParameters.Add(linkedParameter);
                LinkedParameters.Add(newModel);
                lp.Model = newModel;
                lp.Index = LinkedParameters.Count - 1;
                return true;
            },
            (ref string e) =>
            {
                LinkedParameters.RemoveAt(lp.Index);
                _RealLinkedParameters.RemoveAt(lp.Index);
                InvokeRemoved(lp);
                return true;
            },
            (ref string e) =>
            {
                LinkedParameters.Insert(lp.Index, lp.Model);
                _RealLinkedParameters.Insert(lp.Index, lp.Model.RealLinkedParameter);
                return true;
            }),
            ref error
            );
    }

    public LinkedParameterModel GetContained(ParameterModel parameterModel)
    {
        return LinkedParameters.FirstOrDefault((model) => model.Contains(parameterModel));
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
        return _Session.RunCommand(
            XTMFCommand.CreateCommand(
                "Remove Linked Parameter",
                (ref string e) =>
            {
                if ((lp.Index = LinkedParameters.IndexOf(linkedParameter)) < 0)
                {
                    e = "The linked parameter was not found!";
                    return false;
                }
                lp.Model = LinkedParameters[lp.Index];
                LinkedParameters.RemoveAt(lp.Index);
                _RealLinkedParameters.RemoveAt(lp.Index);
                InvokeRemoved(lp);
                return true;
            },
            (ref string e) =>
            {
                LinkedParameters.Insert(lp.Index, lp.Model);
                _RealLinkedParameters.Insert(lp.Index, lp.Model.RealLinkedParameter);
                return true;
            },
            (ref string e) =>
            {
                LinkedParameters.RemoveAt(lp.Index);
                _RealLinkedParameters.RemoveAt(lp.Index);
                InvokeRemoved(lp);
                return true;
            }),
            ref error
            );
    }

    private void InvokeRemoved(LinkedParameterChange lp)
    {
        LinkedParameterRemoved?.Invoke(this, new CollectionChangeEventArgs(CollectionChangeAction.Remove, lp.Model));
    }

    private static ObservableCollection<LinkedParameterModel> CreateLinkedParameters(List<ILinkedParameter> linkedParameters, ModelSystemEditingSession session, ModelSystemModel ModelSystem)
    {
        var ret = new ObservableCollection<LinkedParameterModel>();
        if (linkedParameters == null)
        {
            return ret;
        }
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
    public ObservableCollection<LinkedParameterModel> GetLinkedParameters() => LinkedParameters;

    /// <summary>
    /// The number of linked parameters in the mode.
    /// </summary>
    /// <returns>Number of Linked parameters</returns>
    public int Count => LinkedParameters.Count;

    /// <summary>
    /// Add a new linked parameter without a command.  Only do this in another
    /// command where you will clean up afterwards.
    /// </summary>
    /// <param name="name">The name of the new linked parameter</param>
    /// <param name="value">The value to assign to it.</param>
    /// <returns>The newly created linked parameter</returns>
    internal LinkedParameterModel AddWithoutCommand(string name, string value)
    {
        LinkedParameter linkedParameter = new(name);
        LinkedParameterModel newModel = new(linkedParameter, _Session, _ModelSystem);
        AddWithoutCommand(newModel);
        string error = null;
        newModel.SetWithoutCommand(value, ref error);
        return newModel;
    }

    internal void AddWithoutCommand(LinkedParameterModel linkedParameter)
    {
        _RealLinkedParameters.Add(linkedParameter.RealLinkedParameter);
        LinkedParameters.Add(linkedParameter);
    }

    internal void RemoveWithoutCommand(LinkedParameterModel newLP)
    {
        var index = LinkedParameters.IndexOf(newLP);
        if(index >= 0)
        {
            LinkedParameters.RemoveAt(index);
            _RealLinkedParameters.RemoveAt(index);
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}