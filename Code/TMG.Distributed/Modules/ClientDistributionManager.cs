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
namespace TMG.Distributed.Modules;

public class ClientDistributionManager : IClientDistributionManager
{
    [RunParameter("Distribution Data Channel", 0, "The networking channel to use for communicating with clients.")]
    public int DistributionDataChannel;

    public IClient Client;

    [RunParameter("Input Directory", "../../Input", "The input directory for this model system.")]
    public string InputBaseDirectory { get; set; }

    public string Name { get; set; }

    public string OutputBaseDirectory { get; set; }

    [SubModelInformation(Description = "Initialize the client", Required = false)]
    public IModelSystemTemplate Initialization;

    public float Progress
    {
        get
        {
            return 0f;
        }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get
        {
            return null;
        }
    }


    public sealed class Task : IModule
    {
        [SubModelInformation(Required = true, Description = "The definition of the task")]
        public IModelSystemTemplate TaskModelSystem;

        [RunParameter("Task Name", "Unique Name", "The unique name for this task.")]
        public string TaskName;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    public List<Task> Tasks = [];

    public List<IResource> Resources { get; set; }

    public bool ExitRequest()
    {
        Exit = true;
        return true;
    }

    public volatile bool Exit;

    public bool RuntimeValidation(ref string error)
    {
        // make sure we do not have any duplicate task names
        var duplicates = Tasks.Where(task => Tasks.Any(t => t != task && t.TaskName == task.TaskName)).ToList();
        if(duplicates.Count > 0)
        {
            error = "In '" + Name + "' there are multiple tasks with the name '" + duplicates[0].TaskName + "'!";
            return false;
        }
        return true;
    }

    public void Start()
    {
        InitializeNetworking();
        if(Initialization != null)
        {
            Initialization.Start();
        }
        SignalReady();
        while(!Exit)
        {
            Thread.Sleep(10);
        }
    }

    private class Request
    {
        internal ulong TaskNumber;
        internal string TaskName;
        internal bool Success;
        internal string ErrorMessage;
    }

    private void InitializeNetworking()
    {
        Client.RegisterCustomReceiver(DistributionDataChannel, (stream) =>
        {
            BinaryReader reader = new(stream);
            switch((CommunicationProtocol)reader.ReadInt32())
            {
                case CommunicationProtocol.RunTask:
                    {
                        var request = new Request()
                        {
                            TaskNumber = reader.ReadUInt64(),
                            TaskName = reader.ReadString(),
                            Success = false
                        };
                        System.Threading.Tasks.Task.Factory.StartNew(() =>
                        {
                            var taskToRun = Tasks.FirstOrDefault(t => t.TaskName == request.TaskName);
                            try
                            {
                                if(taskToRun != null)
                                {
                                    taskToRun.TaskModelSystem.Start();
                                }
                                request.Success = true;
                            }
                            catch (Exception e)
                            {
                                request.ErrorMessage = e.Message + "\r\n" + e.StackTrace;
                            }
                            finally
                            {
                                // let the host know we finished
                                Client.SendCustomMessage(request, DistributionDataChannel);
                            }
                        }, System.Threading.Tasks.TaskCreationOptions.LongRunning);
                    }
                    break;
            }
            return null;
        });
        Client.RegisterCustomSender(DistributionDataChannel, (data, stream) =>
        {
            BinaryWriter writer = new(stream);
            var request = data as Request;
            var message = data as string;
            if(data == null)
            {
                writer.Write((Int32)CommunicationProtocol.ClientActivated);
            }
            else if(request != null)
            {
                if(request.Success)
                {
                    writer.Write((Int32)CommunicationProtocol.TaskComplete);
                    writer.Write(request.TaskNumber);
                }
                else
                {
                    writer.Write((Int32)CommunicationProtocol.TaskFailed);
                    writer.Write(request.ErrorMessage);
                }
            }
            else if(message != null)
            {
                writer.Write((Int32)CommunicationProtocol.SendTextMessageToHost);
                writer.Write(message);
            }
            writer.Flush();
        });
        
    }

    private void SignalReady()
    {
        // let the host know we are ready to operate
        Client.SendCustomMessage(null, DistributionDataChannel);
    }

    public bool HasTaskWithName(string taskName)
    {
        return Tasks.Any(t => t.TaskName == taskName);
    }

    public void SendTextMessageToHost(string message)
    {
        if(message != null)
        {
            Client.SendCustomMessage(message, DistributionDataChannel);
        }
    }
}
