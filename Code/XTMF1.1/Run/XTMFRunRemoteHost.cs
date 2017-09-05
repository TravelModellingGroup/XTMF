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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Run
{
    sealed class XTMFRunRemoteHost : XTMFRun
    {
        private float RemoteProgress = 0.0f;
        private string RemoteStatus = String.Empty;

        private NamedPipeServerStream Pipe;

        public override bool RunsRemotely => throw new NotImplementedException();

        public XTMFRunRemoteHost(IConfiguration configuration, string runName, string runDirectory)
            : base(runName, runDirectory, configuration)
        {

        }

        private string GetXTMFRunFileName() => Path.Combine(Path.GetDirectoryName(
    Assembly.GetEntryAssembly().Location
    ), "XTMF.Run.exe");

        private void StartupHost()
        {
            var pipeName = Guid.NewGuid().ToString();
            Pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            var info = new ProcessStartInfo(GetXTMFRunFileName(), "-pipe " + pipeName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            DataReceivedEventHandler messageHandler = (sender, args) =>
            {
                if (args.Data != null)
                {
                    Console.WriteLine(args.Data);
                }
            };
            var runProcess = new Process
            {
                StartInfo = info
            };
            runProcess.OutputDataReceived += messageHandler;
            runProcess.ErrorDataReceived += messageHandler;
            runProcess.Start();
            runProcess.BeginOutputReadLine();
            runProcess.BeginErrorReadLine();
            Pipe.WaitForConnection();
        }

        private void RequestSignal(ToClient signal)
        {
            lock (this)
            {
                BinaryWriter writer = new BinaryWriter(Pipe, System.Text.Encoding.UTF8, true);
                writer.Write((Int32)signal);
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

        private void RunRemotely()
        {
            StartClientListener();
            // Send the instructiosn to run the model system
            InitializeClientAndSendModelSystem();

            RequestSignal(ToClient.KillModelRun);
        }

        private void InitializeClientAndSendModelSystem()
        {
            lock (this)
            {
                BinaryWriter writer = new BinaryWriter(Pipe, System.Text.Encoding.UTF8, true);
                writer.Write((Configuration as Configuration)?.ConfigurationFileName ?? "");
                WriteModelSystemToStream(writer);
                writer.Flush();
            }
        }

        private void StartClientListener()
        {
            Task.Factory.StartNew(() =>
            {
                BinaryReader reader = new BinaryReader(Pipe, System.Text.Encoding.UTF8, true);
                while (true)
                {
                    switch ((ToHost)reader.ReadInt32())
                    {
                        case ToHost.Heartbeat:
                            break;
                        case ToHost.ClientReportedProgress:
                            RemoteProgress = reader.ReadInt32();
                            break;
                        case ToHost.ClientReportedStatus:
                            RemoteStatus = reader.ReadString();
                            break;
                        case ToHost.ClientErrorValidatingModelSystem:
                            SendValidationError(reader.ReadString());
                            return;
                        case ToHost.ClientErrorWhenRunningModelSystem:
                            SendRuntimeError(reader.ReadString(), reader.ReadString());
                            return;

                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private void WriteModelSystemToStream(BinaryWriter writer)
        {
            using (var memStream = new MemoryStream())
            {
                ModelSystemStructureModelRoot.RealModelSystemStructure.Save(memStream);
                memStream.Seek(0, SeekOrigin.Begin);
                BinaryReader reader = new BinaryReader(memStream);
                writer.Write((UInt32)ToClient.RunModelSystem);
                writer.Write(RunName);
                writer.Write(RunDirectory);
                writer.Write(reader.ReadString());
            }
        }

        public override bool ExitRequest()
        {
            RequestSignal(ToClient.KillModelRun);
            return true;
        }

        public override Tuple<byte, byte, byte> PollColour()
        {
            return new Tuple<byte, byte, byte>(50, 150, 50);
        }

        public override float PollProgress()
        {
            RequestSignal(ToClient.RequestProgress);
            return RemoteProgress;
        }

        public override string PollStatusMessage()
        {
            RequestSignal(ToClient.RequestStatus);
            return RemoteStatus;
        }

        public override bool DeepExitRequest()
        {
            throw new NotImplementedException();
        }

        public override List<Tuple<IModelSystemStructure, Queue<int>, string>> CollectRuntimeValidationErrors()
        {
            throw new NotImplementedException();
        }

        public override List<Tuple<IModelSystemStructure, Queue<int>, string>> CollectValidationErrors()
        {
            throw new NotImplementedException();
        }

        public override void Start()
        {
            throw new NotImplementedException();
        }

        public override void Wait()
        {
            throw new NotImplementedException();
        }

        public override void TerminateRun()
        {
            RequestSignal(ToClient.KillModelRun);
        }
    }
}
