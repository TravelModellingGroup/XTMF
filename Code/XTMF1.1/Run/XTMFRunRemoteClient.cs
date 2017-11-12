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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XTMF.Run
{
    sealed class XTMFRunRemoteClient : XTMFRun
    {
        private Thread _RunThread;

        private bool _Overwrite = false;

        private ModelSystemStructure _Root;

        public XTMFRunRemoteClient(Configuration configuration, string runName, string runDirectory, string modelSystemString)
            : base(runName, runDirectory, configuration)
        {
            using (var memStream = new MemoryStream())
            {
                try
                {
                    Project temp = new Project(Path.GetFileName(runDirectory), configuration, true);
                    ((ProjectRepository)(configuration.ProjectRepository)).SetActiveProject(temp);
                    var msAsBytes = Encoding.Unicode.GetBytes(modelSystemString);
                    memStream.Write(msAsBytes, 0, msAsBytes.Length);
                    memStream.Position = 0;

                    var mss = ModelSystemStructure.Load(memStream, configuration);
                    _Root = (ModelSystemStructure)mss;
                    temp.ModelSystemStructure.Add(_Root);
                    temp.ModelSystemDescriptions.Add(String.Empty);
                    temp.LinkedParameters.Add(new List<ILinkedParameter>());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        public override bool RunsRemotely => true;

        public override bool DeepExitRequest()
        {
            bool Exit(IModelSystemStructure current)
            {
                return current.Children.Aggregate(false, (acc, m) => acc | Exit(m))
                    | (current.Module is IModelSystemTemplate mst && mst.ExitRequest());
            }
            var root = ModelSystemStructureModelRoot;
            if (root != null)
            {
                return Exit(root.RealModelSystemStructure);
            }
            return false;
        }

        public override bool ExitRequest() => DeepExitRequest();

        public override Tuple<byte, byte, byte> PollColour() => MST?.ProgressColour ?? new Tuple<byte, byte, byte>(50, 150, 50);

        public override float PollProgress() => MST?.Progress ?? 1.0f;

        public override string PollStatusMessage() => MST?.ToString() ?? "No MST";

        public override void Start()
        {
            (_RunThread = new Thread(() => Run())).Start();
        }

        private void Run()
        {
            if (ValidateModelSystem())
            {
                try
                {
                    SetupRunDirectory();
                    if (!ValidateRuntimeModelSystem())
                    {
                        // The call will already signal the errors.
                        return;
                    }
                    _Root.Save(Path.Combine(RunDirectory, "RunParameters.xml"));
                    MST.Start();
                    InvokeRunCompleted();
                }
                catch (ThreadAbortException)
                {
                    // This is fine just continue
                }
                catch (Exception e)
                {
                    GetInnermostError(ref e);
                    List<int> path = null;
                    if (e is XTMFRuntimeException runtimeError)
                    {
                        path = GetModulePath(runtimeError.Module);
                    }
                    InvokeRuntimeError(new ErrorWithPath(path, e.Message, e.StackTrace));
                }
            }
        }

        private List<int> GetModulePath(IModule module)
        {
            if (module == null) return null;
            List<int> ret = new List<int>();
            bool Explore(IModelSystemStructure current, List<int> path, IModule lookingFor)
            {
                if (current.Module == lookingFor)
                {
                    return true;
                }
                var children = current.Children;
                if (children != null)
                {
                    path.Add(0);
                    foreach (var child in children)
                    {
                        if (Explore(child, path, lookingFor))
                        {
                            return true;
                        }
                        path[path.Count - 1] += 1;
                    }
                    path.RemoveAt(path.Count - 1);
                }
                return false;
            }
            return Explore(_Root, ret, module) ? ret : null;
        }

        private bool ValidateModelSystem()
        {
            try
            {
                ErrorWithPath error = new ErrorWithPath();
                if (!_Root.Validate(ref error, new List<int>()))
                {
                    InvokeValidationError(CreateFromSingleError(error));
                    return false;
                }
                if (!Project.CreateModule(Configuration, _Root, _Root, new List<int>(), ref error))
                {
                    InvokeValidationError(CreateFromSingleError(error));
                    return false;
                }
                MST = _Root.Module as IModelSystemTemplate;
                if (MST == null)
                {
                    InvokeValidationError(CreateFromSingleError(new ErrorWithPath(null, "Unable to generate MST!")));
                    return false;
                }
            }
            catch (Exception e)
            {
                InvokeValidationError(CreateFromSingleError(new ErrorWithPath(null, e.Message, e.StackTrace)));
                return false;
            }
            return true;
        }

        private bool ValidateRuntimeModelSystem()
        {
            List<ErrorWithPath> errors = new List<ErrorWithPath>();
            try
            {
                _Root.Save(Path.GetFullPath("RunParameters.xml"));
                if (!RunTimeValidation(new List<int>(), errors, _Root))
                {
                    InvokeRuntimeValidationError(errors);
                    return false;
                }
                else
                {
                    SetStatusToRunning();
                }
            }
            catch (Exception e)
            {
                errors.Add(new ErrorWithPath(null, e.Message, e.StackTrace));
                InvokeRuntimeValidationError(errors);
                return false;
            }
            return true;
        }

        private void SetupRunDirectory()
        {
            DirectoryInfo runDir = new DirectoryInfo(RunDirectory);
            if (_Overwrite && runDir.Exists)
            {
                runDir.Delete(true);
            }
            runDir.Create();
            Environment.CurrentDirectory = RunDirectory;
        }

        public override void TerminateRun()
        {
            try
            {
                _RunThread?.Abort();
            }
            catch
            { }
        }

        public override void Wait() => _RunThread?.Join();
    }
}
