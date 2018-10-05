/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace XTMF.Run
{
    sealed class XTMFRunRemoteHost : XTMFRun
    {
        private float _RemoteProgress = 0.0f;

        private string _RemoteStatus = String.Empty;

        private NamedPipeServerStream _Pipe;
        /// <summary>
        /// Bound to in order to do a wait.
        /// This is triggered when the client has exited.
        /// </summary>
        private SemaphoreSlim ClientExiting = new SemaphoreSlim(0);

        public override bool RunsRemotely => true;

        private List<ILinkedParameter> _LinkedParameters;
        private readonly string _modelSystemAsString;
        private bool _deleteDirectory;

        public XTMFRunRemoteHost(IConfiguration configuration, ModelSystemStructureModel root, List<ILinkedParameter> linkedParameters, string runName,
            string runDirectory, bool deleteDirectory)
            : base(runName, runDirectory, configuration)
        {
            ModelSystemStructureModelRoot = root;
            _deleteDirectory = deleteDirectory;
            _LinkedParameters = linkedParameters;
            using (MemoryStream memStream = new MemoryStream())
            {
                WriteModelSystemToStream(memStream);
                _modelSystemAsString = Encoding.Unicode.GetString(memStream.ToArray());
            }
        }

        private string GetXTMFRunFileName() => Path.Combine(Path.GetDirectoryName(
                Assembly.GetEntryAssembly().Location), "XTMF.Run.exe");

        private void StartupHost()
        {
            var debugMode = !((Configuration)Configuration).RunInSeperateProcess; // Debugger.IsAttached;
            var pipeName = debugMode ? "DEBUG_MODEL_SYSTEM" : Guid.NewGuid().ToString();
            _Pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            if (!debugMode)
            {
                var info = new ProcessStartInfo(GetXTMFRunFileName(), "-pipe " + pipeName)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                void messageHandler(object sender, DataReceivedEventArgs args)
                {
                    if (args.Data != null)
                    {
                        SendRunMessage(args.Data);
                    }
                }
                var runProcess = new Process
                {
                    StartInfo = info,
                    EnableRaisingEvents = true
                };
                runProcess.OutputDataReceived += messageHandler;
                runProcess.ErrorDataReceived += messageHandler;
                runProcess.Exited += RunProcess_Exited;
                runProcess.Start();
                runProcess.BeginOutputReadLine();
                runProcess.BeginErrorReadLine();
            }
            _Pipe.WaitForConnection();
        }

        private void RunProcess_Exited(object sender, EventArgs e)
        {
            // make sure the pipe is destroyed if the other process has
            // terminated
            _Pipe?.Dispose();
            ClientExiting.Release();
            InvokeRunCompleted();
        }

        private void RequestSignal(ToClient signal)
        {
            lock (this)
            {
                BinaryWriter writer = new BinaryWriter(_Pipe, System.Text.Encoding.Unicode, true);
                try
                {
                    writer.Write((Int32)signal);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return;
                }

            }
        }

        private void RequestRemoteProgress()
        {
            RequestSignal(ToClient.RequestProgress);
        }

        private void RequestRemoteStatus()
        {
            RequestSignal(ToClient.RequestStatus);
        }

        private void InitializeClientAndSendModelSystem()
        {
            lock (this)
            {
                BinaryWriter writer = new BinaryWriter(_Pipe, System.Text.Encoding.Unicode, true);
                writer.Write((Configuration as Configuration)?.ConfigurationFileName ?? "");
            }
            WriteModelSystemStringToPipe();
        }

        private void StartClientListener()
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    BinaryReader reader = new BinaryReader(_Pipe, System.Text.Encoding.Unicode, true);
                    while (_Pipe?.IsConnected == true)
                    {
                        try
                        {
                            switch ((ToHost)reader.ReadInt32())
                            {
                                case ToHost.Heartbeat:
                                    break;
                                case ToHost.ClientReportedProgress:
                                    ReadProgress(reader);
                                    break;
                                case ToHost.ClientReportedStatus:
                                    _RemoteStatus = reader.ReadString();
                                    break;
                                case ToHost.ClientErrorValidatingModelSystem:
                                    InvokeValidationError(ReadErrors(reader));
                                    return;
                                case ToHost.ClientErrorWhenRunningModelSystem:
                                    InvokeRuntimeError(ReadError(reader));
                                    return;
                                case ToHost.ClientCreatedProgressReport:
                                    AddProgressReport(reader);
                                    break;
                                case ToHost.ClientRemovedProgressReport:
                                    RemoveProgressRport(reader);
                                    break;
                                case ToHost.ClientClearedProgressReports:
                                    ClearProgressReports();
                                    break;
                                case ToHost.ClientFinishedModelSystem:
                                case ToHost.ClientExiting:
                                    return;
                                case ToHost.ProjectSaved:
                                    LoadAndSignalModelSystem(reader);
                                    break;
                                case ToHost.ClientErrorRuntimeValidation:
                                    InvokeRuntimeValidationError(ReadErrors(reader));
                                    return;
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
                finally
                {
                    ClientExiting.Release();
                }
            }, TaskCreationOptions.LongRunning);
        }

        private class ProgressReport : IProgressReport
        {
            public Tuple<byte, byte, byte> Colour { get; set; }

            public Func<float> GetProgress { get; }

            internal float Progress;

            public string Name { get; }

            public ProgressReport(string name, byte r, byte g, byte b)
            {
                Name = name;
                GetProgress = () => Progress;
                Colour = new Tuple<byte, byte, byte>(r, g, b);
            }
        }

        private void ClearProgressReports()
        {
            Configuration.DeleteAllProgressReport();
        }

        private void AddProgressReport(BinaryReader reader)
        {
            Configuration.ProgressReports.Add(new ProgressReport(reader.ReadString(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()));
        }

        private void RemoveProgressRport(BinaryReader reader)
        {
            var name = reader.ReadString();
            var toRemove = Configuration.ProgressReports.FirstOrDefault(rep => rep.Name == name);
            if (toRemove != null)
            {
                Configuration.ProgressReports.Remove(toRemove);
            }
        }

        private void ReadProgress(BinaryReader reader)
        {
            _RemoteProgress = reader.ReadSingle();
            var length = reader.ReadInt32();
            if (length > 0)
            {
                var reports = Configuration.ProgressReports;
                lock (((ICollection)reports).SyncRoot)
                {
                    var givenReports = new List<(string name, float progress, byte r, byte g, byte b)>(length);
                    for (int i = 0; i < length; i++)
                    {
                        givenReports.Add((reader.ReadString(), reader.ReadSingle(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte()));
                    }
                    foreach (var (name, progress, r, g, b) in givenReports)
                    {
                        foreach (var holdRep in reports)
                        {
                            if (name == holdRep.Name)
                            {
                                if (holdRep is ProgressReport remoteProgress)
                                {
                                    remoteProgress.Progress = progress;
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void LoadAndSignalModelSystem(BinaryReader reader)
        {
            try
            {
                var length = (int)reader.ReadInt64();
                byte[] msText = new byte[length];
                var soFar = 0;
                while (soFar < length)
                {
                    soFar += reader.Read(msText, soFar, length - soFar);
                }
                using (var stream = new MemoryStream(msText))
                {
                    var mss = ModelSystemStructure.Load(stream, Configuration);
                    SendProjectSaved(mss as ModelSystemStructure);
                }
            }
            catch (Exception e)
            {
                SendRunMessage(e.Message + "\r\n" + e.StackTrace);
            }
        }

        private static List<ErrorWithPath> ReadErrors(BinaryReader reader)
        {
            int numberOfErrors = reader.ReadInt32();
            List<ErrorWithPath> errors = new List<ErrorWithPath>(numberOfErrors);
            for (int i = 0; i < numberOfErrors; i++)
            {
                errors.Add(ReadError(reader));
            }
            return errors;
        }

        private static ErrorWithPath ReadError(BinaryReader reader)
        {
            int pathSize = reader.ReadInt32();
            List<int> path = null;
            if (pathSize > 0)
            {
                path = new List<int>(pathSize);
                for (int j = 0; j < pathSize; j++)
                {
                    path.Add(reader.ReadInt32());
                }
            }
            var message = reader.ReadString();
            var stackTrace = reader.ReadString();
            var moduleName = reader.ReadString();
            if (String.IsNullOrWhiteSpace(stackTrace))
            {
                stackTrace = null;
            }
            return new ErrorWithPath(path, message, stackTrace, moduleName);
        }

        private static string LookupName(IModuleParameter reference, IModelSystemStructure current)
        {
            var param = current.Parameters;
            if (param != null)
            {
                int index = param.Parameters.IndexOf(reference);
                if (index >= 0)
                {
                    return current.Parameters.Parameters[index].Name;
                }
            }
            var childrenList = current.Children;
            if (childrenList != null)
            {
                for (int i = 0; i < childrenList.Count; i++)
                {
                    var res = LookupName(reference, childrenList[i]);
                    if (res != null)
                    {
                        // make sure to use an escape character before the . to avoid making the mistake of reading it as another index
                        return string.Concat(current.IsCollection ? i.ToString()
                            : childrenList[i].ParentFieldName.Replace(".", "\\."), '.', res);
                    }
                }
            }
            return null;
        }

        private void WriteModelSystemStringToPipe()
        {
            lock (this)
            {
                using (var memStream = new MemoryStream())
                {
                    BinaryWriter pipeWriter = new BinaryWriter(_Pipe, System.Text.Encoding.Unicode, true);
                    WriteModelSystemToStream(memStream);
                    pipeWriter.Write((UInt32)ToClient.RunModelSystem);
                    pipeWriter.Write(RunName);
                    pipeWriter.Write(RunDirectory);
                    pipeWriter.Write(_deleteDirectory);
                    pipeWriter.Write(_modelSystemAsString);

                }
            }
        }

        private void WriteModelSystemToStream(MemoryStream memStream)
        {
            using (XmlWriter xml = XmlTextWriter.Create(memStream, new XmlWriterSettings() { Encoding = Encoding.Unicode }))
            {
                xml.WriteStartDocument();
                xml.WriteStartElement("Root");
                var root = ModelSystemStructureModelRoot.RealModelSystemStructure;
                root.Save(xml);
                xml.WriteStartElement("LinkedParameters");
                foreach (var lp in _LinkedParameters)
                {
                    xml.WriteStartElement("LinkedParameter");
                    xml.WriteAttributeString("Name", lp.Name);
                    xml.WriteAttributeString("Value", lp.Value ?? String.Empty);
                    foreach (var reference in lp.Parameters)
                    {
                        xml.WriteStartElement("Reference");
                        xml.WriteAttributeString("Name", LookupName(reference, root));
                        xml.WriteEndElement();
                    }
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndDocument();
                xml.Flush();
            }
        }

        public override bool ExitRequest()
        {
            if (_runStarted)
            {
                RequestSignal(ToClient.KillModelRun);
            }
            else
            {
                InvokeRunCompleted();
            }
            return true;
        }

        public override Tuple<byte, byte, byte> PollColour() => new Tuple<byte, byte, byte>(50, 150, 50);

        public override float PollProgress()
        {
            RequestSignal(ToClient.RequestProgress);
            return _RemoteProgress;
        }

        public override string PollStatusMessage()
        {
            RequestSignal(ToClient.RequestStatus);
            return _RemoteStatus;
        }

        public override bool DeepExitRequest()
        {
            RequestSignal(ToClient.KillModelRun);
            return true;
        }

        private volatile bool _runStarted = false;

        public override void Start()
        {
            Task.Run(() =>
            {
                StartupHost();
                StartClientListener();
                _runStarted = true;
                // Send the instructions to run the model system
                InitializeClientAndSendModelSystem();
                SetStatusToRunning();
            });
        }

        public override void Wait() => ClientExiting.Wait();

        public override void TerminateRun()
        {
            if (_runStarted)
            {
                RequestSignal(ToClient.KillModelRun);
            }
            else
            {
                InvokeRunCompleted();
                ClientExiting.Release();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _Pipe?.Dispose();
            ClientExiting?.Dispose();
        }
    }
}
