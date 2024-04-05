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
using System.Xml;

namespace XTMF.Run;

sealed class XTMFRunRemoteClient : XTMFRun
{
    private Thread _RunThread;

    private bool _Overwrite = false;

    private ModelSystemStructure _Root;

    private readonly List<ILinkedParameter> _linkedParameters;

    public XTMFRunRemoteClient(Configuration configuration, string runName, string runDirectory, string modelSystemString)
        : base(runName, runDirectory, configuration)
    {
        using var memStream = new MemoryStream();
        try
        {
            Project temp = new(Path.GetFileName(runDirectory), configuration, true);
            ((ProjectRepository)(configuration.ProjectRepository)).SetActiveProject(temp);
            temp.ExternallySaved += (o, e) =>
            {
                SendProjectSaved(null);
            };
            var msAsBytes = Encoding.Unicode.GetBytes(modelSystemString);
            memStream.Write(msAsBytes, 0, msAsBytes.Length);
            memStream.Position = 0;
            var mss = ModelSystemStructure.Load(memStream, configuration);
            memStream.Position = 0;
            _Root = (ModelSystemStructure)mss;
            _linkedParameters = LoadLinkedParameters(_Root, memStream);
            temp.AddModelSystem(_Root, _linkedParameters, String.Empty);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    private List<ILinkedParameter> LoadLinkedParameters(ModelSystemStructure root, Stream stream)
    {
        var ret = new List<ILinkedParameter>();
        string error = null;
        using (XmlReader reader = XmlReader.Create(stream))
        {
            bool skipRead = false;
            while (!reader.EOF && (skipRead || reader.Read()))
            {
                skipRead = false;
                if (reader.NodeType != XmlNodeType.Element) continue;
                switch (reader.LocalName)
                {
                    case "LinkedParameter":
                        {
                            string linkedParameterName = "Unnamed";
                            string valueRepresentation = null;
                            var startingDepth = reader.Depth;
                            while (reader.MoveToNextAttribute())
                            {
                                if (reader.NodeType == XmlNodeType.Attribute)
                                {
                                    if (reader.LocalName == "Name")
                                    {
                                        linkedParameterName = reader.ReadContentAsString();
                                    }
                                    else if (reader.LocalName == "Value")
                                    {
                                        valueRepresentation = reader.ReadContentAsString();
                                    }
                                }
                            }
                            LinkedParameter lp = new(linkedParameterName);
                            lp.SetValue(valueRepresentation, ref error);
                            ret.Add(lp);
                            skipRead = true;
                            while (reader.Read())
                            {
                                if (reader.Depth <= startingDepth)
                                {
                                    break;
                                }
                                if (reader.NodeType != XmlNodeType.Element)
                                {
                                    continue;
                                }
                                if (reader.LocalName == "Reference")
                                {
                                    string variableLink = null;
                                    while (reader.MoveToNextAttribute())
                                    {
                                        if (reader.Name == "Name")
                                        {
                                            variableLink = reader.ReadContentAsString();
                                        }
                                    }
                                    if (variableLink != null)
                                    {
                                        IModuleParameter param = GetParameterFromLink(variableLink, root);
                                        if (param != null)
                                        {
                                            // in any case if there is a type error, just throw it out
                                            lp.Add(param, ref error);
                                        }
                                    }
                                }
                            }
                        }
                        break;
                }
            }
        }
        return ret;
    }

    private IModuleParameter GetParameterFromLink(string variableLink, ModelSystemStructure root)
    {
        // we need to search the space now
        return GetParameterFromLink(ParseLinkedParameterName(variableLink), 0, root);
    }

    private IModuleParameter GetParameterFromLink(string[] variableLink, int index, IModelSystemStructure current)
    {
        if (index == variableLink.Length - 1)
        {
            // search the parameters
            var parameters = current.Parameters;
            foreach (var p in parameters)
            {
                if (p.Name == variableLink[index])
                {
                    return p;
                }
            }
        }
        else
        {
            IList<IModelSystemStructure> descList = current.Children;
            if (descList == null)
            {
                return null;
            }
            if (current.IsCollection)
            {
                if (int.TryParse(variableLink[index], out int collectionIndex))
                {
                    if (collectionIndex >= 0 && collectionIndex < descList.Count)
                    {
                        return GetParameterFromLink(variableLink, index + 1, descList[collectionIndex]);
                    }
                    return null;
                }
            }
            else
            {
                foreach (var sub in descList)
                {
                    if (sub.ParentFieldName == variableLink[index])
                    {
                        return GetParameterFromLink(variableLink, index + 1, sub);
                    }
                }
            }
        }
        return null;
    }

    private static string[] ParseLinkedParameterName(string variableLink)
    {
        List<string> ret = [];
        bool escape = false;
        var length = variableLink.Length;
        StringBuilder builder = new(length);
        for (int i = 0; i < length; i++)
        {
            var c = variableLink[i];
            // check to see if we need to add in the escape
            if (escape & c != '.')
            {
                builder.Append('\\');
            }
            // check to see if we need to move onto the next part
            if (escape == false & c == '.')
            {
                ret.Add(builder.ToString());
                builder.Clear();
                escape = false;
            }
            else if (c != '\\')
            {
                builder.Append(c);
                escape = false;
            }
            else
            {
                escape = true;
            }
        }
        if (escape)
        {
            builder.Append('\\');
        }
        ret.Add(builder.ToString());
        return ret.ToArray();
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
        if (_Root == null)
        {
            InvokeRuntimeValidationError(
            [
                new ErrorWithPath(null, "Model System Root not found", "The model system was not processed properly and then run!")
            ]);
            return;
        }
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
                string error = null;
                ModelSystem.Save(Path.Combine(RunDirectory, "RunParameters.xml"), _Root, string.Empty, _linkedParameters, Configuration, ref error);
                ModelSystem.SaveQuickParameters(Path.Combine(RunDirectory, "QuickParameters.xml"), _Root);
                ModelSystemStructureModelRoot = new ModelSystemStructureModel(null, _Root);
                MST.Start();
                Console.WriteLine("Model run complete!");
                DisposeModelSystem(ModelSystemStructureModelRoot);
                InvokeRunCompleted();
            }
            catch (ThreadAbortException)
            {
                // This is fine just continue
            }
            catch (Exception e)
            {
                DisposeModelSystem(ModelSystemStructureModelRoot);
                GetInnermostError(ref e);
                List<int> path = null;
                string moduleName = null;
                if (e is XTMFRuntimeException runtimeError)
                {
                    if (runtimeError.Module != null)
                    {
                        path = GetModulePath(runtimeError.Module);
                        moduleName = runtimeError.Module.Name;
                    }
                    else
                    {
                        path = null;
                        moduleName = "Null Module";
                    }
                }
                InvokeRuntimeError(new ErrorWithPath(path, e.Message, CombineStackTrace(e), moduleName, e));
            }
        }
    }

    private void DisposeModelSystem(ModelSystemStructureModel module)
    {
        if (module.RealModelSystemStructure.Module is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // ignore errors during dispose
            }
        }

        var children = module.Children;
        if (children is not null)
        {
            foreach (var child in children)
            {
                DisposeModelSystem(child);
            }
        }
    }

    private string CombineStackTrace(Exception runtimeException)
    {
        if (runtimeException.InnerException != null)
        {
            return string.Concat(runtimeException.InnerException, Environment.NewLine, runtimeException.StackTrace);
        }
        else
        {
            return runtimeException.StackTrace;
        }
    }

    private List<int> GetModulePath(IModule module)
    {
        if (module == null) return null;
        List<int> ret = [];
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
            ErrorWithPath error = new();
            if (!_Root.Validate(ref error, []))
            {
                InvokeValidationError(CreateFromSingleError(error));
                return false;
            }
            if (!Project.CreateModule(Configuration, _Root, _Root, [], ref error))
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
            InvokeValidationError(CreateFromSingleError(new ErrorWithPath(null, e.Message, e.StackTrace, exception: e)));
            return false;
        }
        return true;
    }

    private bool ValidateRuntimeModelSystem()
    {
        List<ErrorWithPath> errors = [];
        try
        {
            _Root.Save(Path.GetFullPath("RunParameters.xml"));
            if (!RunTimeValidation([], errors, _Root))
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
            errors.Add(new ErrorWithPath(null, e.Message, e.StackTrace, exception: e));
            InvokeRuntimeValidationError(errors);
            return false;
        }
        return true;
    }

    private void SetupRunDirectory()
    {
        DirectoryInfo runDir = new(RunDirectory);
        if (_Overwrite && runDir.Exists)
        {
            runDir.Delete(true);
        }
        runDir.Create();
        Environment.CurrentDirectory = RunDirectory;
    }

    public override void TerminateRun()
    {
        // TODO: Replace with a new system to replace Thread.Abort
    }

    public override void Wait() => _RunThread?.Join();
}
