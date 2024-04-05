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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace XTMF.Run;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "-pipe")
        {
            StartupExecuteRunsInADifferentProcess(args[1]);
            return;
        }
        if (args.Length != 3)
        {
            Console.WriteLine("Usage: [ProjectName] [ModelSystemName] [RunName]");
            return;
        }
        RunModelSystemFromProjectPath(args);
    }

    private static void RunModelSystemFromProjectPath(string[] args)
    {
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
        using var projectSession = runtime.ProjectController.EditProject(project);
        var modelSystems = projectSession.Project.ProjectModelSystems.Select((m, i) => new { MSS = m, Index = i }).Where((m, i) => m.MSS.Name == modelSystemName).ToList();
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

    private static XTMFRun CurrentRun;

    private static IConfiguration Configuration;

    private static void StartupExecuteRunsInADifferentProcess(string pipeName)
    {
        using var clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        if (!clientStream.IsConnected)
        {
            clientStream.Connect();
        }
        IModelSystemStructure root = null;
        using var messagesToSend = new BlockingCollection<byte[]>();
        XTMFRuntime runtime = null;
        // create the client
        Task.Factory.StartNew(() =>
        {
            var reader = new BinaryReader(clientStream, Encoding.Unicode, true);
            Configuration config = new(reader.ReadString())
            {
                DivertSaveRequests = true
            };
            Configuration = config;
            config.AddingNewProgressReport += (o) =>
            {
                WriteMessageToStream(messagesToSend, (writer) =>
                {
                    var newReport = (IProgressReport)o;
                    var colour = newReport.Colour;
                    writer.Write((Int32)ToHost.ClientCreatedProgressReport);
                    writer.Write(newReport.Name);
                    writer.Write(colour.Item1);
                    writer.Write(colour.Item2);
                    writer.Write(colour.Item3);
                });
            };
            config.ProgressReports.BeforeRemove += (o, e) =>
            {
                var toBeRemoved = config.ProgressReports[e.NewIndex];
                WriteMessageToStream(messagesToSend, (writer) =>
                {
                    writer.Write((Int32)ToHost.ClientRemovedProgressReport);
                    writer.Write(toBeRemoved.Name);
                });
            };
            config.DeletedProgressReports += () =>
            {
                WriteMessageToStream(messagesToSend, (writer) =>
                {
                    writer.Write((Int32)ToHost.ClientClearedProgressReports);
                });
            };
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
                            RunModelSystem(config, reader, messagesToSend);
                            break;
                        case ToClient.RequestProgress:
                            ProgressRequested(messagesToSend);
                            break;
                        case ToClient.RequestStatus:
                            StatusRequested(messagesToSend);
                            break;
                        case ToClient.CancelModelRun:
                            CancelModelSystem(root);
                            Console.WriteLine("Model System cancelled by host.");
                            return;
                        case ToClient.KillModelRun:
                            Console.WriteLine("Model system termination signalled.");
                            return;
                        default:
                            Console.WriteLine("Unknown command!");
                            return;
                    }
                }
            }
            finally
            {
                messagesToSend?.CompleteAdding();
                Environment.Exit(0);
            }
        }, TaskCreationOptions.LongRunning);
        // Send out the messages as they arise
        foreach (var msg in messagesToSend.GetConsumingEnumerable())
        {
            clientStream.Write(msg, 0, msg.Length);
        }
    }

    private static void RunModelSystem(Configuration config, BinaryReader reader, BlockingCollection<byte[]> messageQueue)
    {
        var runName = reader.ReadString();
        var runDirectory = reader.ReadString();
        var deleteDirectory = reader.ReadBoolean();
        var modelSystemString = reader.ReadString();
        Task.Factory.StartNew(() =>
        {
            try
            {
                if (deleteDirectory && Directory.Exists(runDirectory))
                {
                    Directory.Delete(runDirectory, true);
                }
            }
            catch
            {
                // if there is an error it is alright to just continue
            }
            var run = XTMFRun.CreateRemoteClient(config, runName, runDirectory, modelSystemString);
            run.ValidationError += (message) =>
            {
                WriteMessageToStream(messageQueue, (writer) =>
                {
                    writer.Write((Int32)ToHost.ClientErrorValidatingModelSystem);
                    WriteErrors(writer, message);
                });
            };
            run.RuntimeValidationError += (message) =>
            {
                WriteMessageToStream(messageQueue, (writer) =>
                {
                    writer.Write((Int32)ToHost.ClientErrorRuntimeValidation);
                    WriteErrors(writer, message);
                });
                messageQueue.CompleteAdding();
            };
            run.RuntimeError += (error) =>
            {
                WriteMessageToStream(messageQueue, (writer) =>
                {
                    writer.Write((Int32)ToHost.ClientErrorWhenRunningModelSystem);
                    WriteError(writer, error);
                });
                messageQueue.CompleteAdding();
            };
            run.ProjectSavedByRun += (_, _2) =>
            {
                WriteMessageToStream(messageQueue, (writer) =>
                {
                    writer.Write((Int32)ToHost.ProjectSaved);
                    writer.Flush();
                    run.ModelSystemStructureModelRoot.Save(writer.BaseStream);
                    writer.BaseStream.Flush();
                });
            };
            run.RunCompleted += () =>
            {
                WriteMessageToStream(messageQueue, (writer) =>
                {
                    writer.Write((Int32)ToHost.ClientFinishedModelSystem);
                });
                messageQueue.CompleteAdding();
            };
            CurrentRun = run;
            run.Start();
            run.Wait();
        }, TaskCreationOptions.LongRunning);
    }

    private static void WriteErrors(BinaryWriter writer, List<ErrorWithPath> errors)
    {
        writer.Write(errors.Count);
        foreach (var error in errors)
        {
            WriteError(writer, error);
        }
    }

    private static void WriteError(BinaryWriter writer, ErrorWithPath error)
    {
        var path = error.Path;
        if (path != null)
        {
            writer.Write(path.Count);
            foreach (var point in path)
            {
                writer.Write(point);
            }
        }
        else
        {
            writer.Write((Int32)(-1));
        }
        writer.Write(error.Message);
        writer.Write(error.StackTrace ?? String.Empty);
        writer.Write(error.ModuleName ?? String.Empty);
    }

    private static void WriteMessageToStream(BlockingCollection<byte[]> messagesToSend, Action<BinaryWriter> action)
    {
        using var backend = new MemoryStream();
        BinaryWriter writer = new(backend, System.Text.Encoding.Unicode, true);
        action(writer);
        writer.Flush();
        messagesToSend.Add(backend.ToArray());
    }

    private static void ProgressRequested(BlockingCollection<byte[]> messagesToSend)
    {
        var root = CurrentRun?.MST;
        if (root != null)
        {
            WriteMessageToStream(messagesToSend, (writer) =>
            {
                try
                {
                    var progress = root.Progress;
                    // make sure there is no error gathering the progress
                    writer.Write((Int32)ToHost.ClientReportedProgress);
                    writer.Write(progress);
                    var reports = Configuration.ProgressReports;
                    if (reports != null)
                    {
                        lock (((ICollection)reports).SyncRoot)
                        {
                            var length = reports.Count;
                            writer.Write(length);
                            int i = 0;
                            for (; i < length; ++i)
                            {
                                IProgressReport report = reports[i];
                                writer.Write(report.Name);
                                writer.Write(report.GetProgress());
                                writer.Write(report.Colour.Item1);
                                writer.Write(report.Colour.Item2);
                                writer.Write(report.Colour.Item3);
                            }
                        }
                    }
                    else
                    {
                        writer.Write(0);
                    }
                }
                catch
                {
                }
            });
        }
    }

    private static void StatusRequested(BlockingCollection<byte[]> messagesToSend)
    {
        var root = CurrentRun?.MST;
        if (root != null)
        {
            WriteMessageToStream(messagesToSend, (writer) =>
            {
                try
                {
                    var status = root.ToString();
                    // make sure there is no error gathering the progress
                    writer.Write((Int32)ToHost.ClientReportedStatus);
                    writer.Write(status);
                }
                catch
                {
                }
            });
        }
        else
        {
            WriteMessageToStream(messagesToSend, (writer) =>
            {
                // make sure there is no error gathering the progress
                writer.Write((Int32)ToHost.ClientReportedStatus);
                writer.Write("Model System Initializing");
            });
        }
    }

    private static void CancelModelSystem(IModelSystemStructure current)
    {
        (current.Module as IModelSystemTemplate)?.ExitRequest();
        var children = current.Children;
        if (children == null)
        {
            foreach (var child in children)
            {
                CancelModelSystem(child);
            }
        }
    }

    private static void Run(int index, ProjectEditingSession projectSession, string runName)
    {
        using var modelSystemSession = projectSession.EditModelSystem(index);
        XTMFRun run;
        string error = null;
        if ((run = modelSystemSession.Run(runName, ref error, true, true, true)) == null)
        {
            Console.WriteLine("Unable to run \r\n" + error);
            return;
        }
        modelSystemSession.ExecuteRun(run, true);
        run.RunCompleted += Run_RunComplete;
        run.Wait();
    }

    private static void Run_RunComplete()
    {
        Environment.Exit(0);
    }
}


