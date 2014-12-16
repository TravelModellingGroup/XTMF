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
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using XTMF;
using XTMF.Networking;

namespace TMG.Distributed.Modules
{
    [ModuleInformation(
        Description = "This module is designed to handle the coordination of remote tasks, tracking their lifetimes and ensuring complete execution."
        )]
    public class HostDistributionManager : IHostDistributionManager, IDisposable
    {
        [SubModelInformation(Required = true, Description = "The client side model system to run.")]
        public IClientDistributionManager Client { get; set; }

        [SubModelInformation(Required = true, Description = "The model system to execute on the host.")]
        public IModelSystemTemplate MainModelSystem;

        private IModelSystemStructure ClientStructure;

        [RunParameter("Distribution Data Channel", 0, "The networking channel to use for communicating with clients.")]
        public int DistributionDataChannel;

        private volatile bool Exit = false;

        /// <summary>
        /// The link into XTMF Networking
        /// </summary>
        public IHost Host;

        [RunParameter("Input Base Directory", "../../Input", "The input directory for the model system.")]
        public string InputBaseDirectory { get; set; }

        public string Name { get; set; }

        public string OutputBaseDirectory { get; set; }

        public float Progress
        {
            get
            {
                return MainModelSystem.Progress;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return MainModelSystem.ProgressColour;
            }
        }

        public void AddTask(string taskName)
        {
            lock (Host)
            {
                PendingTasks.Add(
                    new ExecutingTask()
                {
                    Client = null,
                    Complete = false,
                    TaskName = taskName
                });
            }
            UpdateTaskAssignments();
        }

        public bool ExitRequest()
        {
            if(MainModelSystem.ExitRequest())
            {
                Exit = true;
                return true;
            }
            return false;
        }

        private IConfiguration Config;

        public HostDistributionManager(IConfiguration configuration)
        {
            Config = configuration;
        }

        private bool FindUs(IModelSystemStructure mst, ref IModelSystemStructure modelSystemStructure)
        {
            if(mst.Module == this)
            {
                modelSystemStructure = mst;
                return true;
            }
            if(mst.Children != null)
            {
                foreach(var child in mst.Children)
                {
                    if(FindUs(child, ref modelSystemStructure))
                    {
                        return true;
                    }
                }
            }
            // Then we didn't find it in this tree
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            IModelSystemStructure us = null;
            foreach(var mst in Config.ProjectRepository.ActiveProject.ModelSystemStructure)
            {
                if(FindUs(mst, ref us))
                {
                    foreach(var child in us.Children)
                    {
                        if(child.ParentFieldName == "Client")
                        {
                            ClientStructure = child;
                            break;
                        }
                    }
                    break;
                }
            }
            if(ClientStructure == null)
            {
                error = "In '" + Name + "' we were unable to find our client model system!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            try
            {
                SetupNetworkInterface();
                MainModelSystem.Start();
            }
            finally
            {
                Host.Shutdown();
            }
        }

        private void SetupNetworkInterface()
        {
            Host.ClientDisconnected += Host_ClientDisconnected;
            Host.NewClientConnected += Host_NewClientConnected;
            Host.RegisterCustomReceiver(DistributionDataChannel, (stream, client) =>
            {
                BinaryReader reader = new BinaryReader(stream);
                lock (Host)
                {
                    switch((CommunicationProtocol)reader.ReadInt32())
                    {
                        case CommunicationProtocol.ClientActivated:
                            Clients.Add(client);
                            AvailableClients.Add(client);
                            break;
                        case CommunicationProtocol.TaskComplete:
                            {
                                AvailableClients.Add(client);
                                var taskNumber = reader.ReadUInt64();
                                ExecutingTasks.RemoveAll((task) => task.TaskNumber == taskNumber && task.Client == client);
                            }
                            break;
                    }
                }
                UpdateTaskAssignments();
                // we don't actually bother storing an object
                reader = null;
                return null;
            });
            Host.RegisterCustomSender(DistributionDataChannel, (task, client, stream) =>
            {
                BinaryWriter writer = new BinaryWriter(stream);
                var t = task as ExecutingTask;
                writer.Write((Int32)CommunicationProtocol.RunTask);
                writer.Write(t.TaskNumber);
                writer.Write(t.TaskName);
                writer.Flush();
                writer = null;
            });
        }

        private void UpdateTaskAssignments()
        {
            lock (Host)
            {
                for(int i = 0; i < PendingTasks.Count; i++)
                {
                    if(AvailableClients.Count > 0)
                    {
                        var task = PendingTasks[0];
                        task.Client = AvailableClients[0];
                        task.TaskNumber = GetTaskNumber();
                        ExecutingTasks.Add(task);
                        // clean up
                        AvailableClients.RemoveAt(0);
                        PendingTasks.RemoveAt(0);
                        // fire the message to start processing
                        task.Client.SendCustomMessage(task, DistributionDataChannel);
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        private long TaskNumberHolder = 0;
        private ulong GetTaskNumber()
        {
            return (ulong)Interlocked.Increment(ref TaskNumberHolder);
        }

        private class ExecutingTask
        {
            internal ulong TaskNumber;
            internal string TaskName;
            internal IRemoteXTMF Client;
            internal volatile bool Complete;
        }

        /// <summary>
        /// All of the tasks that have yet to be scheduled
        /// </summary>
        List<ExecutingTask> PendingTasks = new List<ExecutingTask>();

        /// <summary>
        /// All of the tasks that are currently executing
        /// </summary>
        List<ExecutingTask> ExecutingTasks = new List<ExecutingTask>();

        /// <summary>
        /// The list of all clients
        /// </summary>
        List<IRemoteXTMF> Clients = new List<IRemoteXTMF>();

        /// <summary>
        /// The clients that are not currently executing
        /// </summary>
        List<IRemoteXTMF> AvailableClients = new List<IRemoteXTMF>();

        private void Host_NewClientConnected(IRemoteXTMF obj)
        {
            // fire off the model system to the client
            obj.SendModelSystem(ClientStructure);
        }

        private void Host_ClientDisconnected(IRemoteXTMF disconnectingClient)
        {
            lock (Host)
            {
                var unfinishedTasks = ExecutingTasks.Where(task => task.Client == disconnectingClient && task.Complete == false);
                AvailableClients.Remove(disconnectingClient);
                Clients.Remove(disconnectingClient);
                foreach(var unfinishedTask in unfinishedTasks)
                {
                    unfinishedTask.Client = null;
                    unfinishedTask.Complete = false;
                    unfinishedTask.TaskNumber = 0;
                    PendingTasks.Add(unfinishedTask);
                }
            }
            UpdateTaskAssignments();
        }

        public void WaitAll()
        {
            while(true)
            {
                lock (Host)
                {
                    if(ExecutingTasks.Count == 0 && PendingTasks.Count == 0)
                    {
                        return;
                    }
                }
                Thread.Sleep(10);
            }
        }

        public bool WaitAll(int timeoutMilliseconds)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while(true)
            {
                lock (Host)
                {
                    if(ExecutingTasks.Count == 0 && PendingTasks.Count == 0)
                    {
                        return true;
                    }
                }
                if(watch.ElapsedMilliseconds >= timeoutMilliseconds)
                {
                    return false;
                }
                Thread.Sleep(10);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool managed)
        {
            if(managed)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~HostDistributionManager()
        {
            Dispose(false);
        }

        public override string ToString()
        {
            return MainModelSystem.ToString();
        }
    }
}
