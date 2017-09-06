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
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace XTMF.Networking
{
    internal class Client : IClient, IDisposable
    {
        private string Address;

        private IConfiguration Configuration;

        private volatile IModelSystemTemplate CurrentRunningModelSystem;

        private ConcurrentDictionary<int, List<Action<object>>> CustomHandlers = new ConcurrentDictionary<int, List<Action<object>>>();

        private ConcurrentDictionary<int, Func<Stream, object>> CustomReceivers = new ConcurrentDictionary<int, Func<Stream, object>>();

        private ConcurrentDictionary<int, Action<object, Stream>> CustomSenders = new ConcurrentDictionary<int, Action<object, Stream>>();

        private bool Exit = false;

        private MessageQueue<Message> Messages = new MessageQueue<Message>();

        private Thread ModelSystemThread;

        private int Port;

        private float Progress = 0;

        private LinkedList<DelayedResult> ResourceRequests = new LinkedList<DelayedResult>();

        public Client(string address, int port, IConfiguration configuration)
        {
            Configuration = configuration;
            Address = address;
            Port = port;
            new Thread(ClientMain).Start();
            Thread progressThread = new Thread(delegate ()
           {
               while (!Exit)
               {
                   Thread.Sleep(100);
                   try
                   {
                       Progress = CurrentRunningModelSystem.Progress;
                       NotifyProgress();
                   }
                   catch
                   {
                   }
                   Thread.MemoryBarrier();
               }
           });
            progressThread.Start();
        }

        public string UniqueID
        {
            get;
            internal set;
        }

        public void ClientMain()
        {
            TcpClient connection = null;
            bool done = false;
            try
            {
                connection = new TcpClient(Address, Port);
                var networkStream = connection.GetStream();
                new Thread(delegate ()
                   {
                       try
                       {
                           BinaryReader reader = new BinaryReader(networkStream);
                           BinaryFormatter inputFormat = new BinaryFormatter();
                            // we need some connection every 60 minutes, the host should be trying to request progress
                            networkStream.ReadTimeout = Timeout.Infinite;
                           while (!done || Exit)
                           {
                               var msg = new Message((MessageType)reader.ReadInt32());
                               switch (msg.Type)
                               {
                                   case MessageType.RequestProgress:
                                       {
                                           msg.Type = MessageType.PostProgess;
                                           Messages.Add(msg);
                                       }
                                       break;

                                   case MessageType.ReturningResource:
                                       {
                                           var name = reader.ReadString();
                                           bool exists = reader.ReadBoolean();
                                           object data = null;
                                           if (exists)
                                           {
                                               data = inputFormat.Deserialize(reader.BaseStream);
                                           }
                                           Result res = new Result() { Name = name, Data = data };
                                           msg.Data = res;
                                           Messages.Add(msg);
                                       }
                                       break;

                                   case MessageType.PostCancel:
                                       {
                                           Messages.Add(msg);
                                       }
                                       break;

                                   case MessageType.SendModelSystem:
                                       {
                                           var length = reader.ReadInt32();
                                           byte[] data = new byte[length];
                                           var soFar = 0;
                                           while (soFar < length)
                                           {
                                               soFar += reader.Read(data, soFar, length - soFar);
                                           }
                                           msg.Data = data;
                                           Messages.Add(msg);
                                       }
                                       break;

                                   case MessageType.Quit:
                                       {
                                           done = true;
                                           Exit = true;
                                           Console.WriteLine("Exiting.");
                                       }
                                       break;

                                   case MessageType.SendCustomMessage:
                                       {
                                            // Time to receive a new custom message
                                            var number = reader.ReadInt32();
                                           var length = reader.ReadInt32();
                                           var buff = new byte[length];
                                           MemoryStream buffer = new MemoryStream(buff);
                                           int soFar = 0;
                                           while (soFar < length)
                                           {
                                               soFar += reader.Read(buff, soFar, length - soFar);
                                           }
                                           Messages.Add(new Message(MessageType.ReceiveCustomMessage,
                                               new ReceiveCustomMessageMessage()
                                               {
                                                   CustomMessageNumber = number,
                                                   Stream = buffer
                                               }));
                                       }
                                       break;

                                   default:
                                       {
                                            // We don't know how to deal with this
                                            done = true;
                                           Exit = true;
                                           Console.WriteLine("Came across a message number " + msg.Type + " not sure what to do with it.  Exiting.");
                                       }
                                       break;
                               }
                               Thread.MemoryBarrier();
                           }
                       }
                       catch (IOException)
                       {
                           done = true;
                           Thread.MemoryBarrier();
                           Exit = true;
                           Console.WriteLine("Host has disconnected Client");
                           Environment.Exit(0);
                       }
                       catch (Exception e)
                       {
                           done = true;
                           Console.WriteLine("Client exception");
                           Console.WriteLine(e.Message);
                           Console.WriteLine(e.StackTrace);
                       }
                       finally
                       {
                           Console.WriteLine("Client reader has exited.");
                           connection.Close();
                           done = true;
                           Exit = true;
                           Thread.MemoryBarrier();
                       }
                   }).Start();
                BinaryWriter writer = new BinaryWriter(networkStream);
                BinaryFormatter outputFormat = new BinaryFormatter();
                networkStream.WriteTimeout = 20000;
                while (!done && !Exit)
                {
                    var message = Messages.GetMessageOrTimeout(200);
                    Thread.MemoryBarrier();
                    if (!done && message != null)
                    {
                        var exit = ProcessMessage(writer, outputFormat, message);
                        if (exit)
                        {
                            done = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                if (connection != null)
                {
                    connection.Close();
                }
                done = true;
            }
            done = true;
            Exit = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void NotifyComplete(int status = 0, string error = null)
        {
            Messages.Add(new Message(MessageType.PostComplete));
        }

        public void NotifyProgress()
        {
            Messages.Add(new Message(MessageType.PostProgess));
        }

        public void RegisterCustomMessageHandler(int customMessageNumber, Action<object> handler)
        {
            lock (this)
            {
                if (!CustomHandlers.TryGetValue(customMessageNumber, out List<Action<object>> port))
                {
                    port = new List<Action<object>>();
                    CustomHandlers[customMessageNumber] = port;
                }
                port.Add(handler);
            }
        }

        public void RegisterCustomReceiver(int customMessageNumber, Func<Stream, object> converter)
        {
            if (!CustomReceivers.TryAdd(customMessageNumber, converter))
            {
                throw new XTMFRuntimeException(null, "The Custom Receiver port " + customMessageNumber + " was attempted to be registered twice!");
            }
        }

        public void RegisterCustomSender(int customMessageNumber, Action<object, Stream> converter)
        {
            if (!CustomSenders.TryAdd(customMessageNumber, converter))
            {
                throw new XTMFRuntimeException(null, "The Custom Sender port " + customMessageNumber + " was attempted to be registered twice!");
            }
        }

        public object RetriveResource(string name, Type t)
        {
            DelayedResult result = new DelayedResult() { Name = name };
            Messages.Add(new Message(MessageType.RequestResource, result));
            result.Lock.Wait();
            result.Lock.Dispose();
            return result.Data;
        }

        public void SendCustomMessage(object data, int customMessageNumber)
        {
            Messages.Add(new Message(MessageType.SendCustomMessage,
                new SendCustomMessageMessage() { CustomMessageNumber = customMessageNumber, Data = data }));
        }

        public bool SetResource(string name, object o)
        {
            Result data = new Result() { Name = name, Data = o };
            Messages.Add(new Message(MessageType.PostResource, data));
            return true;
        }

        protected virtual void Dispose(bool includeManaged)
        {
            if (Messages != null)
            {
                Messages.Dispose();
                Messages = null;
            }
        }

        private void ModelSystemStartup(object modelSystemStructure)
        {
            string error = null;
            var mss = modelSystemStructure as IModelSystemStructure;
            var project = Configuration.ProjectRepository.ActiveProject;
            if (project == null)
            {
                project = new Project("Remote", Configuration, true);
            }
            if (project.ModelSystemStructure.Count == 0)
            {
                project.ModelSystemStructure.Add(mss);
            }
            else
            {
                project.ModelSystemStructure[0] = mss;
            }
            ((ProjectRepository)Configuration.ProjectRepository).SetActiveProject(project);
            var modelSystem = project.CreateModelSystem(ref error, 0);
            var now = DateTime.Now;
            var runDirectory = Path.GetFullPath(Path.Combine(Configuration.ProjectDirectory,
                project.Name, String.Format("{0:##}.{1:##}.{2:##}-{3}", now.Hour, now.Minute, now.Second, Guid.NewGuid())));
            bool crashed = false;
            if (!Directory.Exists(runDirectory))
            {
                Directory.CreateDirectory(runDirectory);
            }
            Directory.SetCurrentDirectory(runDirectory);
            project.Save(Path.GetFullPath("RunParameters.xml"), ref error);
            try
            {
                if (RunTimeValidation(ref error, mss))
                {
                    CurrentRunningModelSystem = modelSystem;
                    modelSystem.Start();
                }
            }
            catch (Exception e)
            {
                crashed = true;
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            CleanUp(mss);
            CurrentRunningModelSystem = null;
            NotifyComplete();
            if (crashed)
            {
                Exit = true;
                Thread.MemoryBarrier();
            }
        }

        private void CleanUp(IModelSystemStructure mss)
        {
            if (mss.Module != null)
            {
                if (mss.Module is IDisposable disp)
                {
                    disp.Dispose();
                }
                mss.Module = null;
            }
            if (mss.Children != null)
            {
                foreach (var child in mss.Children)
                {
                    CleanUp(child);
                }
            }
        }

        private bool ProcessMessage(BinaryWriter writer, BinaryFormatter outputFormat, Message message)
        {
            switch (message.Type)
            {
                case MessageType.PostComplete:
                    {
                        writer.Write((Int32)MessageType.PostComplete);
                    }
                    break;

                case MessageType.PostResource:
                    {
                        var data = message.Data as Result;
                        writer.Write(data.Name);
                        outputFormat.Serialize(writer.BaseStream, data.Data);
                    }
                    break;

                case MessageType.PostProgess:
                    {
                        writer.Write((Int32)MessageType.PostProgess);
                        writer.Write(Progress);
                        writer.Flush();
                    }
                    break;

                case MessageType.RequestResource:
                    {
                        var dr = message.Data as DelayedResult;
                        ResourceRequests.AddLast(dr);
                        writer.Write((Int32)MessageType.RequestResource);
                        writer.Write(dr.Name);
                        writer.Flush();
                    }
                    break;

                case MessageType.ReturningResource:
                    {
                        var result = message.Data as Result;
                        DelayedResult toRemove = null;
                        foreach (var delayed in ResourceRequests)
                        {
                            if (delayed.Name == result.Name)
                            {
                                toRemove = delayed;
                                delayed.Data = result.Data;
                                delayed.Lock.Release();
                                break;
                            }
                        }
                        if (toRemove != null)
                        {
                            ResourceRequests.Remove(toRemove);
                        }
                    }
                    break;

                case MessageType.PostCancel:
                    {
                        var ms = CurrentRunningModelSystem;
                        // if we don't have a model system then we are done
                        if (ms == null) break;
                        try
                        {
                            // try to cancel the model system
                            ms.ExitRequest();
                        }
                        catch
                        {
                        }
                    }
                    break;

                case MessageType.SendModelSystem:
                    {
                        try
                        {
                            var mssBuff = message.Data as byte[];
                            IModelSystemStructure mss = null;
                            using (MemoryStream memory = new MemoryStream())
                            {
                                memory.Write(mssBuff, 0, mssBuff.Length);
                                memory.Position = 0;
                                mss = ModelSystemStructure.Load(memory, Configuration);
                            }

                            if (ModelSystemThread != null && ModelSystemThread.IsAlive)
                            {
                                try
                                {
                                    ModelSystemThread.Abort();
                                }
                                catch
                                {
                                }
                            }
                            // now that the other thread is going to end
                            // we can now go and start generating ourselves
                            // in another run thread
                            (ModelSystemThread =
                                new Thread(ModelSystemStartup)).Start(mss);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                            return true;
                        }
                    }
                    break;

                case MessageType.SendCustomMessage:
                    {
                        var msg = message.Data as SendCustomMessageMessage;
                        int msgNumber = msg.CustomMessageNumber;
                        int length = 0;
                        var failed = false;
                        byte[] buffer = null;
                        Action<object, Stream> customConverter;
                        bool getConverter = false;
                        lock (this)
                        {
                            getConverter = CustomSenders.TryGetValue(msgNumber, out customConverter);
                        }
                        if (getConverter)
                        {
                            using (MemoryStream mem = new MemoryStream(0x100))
                            {
                                try
                                {
                                    customConverter(msg.Data, mem);
                                    mem.Position = 0;
                                    buffer = mem.ToArray();
                                    length = buffer.Length;
                                }
                                catch
                                {
                                    failed = true;
                                }
                            }
                        }
                        writer.Write((Int32)MessageType.SendCustomMessage);
                        writer.Write((Int32)msg.CustomMessageNumber);
                        writer.Write((Int32)length);
                        if (!failed)
                        {
                            writer.Write(buffer, 0, length);
                            buffer = null;
                        }
                    }
                    break;

                case MessageType.ReceiveCustomMessage:
                    {
                        var msg = message.Data as ReceiveCustomMessageMessage;
                        var customNumber = msg.CustomMessageNumber;
                        Func<Stream, object> customConverter;
                        bool getConverted = false;
                        lock (this)
                        {
                            getConverted = CustomReceivers.TryGetValue(customNumber, out customConverter);
                        }
                        if (getConverted)
                        {
                            using (var stream = msg.Stream)
                            {
                                try
                                {
                                    object output = customConverter(stream);
                                    if (CustomHandlers.TryGetValue(msg.CustomMessageNumber, out List<Action<object>> handlers))
                                    {
                                        foreach (var handler in handlers)
                                        {
                                            try
                                            {
                                                handler(output);
                                            }
                                            catch (Exception e)
                                            {
                                                Console.WriteLine(e.Message + "\r\n" + e.StackTrace);
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                    break;

                default:
                    {
                        // FAIL!
                        Console.WriteLine("Processing a message of type " + message.Type + " and we didn't know what to do with it.");
                        return true;
                    }
            }
            return false;
        }

        /// <summary>
        /// Validate the model system before starting to execute it
        /// </summary>
        /// <param name="error">If there is an error, the error message will be stored in here</param>
        /// <param name="currentPoint">The current point through the tree we are testing</param>
        /// <returns></returns>
        private bool RunTimeValidation(ref string error, IModelSystemStructure currentPoint)
        {
            try
            {
                // if there is a module at this point
                if (currentPoint.Module != null)
                {
                    if (!currentPoint.Module.RuntimeValidation(ref error))
                    {
                        return false;
                    }
                }
                // check to see if there are descendants that need to be checked
                if (currentPoint.Children != null)
                {
                    foreach (var module in currentPoint.Children)
                    {
                        if (!RunTimeValidation(ref error, module))
                        {
                            Console.WriteLine("Validation error in module " + module.Name + "\r\n" + error);
                            return false;
                        }
                    }
                }
                // if all of our children are alright, and we are also alright this part of the tree is ready to run
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}