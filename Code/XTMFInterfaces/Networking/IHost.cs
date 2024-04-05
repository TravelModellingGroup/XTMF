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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace XTMF.Networking;

/// <summary>
/// Include this in your Host ModelSystemTemplate in order to unlock the XTMF Networking Sub-System.
///
/// The containing class will need to leave this as a public field/property for it to be setup properly.
/// </summary>
public interface IHost
{
    /// <summary>
    /// Notifies that all of the runs have been completed
    /// </summary>
    event Action AllModelSystemRunsComplete;

    /// <summary>
    /// This event will notify you when a client has disconnected from the
    /// XTMF host
    /// </summary>
    event Action<IRemoteXTMF> ClientDisconnected;

    /// <summary>
    /// This event will notify you when a client has completed their run,
    /// their exit status, and any error strings
    /// </summary>
    event Action<IRemoteXTMF, int, string> ClientRunComplete;

    /// <summary>
    /// This event will notify you when a new client has connected
    /// to the XTMF Host
    /// </summary>
    event Action<IRemoteXTMF> NewClientConnected;

    /// <summary>
    /// The event will notify you when a client's progress has been updated
    /// </summary>
    event Action<IRemoteXTMF, float> ProgressUpdated;

    IList<IRemoteXTMF> ConnectedClients { get; }

    /// <summary>
    /// Gives the number of model systems that are still executing
    /// </summary>
    int CurrentlyExecutingModelSystems { get; }

    /// <summary>
    /// Checks to see if the current host has been shutdown
    /// </summary>
    bool IsShutdown { get; }

    /// <summary>
    /// The resources for the clients stored on the host
    /// </summary>
    ConcurrentDictionary<string, object> Resources { get; }

    /// <summary>
    /// Load a model system's structure from the model system repository
    /// </summary>
    /// <param name="name">The name of the model system to use</param>
    /// <param name="error">The error generated while trying to load the model system</param>
    /// <returns>null if that model system does not exist, or if it can not be produced and validated.</returns>
    IModelSystemStructure CreateModelSystem(string name, ref string error);

    /// <summary>
    /// Manually create a new ModelSystemStructure Node
    /// </summary>
    /// <param name="parent">The parent Type for this node</param>
    /// <param name="nodeType">The type this node represents</param>
    /// <param name="collection">If this node is a collection or not</param>
    /// <returns>The generated node</returns>
    /// <remarks>
    /// Please avoid doing this if possible since it will lead to hard coding.
    /// Instead please use the CreateModelSystem(string name) function.
    /// </remarks>
    IModelSystemStructure CreateModelSystemStructure(Type parent, Type nodeType, bool collection);

    /// <summary>
    /// Executes the given model system structure with the
    /// </summary>
    /// <param name="structure">The structure to execute</param>
    void ExecuteModelSystemAsync(IModelSystemStructure structure);

    /// <summary>
    /// Executes the given model system structure with the
    /// </summary>
    /// <param name="structure">A collection of structures to execute</param>
    void ExecuteModelSystemAsync(ICollection<IModelSystemStructure> structure);

    void RegisterCustomMessageHandler(int customMessageNumber, Action<object, IRemoteXTMF> handler);

    void RegisterCustomReceiver(int customMessageNumber, Func<Stream, IRemoteXTMF, object> converter);

    void RegisterCustomSender(int customMessageNumber, Action<object, IRemoteXTMF, Stream> converter);

    /// <summary>
    /// Shutdown the IHost
    /// </summary>
    void Shutdown();
}