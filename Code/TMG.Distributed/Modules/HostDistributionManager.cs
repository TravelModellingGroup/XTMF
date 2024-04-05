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

        [RunParameter("Prefer Previous Client", true, "Assign tasks to clients that have previously executed the task if available.")]
        public bool PreferPreviousClient;

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
                BinaryReader reader = new(stream);
                lock (Host)
                {
                    switch((CommunicationProtocol)reader.ReadInt32())
                    {
                        case CommunicationProtocol.ClientActivated:
                            AvailableClients.Push(client);
                            break;
                        case CommunicationProtocol.TaskComplete:
                            {
                                AvailableClients.Push(client);
                                var taskNumber = reader.ReadUInt64();
                                ExecutingTasks.RemoveAll((task) => task.TaskNumber == taskNumber && task.Client == client);
                            }
                            break;
                        case CommunicationProtocol.TaskFailed:
                            {
                                Console.WriteLine("Client Error:\r\n" + reader.ReadString());
                                client.SendCancel("Previous Task Failed");
                            }
                            break;
                        case CommunicationProtocol.SendTextMessageToHost:
                            Console.WriteLine(reader.ReadString());
                            break;
                    }
                }
                UpdateTaskAssignments();
                // we don't actually bother storing an object
                return null;
            });
            Host.RegisterCustomSender(DistributionDataChannel, (task, client, stream) =>
            {
                BinaryWriter writer = new(stream);
                var t = task as ExecutingTask;
                if (t == null)
                {
                    throw new XTMFRuntimeException(this, $"In {Name} we were sent an object for task processing that was not a task!");
                }
                writer.Write((Int32)CommunicationProtocol.RunTask);
                writer.Write(t.TaskNumber);
                writer.Write(t.TaskName);
                writer.Flush();
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
                        if (PreferPreviousClient && PreviousTaskAssignments.TryGetValue(task.TaskName, out IRemoteXTMF previousHost)
                            && AvailableClients.Contains(previousHost))
                        {
                            RemoveClient(previousHost);
                            task.Client = previousHost;
                        }
                        else
                        {
                            task.Client = AvailableClients.Pop();
                        }
                        task.TaskNumber = GetTaskNumber();
                        PreviousTaskAssignments[task.TaskName] = task.Client;
                        ExecutingTasks.Add(task);
                        // clean up
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

        private long TaskNumberHolder;
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
        List<ExecutingTask> PendingTasks = [];

        /// <summary>
        /// All of the tasks that are currently executing
        /// </summary>
        List<ExecutingTask> ExecutingTasks = [];

        /// <summary>
        /// The clients that are not currently executing
        /// </summary>
        Stack<IRemoteXTMF> AvailableClients = new();

        Dictionary<string, IRemoteXTMF> PreviousTaskAssignments = [];

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
                RemoveClient(disconnectingClient);
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

        /// <summary>
        /// The calling method must have the Host lock!
        /// </summary>
        /// <param name="toRemove">The client to remove</param>
        private void RemoveClient(IRemoteXTMF toRemove)
        {
            AvailableClients = new Stack<IRemoteXTMF>(
                from client in AvailableClients
                where client != toRemove
                select client);
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

        protected virtual void Dispose(bool managed)
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
