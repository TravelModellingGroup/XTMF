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
using System.IO;

namespace XTMF.Networking;

/// <summary>
/// Include this in your Client ModelSystemTemplate in order to unlock the XTMF Networking Sub-System.
///
/// The containing class will need to leave this as a public field/property for it to be setup properly.
/// </summary>
public interface IClient
{
    /// <summary>
    /// The Id of this client
    /// </summary>
    string UniqueID { get; }

    /// <summary>
    /// Notify the host that we have completed our run
    /// </summary>
    /// <param name="status">Set this to non-zero if there is additional meaning for the host</param>
    /// <param name="error">This allows us to pass back a string in case of an error</param>
    void NotifyComplete(int status = 0, string? error = null);

    /// <summary>
    /// Notify the host of our current state of progress.
    /// The progress will be gathered by the XTMF framework automatically from the model system template
    /// </summary>
    void NotifyProgress();

    void RegisterCustomMessageHandler(int customMessageNumber, Action<object> handler);

    void RegisterCustomReceiver(int customMessageNumber, Func<Stream, object> converter);

    void RegisterCustomSender(int customMessageNumber, Action<object, Stream> converter);

    /// <summary>
    /// Get a resource from the host
    /// </summary>
    /// <param name="name">The name of the resource</param>
    /// <param name="t">The type of resource expected</param>
    /// <returns>The object, if found and of type.  Null otherwise.</returns>
    object RetriveResource(string name, Type t);

    void SendCustomMessage(object data, int customMessageNumber);

    /// <summary>
    /// Set the resource on the host
    /// </summary>
    /// <param name="name">With the given name</param>
    /// <param name="o">The object to send</param>
    /// <returns>True if successful</returns>
    bool SetResource(string name, object o);
}