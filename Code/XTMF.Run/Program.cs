/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using XTMF.Bus;
using System.Collections.Concurrent;

namespace XTMF.Run
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "-pipe")
            {
                StartupExecuteRunsInADifferentProcess(args[1]);
            }

            if (args.Length != 3)
            {
                Console.WriteLine("Usage: [ProjectName] [ModelSystemName] [RunName]");
                return;
            }
            string projectName = args[0];
            string modelSystemName = args[1];
            string runName = args[2];
            var runtime = new XTMFRuntime();
            string error = null;
            Project project;
            if ((project = runtime.ProjectController.Load(projectName, ref error)) == null)
            {
                Console.WriteLine("Error loading project\r\n" + error);
                return;
            }
            using (var projectSession = runtime.ProjectController.EditProject(project))
            {
                var modelSystems = projectSession.Project.ModelSystemStructure.Select((m, i) => new { MSS = m, Index = i }).Where((m, i) => m.MSS.Name == modelSystemName).ToList();
                switch (modelSystems.Count)
                {
                    case 0:
                        Console.WriteLine("There was no model system in the project " + project.Name + " called " + modelSystemName + "!");
                        return;
                    case 1:
                        Run(modelSystems[0].Index, projectSession, runName);
                        break;
                    default:
                        Console.WriteLine("There were multiple model systems in the project " + project.Name + " called " + modelSystemName + "!");
                        return;
                }
            }
        }

        private static void StartupExecuteRunsInADifferentProcess(string pipeName)
        {
            using (NamedPipeClientStream clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {

                IModelSystemStructure root = null;
                using (BlockingCollection<byte[]> messagesToSend = new BlockingCollection<byte[]>())
                {
                    XTMFRuntime runtime = null;
                    // create the client
                    Task.Factory.StartNew(() =>
                    {
                        BinaryReader reader = new BinaryReader(clientStream);
                        Configuration config = new Configuration(reader.ReadString());
                        runtime = new XTMFRuntime(config);
                        try
                        {
                            while (true)
                            {
                                switch ((ToClient)reader.ReadInt32())
                                {
                                    case ToClient.Heartbeat:
                                        // do nothing
                                        break;
                                    case ToClient.RunModelSystem:
                                        RunModelSystem(config, ref root, reader, messagesToSend);
                                        break;
                                    case ToClient.RequestProgress:
                                        ProgressRequested(root, messagesToSend);
                                        break;
                                    case ToClient.CancelModelRun:
                                        CancelModelSystem(root);
                                        Console.WriteLine("Model System canceled by host.");
                                        Environment.Exit(0);
                                        break;
                                    default:
                                        Console.WriteLine("Unknown command!");
                                        Environment.Exit(-1);
                                        break;
                                }
                            }
                        }
                        catch
                        {
                            Environment.Exit(0);
                        }
                    }, TaskCreationOptions.LongRunning);

                    // Send out the messages as they arise
                    foreach (var msg in messagesToSend.GetConsumingEnumerable())
                    {
                        clientStream.Write(msg, 0, msg.Length);
                    }
                }
            }
        }

        private static void RunModelSystem(Configuration config, ref IModelSystemStructure root, BinaryReader reader, BlockingCollection<byte[]> messageQueue)
        {
            var runDirectory = reader.ReadString();
            var modelSystemString = reader.ReadString();
            Task.Factory.StartNew(() =>
            {
                if (!Directory.Exists(runDirectory))
                {
                    Directory.CreateDirectory(runDirectory);
                }
                var run = new XTMFRun(config, runDirectory, modelSystemString);
                run.ValidationError += (message) =>
                {
                    WriteMessageToStream(messageQueue, (writer) =>
                    {
                        writer.Write((Int32)ToHost.ClientErrorValidatingModelSystem);
                        writer.Write(message);
                    });
                };
                run.RuntimeValidationError += (error) =>
                {
                    WriteMessageToStream(messageQueue, (writer) =>
                    {
                        writer.Write((Int32)ToHost.ClientErrorValidatingModelSystem);
                        writer.Write(error);
                    });
                };
                run.RuntimeError += (message, stack) =>
                {
                    WriteMessageToStream(messageQueue, (writer) =>
                    {
                        writer.Write((Int32)ToHost.ClientErrorWhenRunningModelSystem);
                        writer.Write(message);
                        writer.Write(stack);
                    });
                };
                run.RunComplete += () =>
                {
                    WriteMessageToStream(messageQueue, (writer) =>
                    {
                        writer.Write((Int32)ToHost.ClientFinishedModelSystem);
                    });
                };
                run.Start();
                run.Wait();
            }, TaskCreationOptions.LongRunning);
        }


        private static void WriteMessageToStream(BlockingCollection<byte[]> messagesToSend, Action<BinaryWriter> action)
        {
            using (var backend = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(backend, System.Text.Encoding.UTF8, true);
                action(writer);
                writer.Flush();
                messagesToSend.Add(backend.ToArray());
            }
        }

        private static void ProgressRequested(IModelSystemStructure root, BlockingCollection<byte[]> messagesToSend)
        {
            if (root != null)
            {
                WriteMessageToStream(messagesToSend, (writer) =>
                {
                    try
                    {
                        var module = root.Module;
                        if (module == null)
                        {
                            var progress = module.Progress;
                            var status = module.ToString();
                            // make sure there is no error gathering the progress
                            writer.Write((Int32)ToHost.ProgressUpdate);
                            writer.Write(status);
                            writer.Write(progress);
                        }
                    }
                    catch
                    {
                    }
                });
            }
        }

        private static void CancelModelSystem(IModelSystemStructure current)
        {
            (current.Module as IModelSystemTemplate)?.ExitRequest();
            var children = current.Children;
            if (children != null)
            {
                foreach (var child in children)
                {
                    CancelModelSystem(child);
                }
            }
        }

        private static void Run(int index, ProjectEditingSession projectSession, string runName)
        {
            using (var modelSystemSession = projectSession.EditModelSystem(index))
            {
                string error = null;
                XTMFRun run;
                if ((run = modelSystemSession.Run(runName, ref error)) == null)
                {
                    Console.WriteLine("Unable to run \r\n" + error);
                    return;
                }
                run.RunComplete += Run_RunComplete;
                run.Start();
                run.Wait();
            }
        }

        private static void Run_RunComplete()
        {
            Environment.Exit(0);
        }
    }
}


