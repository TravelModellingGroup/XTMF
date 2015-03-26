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
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading;
using XTMF;

namespace TMG.Emme
{
    public sealed class ModellerController : Controller, IDisposable
    {
        /// <summary>
        /// Tell the bridge to clean out the modeller's logbook
        /// </summary>
        private const int SignalCleanLogbook = 6;

        /// <summary>
        /// We receive this error if the bridge can not get the parameters to match
        /// the selected tool
        /// </summary>
        private const int SignalParameterError = 4;

        internal void SetLogbookWriting(bool v)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Receive a signal that contains a progress report
        /// </summary>
        private const int SignalProgressReport = 7;

        /// <summary>
        /// We receive this when the Bridge has completed its module run
        /// </summary>
        private const int SignalRunComplete = 3;

        /// <summary>
        /// We receive this when the Bridge has completed its module run and that there is a return value as well
        /// </summary>
        private const int SignalRunCompleteWithParameter = 8;

        /// <summary>
        /// We revive this error if the tool that we tell the bridge to run throws a
        /// runtime exception that is not handled within the tool
        /// </summary>
        private const int SignalRuntimeError = 5;

        /// <summary>
        /// We will receive this from the ModellerBridge
        /// when it is ready to start processing
        /// </summary>
        private const int SignalStart = 0;

        /// <summary>
        /// We will send this signal when we want to start to run a new module
        /// </summary>
        private const int SignalStartModule = 2;

        /// <summary>
        /// This is the message that we will send when it is time to shutdown the bridge.
        /// If we receive it, then we know that the bridge is in a panic and has exited
        /// </summary>
        private const int SignalTermination = 1;

        /// <summary>
        /// Signal the bridge to check if a tool namespace exists
        /// </summary>
        private const int SignalCheckToolExists = 9;

        /// <summary>
        /// Signal from the bridge throwing an exception that the tool namespace could not be found
        /// </summary>
        private const int SignalToolDoesNotExistError = 10;

        /// <summary>
        /// Signal from the bridge that a print statement has been called, to write to the XTMF Console
        /// </summary>
        private const int SignalSentPrintMessage = 11;

        private const int SignalDisableLogbook = 12;

        private const int SignalEnableLogbook = 13;

        public bool WriteToLogbook
        {
            set
            {
                lock (this)
                {
                    try
                    {
                        // clear out all of the old input before starting
                        this.FromEmme.BaseStream.Flush();
                        BinaryWriter writer = new BinaryWriter(this.ToEmme.BaseStream);
                        writer.Write(value ? ModellerController.SignalEnableLogbook : ModellerController.SignalDisableLogbook);
                        writer.Flush();
                        // now that we have setup the macro, we can force the writer out of scope
                        writer = null;
                    }
                    catch (IOException e)
                    {
                        throw new XTMFRuntimeException("I/O Connection with emme while sending data, with:\r\n" + e.Message);
                    }
                }
            }
        }

        private NamedPipeServerStream PipeFromEmme;
        private string PipeName;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="projectFile"></param>
        /// <param name="PerformanceAnalysis"></param>
        public ModellerController(string projectFile, bool PerformanceAnalysis = false, string userInitials = "XTMF")
        {
            if(!projectFile.EndsWith(".emp") | !File.Exists(projectFile))
            {
                throw new XTMFRuntimeException(this.AddQuotes(projectFile) + " is not an existing Emme project file (*.emp)");
            }

            //Python invocation command:
            //[FullPath...python.exe] -u [FullPath...ModellerBridge.py] [FullPath...EmmeProject.emp] [User initials] [[Performance (optional)]] 

            // Get the path of the Python executable
            string emmePath = Environment.GetEnvironmentVariable("EMMEPATH");
            if(String.IsNullOrWhiteSpace(emmePath))
            {
                throw new XTMFRuntimeException("Please make sure that EMMEPATH is on the system environment variables!");
            }
            string pythonPath = this.AddQuotes(Path.Combine(emmePath, Path.Combine(this.FindPython(emmePath), @"python.exe")));

            // Get the path of ModellerBridge
            // Learn where the modules are stored so we can find the python script
            // The Entry assembly will be the XTMF.GUI or XTMF.RemoteClient
            var codeBase = Assembly.GetEntryAssembly().CodeBase;
            string programPath = null;
            // make sure that the path is not relative
            try
            {
                programPath = Path.GetFullPath(codeBase);
            }
            catch
            {
                programPath = codeBase.Replace("file:///", String.Empty);
            }
            // Since the modules are always located in the ~/Modules subdirectory for XTMF,
            // we can just go in there to find the script
            var modulesDirectory = Path.Combine(Path.GetDirectoryName(programPath), "Modules");
            // When emme is installed it will link the .py to their python interpreter properly
            string argumentString = AddQuotes(Path.Combine(modulesDirectory, "ModellerBridge.py"));
            PipeName = Guid.NewGuid().ToString();
            PipeFromEmme = new NamedPipeServerStream(PipeName, PipeDirection.In);
            //The first argument that gets passed into the Bridge is the name of the Emme project file
            argumentString += " " + this.AddQuotes(projectFile) + " " + userInitials + " " + (PerformanceAnalysis ? 1 : 0) + " \"" + PipeName + "\"";

            //Setup up the new process
            // When creating this process, we can not start in our own window because we are re-directing the I/O
            // and windows won't allow us to have a window and take its standard I/O streams at the same time
            this.Emme = new Process();
            this.Emme.StartInfo.FileName = pythonPath;
            this.Emme.StartInfo.Arguments = "-u " + argumentString;
            this.Emme.StartInfo.CreateNoWindow = true;
            this.Emme.StartInfo.UseShellExecute = false;
            this.Emme.StartInfo.RedirectStandardInput = true;
            this.Emme.StartInfo.RedirectStandardOutput = true;
            this.Emme.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            //Start the new process
            try
            {
                Emme.Start();
            }
            catch
            {
                throw new XTMFRuntimeException("Unable to create a bridge to EMME!");
            }
            // Give some short names for the streams that we will be using
            this.ToEmme = this.Emme.StandardInput;
            // no more standard out
            this.PipeFromEmme.WaitForConnection();
            //this.FromEmme = this.Emme.StandardOutput;
        }

        ~ModellerController()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Try to clean out the Emme Modeller's log-book
        /// </summary>
        /// <returns>If we successfully cleaned the log book</returns>
        public bool CleanLogbook()
        {
            lock (this)
            {
                try
                {
                    BinaryWriter writer = new BinaryWriter(this.ToEmme.BaseStream);
                    writer.Write(ModellerController.SignalCleanLogbook);
                    writer.Flush();
                    // now that we have setup the macro, we can force the writer out of scope
                    writer = null;
                    string _unused = null;
                    return WaitForEmmeResponce(ref _unused, null);
                }
                catch (EndOfStreamException)
                {
                    throw new XTMFRuntimeException("We were unable to communicate with EMME.  Please make sure you have an active EMME license.  Sometimes rebooting has helped fix this bug.");
                }
                catch (IOException)
                {
                    return false;
                }
            }
        }

        private bool WaitForEmmeResponce(ref string returnValue, Action<float> updateProgress)
        {
            // now we need to wait
            try
            {
                string toPrint;
                while(true)
                {
                    BinaryReader reader = new BinaryReader(PipeFromEmme);
                    int result = reader.ReadInt32();
                    switch(result)
                    {
                        case ModellerController.SignalStart:
                            {
                                continue;
                            }
                        case ModellerController.SignalRunComplete:
                            {
                                return true;
                            }
                        case ModellerController.SignalRunCompleteWithParameter:
                            {
                                returnValue = reader.ReadString();
                                return true;
                            }
                        case ModellerController.SignalTermination:
                            {
                                throw new XTMFRuntimeException("The EMME ModellerBridge panicked and unexpectedly shutdown.");
                            }
                        case ModellerController.SignalParameterError:
                            {
                                throw new EmmeToolParameterException("EMME Parameter Error: " + reader.ReadString());
                            }
                        case ModellerController.SignalRuntimeError:
                            {
                                throw new EmmeToolRuntimeException("EMME Runtime " + reader.ReadString());
                            }
                        case ModellerController.SignalToolDoesNotExistError:
                            {
                                throw new EmmeToolCouldNotBeFoundException(reader.ReadString());
                            }
                        case ModellerController.SignalSentPrintMessage:
                            {
                                toPrint = reader.ReadString();
                                Console.Write(toPrint);
                                break;
                            }
                        case ModellerController.SignalProgressReport:
                            {
                                var progress = reader.ReadSingle();
                                if(updateProgress != null)
                                {
                                    updateProgress(progress);
                                }
                                break;
                            }
                        default:
                            {
                                throw new XTMFRuntimeException("Unknown message passed back from the EMME ModellerBridge.  Signal number " + result);
                            }
                    }
                }
            }
            catch (EndOfStreamException)
            {
                throw new XTMFRuntimeException("We were unable to communicate with EMME.  Please make sure you have an active EMME license.  If the problem persists, sometimes rebooting has helped fix this issue with EMME.");
            }
            catch (IOException e)
            {
                throw new XTMFRuntimeException("I/O Connection with emme ended while waiting for data, with:\r\n" + e.Message);
            }
        }

        public bool CheckToolExists(string toolNamespace)
        {
            lock (this)
            {
                try
                {
                    BinaryWriter writer = new BinaryWriter(this.ToEmme.BaseStream);
                    writer.Write(ModellerController.SignalCheckToolExists);
                    writer.Write(toolNamespace);
                    writer.Flush();
                    // now that we have setup the macro, we can force the writer out of scope
                    writer = null;
                }
                catch (IOException e)
                {
                    throw new XTMFRuntimeException("I/O Connection with EMME while sending data, with:\r\n" + e.Message);
                }
                // now we need to wait
                string _unused = null;
                return WaitForEmmeResponce(ref _unused, null);
            }
        }

        public override bool Run(string macroName, string arguments)
        {
            string unused = null;
            return this.Run(macroName, arguments, null, ref unused);
        }

        public bool Run(string macroName, string arguments, ref string returnValue)
        {
            return this.Run(macroName, arguments, null, ref returnValue);
        }

        public bool Run(string macroName, string arguments, Action<float> progressUpdate, ref string returnValue)
        {
            lock (this)
            {
                try
                {
                    // clear out all of the old input before starting
                    BinaryWriter writer = new BinaryWriter(this.ToEmme.BaseStream);
                    writer.Write(ModellerController.SignalStartModule);
                    writer.Write(macroName);
                    writer.Write(arguments);
                    writer.Flush();
                    // now that we have setup the macro, we can force the writer out of scope
                    writer = null;
                }
                catch (IOException e)
                {
                    throw new XTMFRuntimeException("I/O Connection with emme while sending data, with:\r\n" + e.Message);
                }
                return WaitForEmmeResponce(ref returnValue, progressUpdate);
            }
        }

        private string AddQuotes(string fileName)
        {
            return String.Concat("\"", fileName, "\"");
        }

        protected override void Dispose(bool finalizer)
        {
            lock (this)
            {
                if(this.FromEmme != null)
                {
                    this.FromEmme.Close();
                    this.FromEmme = null;
                }

                if(PipeFromEmme != null)
                {
                    PipeFromEmme.Dispose();
                    PipeFromEmme = null;
                }

                if(this.ToEmme != null)
                {
                    // Send our termination message first
                    try
                    {
                        BinaryWriter writer = new BinaryWriter(this.ToEmme.BaseStream);
                        writer.Write(ModellerController.SignalTermination);
                        writer.Flush();
                        writer = null;
                        this.ToEmme.Flush();
                        // after our message has been sent then we can go and kill the stream
                        this.ToEmme.Close();
                        this.ToEmme = null;
                    }
                    catch
                    {
                    }
                }
            }
        }

        private string FindPython(string emmePath)
        {
            foreach(var dir in Directory.GetDirectories(emmePath))
            {
                var localName = Path.GetFileName(dir);
                if(localName.StartsWith("Python"))
                {
                    var remainder = localName.Substring("Python".Length);
                    if(remainder.Length > 0 && char.IsDigit(remainder[0]))
                    {
                        return localName;
                    }
                }
            }
            throw new XTMFRuntimeException("We were unable to find a version of python inside of EMME!");
        }
    }
}