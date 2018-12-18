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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

// Used for the windows firewall
using NetFwTypeLib;

namespace XTMF.Networking
{
    internal class Host : IHost, IDisposable
    {
        private const string CLSID_FIREWALL_MANAGER = "{304CE942-6E39-40D8-943A-B913C40C9CD4}";

        private const string PROGID_AUTHORIZED_APPLICATION = "HNetCfg.FwAuthorizedApplication";

        private MessageQueue<IRemoteXTMF> _AvailableClients = new MessageQueue<IRemoteXTMF>();

        private readonly IConfiguration _Configuration;

        private readonly ConcurrentDictionary<int, List<Action<object, IRemoteXTMF>>> _CustomHandlers = new ConcurrentDictionary<int, List<Action<object, IRemoteXTMF>>>();

        private readonly ConcurrentDictionary<int, Func<Stream, IRemoteXTMF, object>> _CustomReceivers = new ConcurrentDictionary<int, Func<Stream, IRemoteXTMF, object>>();

        private readonly ConcurrentDictionary<int, Action<object, IRemoteXTMF, Stream>> _CustomSenders = new ConcurrentDictionary<int, Action<object, IRemoteXTMF, Stream>>();

        private MessageQueue<IModelSystemStructure> _ExecutionTasks = new MessageQueue<IModelSystemStructure>();

        private volatile bool _Exit = false;

        private volatile bool _HostActive = false;

        private volatile bool _SetupComplete = false;

        private int _UniqueID = 0;

        public bool IsShutdown { get; private set; }

        public Host(IConfiguration configuration)
        {
            IsShutdown = false;
            _Configuration = configuration;
            ConnectedClients = new List<IRemoteXTMF>();
            Thread hostThread = new Thread(HostMain);
            Thread taskDistributionThread = new Thread(DistributeTasks)
            {
                IsBackground = true
            };
            hostThread.IsBackground = true;
            hostThread.Start();
            taskDistributionThread.Start();
            // Spin until the host has been setup
            while (!_SetupComplete) Thread.Sleep(0);
        }

        public event Action AllModelSystemRunsComplete;

        public event Action<IRemoteXTMF> ClientDisconnected;

        public event Action<IRemoteXTMF, int, string> ClientRunComplete;

        public event Action<IRemoteXTMF> NewClientConnected;

        public event Action<IRemoteXTMF, float> ProgressUpdated;

        public IList<IRemoteXTMF> ConnectedClients { get; private set; }

        public int CurrentlyExecutingModelSystems => ConnectedClients.Count - _AvailableClients.Count;

        public ConcurrentDictionary<string, object> Resources { get; set; }

        public void ClientExited()
        {
            lock (this)
            {
                RemoveAllEventHandels();
            }
        }

        public IModelSystemStructure CreateModelSystem(string name, ref string error)
        {
            foreach (var ms in _Configuration.ModelSystemRepository)
            {
                if (ms.Name == name)
                {
                    var clone = ms.ModelSystemStructure.Clone();
                    clone.Validate(ref error);
                    return clone;
                }
            }
            error = "We were unable to find a model system named \"" + name + "\".";
            return null;
        }

        public IModelSystemStructure CreateModelSystemStructure(Type parent, Type nodeType, bool collection)
        {
            throw new NotImplementedException();
        }

        public void Dispose() => Dispose(true);

        public void ExecuteModelSystemAsync(IModelSystemStructure structure)
        {
            _ExecutionTasks.Add(structure);
        }

        public void ExecuteModelSystemAsync(ICollection<IModelSystemStructure> structure)
        {
            foreach (var mss in structure)
            {
                _ExecutionTasks.Add(mss);
            }
        }

        public void RegisterCustomMessageHandler(int customMessageNumber, Action<object, IRemoteXTMF> handler)
        {
            lock (this)
            {
                if (!_CustomHandlers.TryGetValue(customMessageNumber, out List<Action<object, IRemoteXTMF>> port))
                {
                    port = new List<Action<object, IRemoteXTMF>>();
                    _CustomHandlers[customMessageNumber] = port;
                }
                port.Add(handler);
            }
        }

        public void RegisterCustomReceiver(int customMessageNumber, Func<Stream, IRemoteXTMF, object> converter)
        {
            if (!_CustomReceivers.TryAdd(customMessageNumber, converter))
            {
                throw new XTMFRuntimeException(null, "The Custom Receiver port " + customMessageNumber + " was attempted to be registered twice!");
            }
        }

        public void RegisterCustomSender(int customMessageNumber, Action<object, IRemoteXTMF, Stream> converter)
        {
            if (!_CustomSenders.TryAdd(customMessageNumber, converter))
            {
                throw new XTMFRuntimeException(null, "The Custom Sender port " + customMessageNumber + " was attempted to be registered twice!");
            }
        }

        public void Shutdown()
        {
            _Exit = true;
            while (_HostActive)
            {
                Thread.Sleep(0);
                Thread.MemoryBarrier();
            }
            IsShutdown = true;
        }

        protected virtual void Dispose(bool includeManaged)
        {
            if (_AvailableClients != null)
            {
                _AvailableClients.Dispose();
                _AvailableClients = null;
            }
            if (_ExecutionTasks != null)
            {
                _ExecutionTasks.Dispose();
                _ExecutionTasks = null;
            }
        }

        private static bool AuthorizeApplication(INetFwMgr manager, string title, string applicationPath, NET_FW_SCOPE_ scope, NET_FW_IP_VERSION_ ipVersion)
        {      // Create the type from prog id
            Type type = Type.GetTypeFromProgID(PROGID_AUTHORIZED_APPLICATION);
            INetFwAuthorizedApplication auth = Activator.CreateInstance(type) as INetFwAuthorizedApplication;
            auth.Name = title;
            auth.ProcessImageFileName = applicationPath;
            auth.Scope = scope;
            auth.IpVersion = ipVersion;
            auth.Enabled = true;
            try
            {
                manager.LocalPolicy.CurrentProfile.AuthorizedApplications.Add(auth);
                return true;
            }
            catch
            {
            }
            return false;
        }

        private static INetFwMgr GetFirewallManager()
        {
            var objectType = Type.GetTypeFromCLSID(new Guid(CLSID_FIREWALL_MANAGER));
            return objectType != null ? Activator.CreateInstance(objectType) as INetFwMgr : null;
        }

        private static void InitialzeWindows(string programName, string programPath)
        {
            // we are going to try to access through the Windows XP+ interface and give ourselves
            // access to host a port (might prompt the user)
            var firewallManager = GetFirewallManager();
            // If there firewall manager is not available just skip it
            if (firewallManager == null)
            {
                return;
            }
            bool isFirewallEnabled = firewallManager.LocalPolicy.CurrentProfile.FirewallEnabled;
            INetFwAuthorizedApplications authorizedApplications;
            authorizedApplications = firewallManager.LocalPolicy.CurrentProfile.AuthorizedApplications;
            bool found = false;
            foreach (INetFwAuthorizedApplication app in authorizedApplications)
            {
                if (app.Name == programName && app.ProcessImageFileName == programPath)
                {
                    // If we get here then we have found ourselves
                    if (app.Enabled != true)
                    {
                        // try to enable ourselves
                        app.Enabled = true;
                    }
                    found = true;
                }
            }
            if (!found)
            {
                // if we are not contained, then add ourselves.  It also doesn't matter if we succeed or not, we need to try anyways
                AuthorizeApplication(firewallManager, programName, programPath, NET_FW_SCOPE_.NET_FW_SCOPE_ALL, NET_FW_IP_VERSION_.NET_FW_IP_VERSION_ANY);
            }
        }

        private void ClientMain(object clientObject)
        {
            if (!(clientObject is TcpClient client)) return;
            bool done = false;
            RemoteXTMF ourRemoteClient = new RemoteXTMF();
            try
            {
                // Step 1) Accept the Client
                var clientStream = client.GetStream();
                GenerateUniqueName(ourRemoteClient);
                _AvailableClients.Add(ourRemoteClient);
                lock (this)
                {
                    try
                    {
                        ConnectedClients.Add(ourRemoteClient);
                        NewClientConnected?.Invoke(ourRemoteClient);
                    }
                    catch
                    {
                    }
                }
                // Start up the thread to process the messages coming from the remote xtmf
                new Thread(delegate ()
                   {
                       while (!done && !_Exit)
                       {
                           // cycle every 500ms ~ 1/2 second
                           try
                           {
                               clientStream.ReadTimeout = Timeout.Infinite;
                               BinaryReader reader = new BinaryReader(clientStream);
                               BinaryFormatter readingConverter = new BinaryFormatter();
                               while (!done && !_Exit)
                               {
                                   var messageType = (MessageType)reader.ReadInt32();
                                   var clientMessage = new Message(messageType);
                                   switch (messageType)
                                   {
                                       case MessageType.Quit:
                                           {
                                               done = true;
                                               return;
                                           }
                                       case MessageType.RequestResource:
                                           {
                                               var name = reader.ReadString();
                                               clientMessage.Data = name;
                                               ourRemoteClient.Messages.Add(clientMessage);
                                           }
                                           break;
                                       case MessageType.PostProgess:
                                           {
                                               var progress = reader.ReadSingle();
                                               clientMessage.Data = progress;
                                               ourRemoteClient.Messages.Add(clientMessage);
                                           }
                                           break;
                                       case MessageType.PostComplete:
                                           {
                                               ourRemoteClient.Messages.Add(clientMessage);
                                           }
                                           break;
                                       case MessageType.PostResource:
                                           {
                                               var data = readingConverter.Deserialize(reader.BaseStream);
                                               clientMessage.Data = data;
                                               ourRemoteClient.Messages.Add(clientMessage);
                                           }
                                           break;
                                       case MessageType.SendCustomMessage:
                                           {
                                               // Time to recieve a new custom message
                                               var number = reader.ReadInt32();
                                               var length = reader.ReadInt32();
                                               var buff = new byte[length];
                                               MemoryStream buffer = new MemoryStream(buff);
                                               int soFar = 0;
                                               while (soFar < length)
                                               {
                                                   soFar += reader.Read(buff, soFar, length - soFar);
                                               }
                                               ourRemoteClient.Messages.Add(new Message(MessageType.ReceiveCustomMessage,
                                                   new ReceiveCustomMessageMessage()
                                                   {
                                                       CustomMessageNumber = number,
                                                       Stream = buffer
                                                   }));
                                           }
                                           break;
                                       case MessageType.WriteToHostConsole:
                                           var str = reader.ReadString();
                                           Console.WriteLine($"{ourRemoteClient.UniqueID}\r\n{str}");
                                           break;
                                       default:
                                           {
                                               done = true;
                                               client.Close();
                                           }
                                           break;
                                   }
                               }
                           }
                           catch
                           {
                               // we will get here if the connection is closed
                               try
                               {
                                   if (client.Connected)
                                   {
                                       continue;
                                   }
                               }
                               catch (ObjectDisposedException)
                               {
                                   done = true;
                               }
                           }
                           done = true;
                       }
                       // don't close the reader/writer since this will also close the client stream
                   }).Start();
                BinaryWriter writer = new BinaryWriter(clientStream);
                BinaryFormatter converter = new BinaryFormatter();
                clientStream.WriteTimeout = 10000;
                while (!done && !_Exit)
                {
                    Message message = ourRemoteClient.Messages.GetMessageOrTimeout(200);
                    if (message == null)
                    {
                        message = new Message(MessageType.RequestProgress);
                    }
                    var nowDone = ProcessMessage(done, ourRemoteClient, writer, converter, message);
                    Thread.MemoryBarrier();
                    done = done | nowDone;
                }
                done = true;
            }
            catch
            {
            }
            finally
            {
                done = true;
                lock (this)
                {
                    try
                    {
                        client.Close();
                    }
                    catch
                    {
                    }
                    try
                    {
                        lock (ourRemoteClient)
                        {
                            ourRemoteClient.Connected = false;
                            ourRemoteClient.Messages.Dispose();
                            ourRemoteClient.Messages = null;
                        }
                        lock (this)
                        {
                            ConnectedClients.Remove(ourRemoteClient);
                            ClientDisconnected?.Invoke(ourRemoteClient);
                        }
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
        }

        private void CompletedTask(RemoteXTMF ourRemoteClient, int status, string error)
        {
            lock (this)
            {
                try
                {
                    ClientRunComplete?.Invoke(ourRemoteClient, status, error);
                }
                catch
                {
                }
            }
            ourRemoteClient.Progress = 0;
            _AvailableClients.Add(ourRemoteClient);
        }

        private void DistributeTasks()
        {
            while (!_Exit)
            {
                var client = _AvailableClients.GetMessageOrTimeout(200);
                try
                {
                    if (client != null)
                    {
                        var modelSystemStructure = _ExecutionTasks.GetMessageOrTimeout(200);
                        if (modelSystemStructure != null)
                        {
                            try
                            {
                                client.SendModelSystem(modelSystemStructure);
                            }
                            catch
                            {
                            }
                        }
                        else
                        {
                            _AvailableClients.Add(client);
                        }
                    }
                    Thread.MemoryBarrier();
                }
                catch
                {
                }
            }
        }

        private void GenerateUniqueName(RemoteXTMF ourRemoteClient)
        {
            ourRemoteClient.UniqueID = string.Format("Client:{0}", Interlocked.Increment(ref _UniqueID));
        }

        private void GetFirewallPermissions()
        {
            try
            {
                // Since we are in XTMF.dll we need to figure out what program is actually using us before we open up the firewall
                Assembly baseAssembly = Assembly.GetEntryAssembly();
                var codeBase = baseAssembly.CodeBase;
                var programName = Path.GetFileName(codeBase);
                string programPath = null;
                try
                {
                    programPath = Path.GetFullPath(codeBase);
                }
                catch (IOException)
                {
                    programPath = codeBase.Replace("file:///", String.Empty);
                }
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    InitialzeWindows(programName, programPath);
                }
            }
            catch (Exception)
            {
            }
        }

        private void HostMain()
        {
            try
            {
                // Request permission to use port 1447
                GetFirewallPermissions();
                var hostPort = 1447;
                if (_Configuration is Configuration config)
                {
                    hostPort = config.HostPort;
                }
                TcpListener listener = new TcpListener(IPAddress.Any, hostPort);
                listener.Start(20);
                _SetupComplete = true;
                _HostActive = true;
                while (!_Exit)
                {
                    try
                    {
                        listener.Server.Poll(1000000, SelectMode.SelectRead);
                        while (listener.Pending())
                        {
                            // Process the new connection
                            TcpClient client = listener.AcceptTcpClient();
                            Thread newClientThread = new Thread(ClientMain);
                            // fire and forget the client thread
                            newClientThread.Start(client);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    Thread.MemoryBarrier();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                // make sure no matter what, that this gets set
                _SetupComplete = true;
                _Exit = true;
                _HostActive = false;
            }
        }

        private bool ProcessMessage(bool done, RemoteXTMF ourRemoteClient, BinaryWriter writer, BinaryFormatter converter, Message message)
        {
            if (message != null)
            {
                //If we have a message to process, process it
                switch (message.Type)
                {
                    case MessageType.Quit:
                        {
                            done = true;
                        }
                        break;
                    case MessageType.RequestProgress:
                        {
                            writer.Write((Int32)MessageType.RequestProgress);
                            writer.Flush();
                        }
                        break;
                    case MessageType.RequestResource:
                        {
                            var name = message.Data as string;
                            writer.Write((Int32)MessageType.ReturningResource);
                            writer.Write(name);
                            if (Resources.TryGetValue(name, out object data))
                            {
                                writer.Write(true);
                                converter.Serialize(writer.BaseStream, data);
                            }
                            else
                            {
                                writer.Write(false);
                            }
                            writer.Flush();
                        }
                        break;
                    case MessageType.PostComplete:
                        {
                            CompletedTask(ourRemoteClient, 0, String.Empty);
                        }
                        break;
                    case MessageType.PostCancel:
                        {
                            writer.Write((Int32)MessageType.PostCancel);
                            writer.Flush();
                        }
                        break;
                    case MessageType.PostProgess:
                        {
                            var progress = (float)message.Data;
                            ourRemoteClient.Progress = progress;
                            // we need to lock here since other clients could also
                            // be trying to update the host with their progress
                            lock (this)
                            {
                                try
                                {
                                    ProgressUpdated?.Invoke(ourRemoteClient, progress);
                                }
                                catch
                                {
                                }
                            }
                        }
                        break;
                    case MessageType.PostResource:
                        {
                            var data = (ResourcePost)message.Data;
                            Resources[data.Name] = data.Data;
                        }
                        break;
                    case MessageType.SendModelSystem:
                        {
                            writer.Write((Int32)MessageType.SendModelSystem);
                            var mss = message.Data as IModelSystemStructure;
                            try
                            {
                                byte[] data = null;
                                using (MemoryStream memStream = new MemoryStream())
                                {
                                    mss.Save(memStream);
                                    memStream.Position = 0;
                                    data = memStream.ToArray();
                                }
                                writer.Write(data.Length);
                                writer.Write(data, 0, data.Length);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                            }
                            writer.Flush();
                        }
                        break;
                    case MessageType.SendCustomMessage:
                        {
                            var msg = message.Data as SendCustomMessageMessage;
                            int msgNumber = msg.CustomMessageNumber;
                            int length = 0;
                            var failed = false;
                            byte[] buffer = null;
                            if (_CustomSenders.TryGetValue(msgNumber, out Action<object, IRemoteXTMF, Stream> customConverter))
                            {
                                using (MemoryStream mem = new MemoryStream(0x100))
                                {
                                    try
                                    {
                                        customConverter(msg.Data, ourRemoteClient, mem);
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
                            writer.Write((int)MessageType.SendCustomMessage);
                            writer.Write((int)msg.CustomMessageNumber);
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
                            if (_CustomReceivers.TryGetValue(customNumber, out Func<Stream, IRemoteXTMF, object> customConverter))
                            {
                                using (var stream = msg.Stream)
                                {
                                    try
                                    {
                                        object output = customConverter(stream, ourRemoteClient);
                                        if (_CustomHandlers.TryGetValue(msg.CustomMessageNumber, out List<Action<object, IRemoteXTMF>> handlers))
                                        {
                                            foreach (var handler in handlers)
                                            {
                                                try
                                                {
                                                    handler(output, ourRemoteClient);
                                                }
                                                catch
                                                {
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
                            done = true;
                        }
                        break;
                }
            }
            return done;
        }

        private void RemoveAllEventHandels()
        {
            lock (this)
            {
                if (NewClientConnected != null)
                {
                    var del = NewClientConnected.GetInvocationList();
                    for (int i = 0; i < del.Length; i++)
                    {
                        NewClientConnected -= del[i] as Action<IRemoteXTMF>;
                    }
                }
                if (NewClientConnected != null)
                {
                    var del = NewClientConnected.GetInvocationList();
                    for (int i = 0; i < del.Length; i++)
                    {
                        NewClientConnected -= del[i] as Action<IRemoteXTMF>;
                    }
                }
                if (ClientDisconnected != null)
                {
                    var del = ClientDisconnected.GetInvocationList();
                    for (int i = 0; i < del.Length; i++)
                    {
                        ClientDisconnected -= del[i] as Action<IRemoteXTMF>;
                    }
                }
                if (ProgressUpdated != null)
                {
                    var del = ProgressUpdated.GetInvocationList();
                    for (int i = 0; i < del.Length; i++)
                    {
                        ProgressUpdated -= del[i] as Action<IRemoteXTMF, float>;
                    }
                }
                if (ClientRunComplete != null)
                {
                    var del = ClientRunComplete.GetInvocationList();
                    for (int i = 0; i < del.Length; i++)
                    {
                        ClientRunComplete -= del[i] as Action<IRemoteXTMF, int, string>;
                    }
                }
                if (AllModelSystemRunsComplete != null)
                {
                    var del = AllModelSystemRunsComplete.GetInvocationList();
                    for (int i = 0; i < del.Length; i++)
                    {
                        AllModelSystemRunsComplete -= del[i] as Action;
                    }
                }
                RemoveAll(_CustomHandlers);
                RemoveAll(_CustomReceivers);
                RemoveAll(_CustomSenders);
            }
        }

        private void RemoveAll<K, T>(ConcurrentDictionary<K, T> concurrentDictionary)
        {
            foreach (var h in concurrentDictionary)
            {
                concurrentDictionary.TryRemove(h.Key, out T toRemove);
            }
        }

        internal void ReleaseRegisteredHandlers()
        {
            RemoveAllEventHandels();
        }
    }
}