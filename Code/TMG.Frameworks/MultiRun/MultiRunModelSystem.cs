/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Xml;
using TMG.Input;
using XTMF;
using TMG.Functions;
using System.Threading;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.Frameworks.MultiRun
{
    [ModuleInformation(
        DocURL = "http://tmg.utoronto.ca/doc/1.6/xtmf/frameworks/multirun.html",
        Description =
@"
TMG’s Multi-run framework is designed to aid in the automation of running model systems where each iteration would require setup.
The framework itself is extendable as modules are able to add their own additional commands to the language used in the configuration file.

For specification about the language, and extensibility please consult the TMG Frameworks Users Guide.
"
        )]
    public class MultiRunModelSystem : IModelSystemTemplate
    {
        [RunParameter("Input Directory", "../../Input", "The input directory to use for this model system template.")]
        public string InputBaseDirectory { get; set; }

        [SubModelInformation(Required = true, Description = "The child model system to chain the execution of.")]
        public IModelSystemTemplate Child;

        [RunParameter("Continue After Error", false, "Should we continue onto the next run if a run ends with an exception?")]
        public bool ContinueAfterError;

        public string Name { get; set; }

        public string OutputBaseDirectory { get; set; }

        [SubModelInformation(Required = false, Description = "The location to store a copy of the batch file to.")]
        public FileLocation CopyBatchFileTo;


        public float Progress
        {
            get
            {
                return CurrentProgress();
            }
        }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private bool Exit;

        [SubModelInformation(Required = true, Description = "The file containing the instructions for this batch run.")]
        public FileLocation BatchRunFile;

        public bool ExitRequest()
        {
            return (Exit = Child.ExitRequest());
        }

        public bool RuntimeValidation(ref string error)
        {
            // we need to initialize the commands here so that our children can add new batch commands during their RuntimeValidation
            InitializeCommands();
            return LoadChildFromXtmf(ref error);
        }

        private bool LoadChildFromXtmf(ref string error)
        {
            IModelSystemStructure ourStructure = null;
            if (ModelSystemReflection.FindModuleStructure(Config, this, ref ourStructure))
            {
                foreach (var child in ourStructure.Children)
                {
                    if (child.ParentFieldName == "Child")
                    {
                        ChildStructure = child;
                        break;
                    }
                }
            }
            if (ChildStructure == null)
            {
                error = "In '" + Name + "' we were unable to find the Client Model System!";
                return false;
            }
            return true;
        }

        private string RunName = "Initializing";
        private Func<float> CurrentProgress = () => 0f;

        public void Start()
        {
            CurrentProgress = () => Child.Progress;
            if (CopyBatchFileTo != null)
            {
                try
                {
                    File.Copy(BatchRunFile.GetFilePath(), CopyBatchFileTo.GetFilePath(), true);
                }
                catch (IOException e)
                {
                    throw new XTMFRuntimeException(this, e, $"Unable to copy the multi-run batch file {CopyBatchFileTo.GetFilePath()}. {e.Message}");
                }
            }
            foreach (var runName in ExecuteRuns())
            {
                try
                {
                    RunName = runName;
                    Child.Start();
                }
                catch (ThreadAbortException)
                {
                    // in any case we continue to exit on a thread abort exception
                    throw;
                }
                catch (Exception e)
                {
                    if (!ContinueAfterError)
                    {
                        throw;
                    }
                    SaveException(e);
                }
                if (Exit) return;
            }
        }

        private void SaveException(Exception e)
        {
            using (var writer = new StreamWriter("XTMF.ErrorLog.txt", true))
            {
                var realExeption = GetTopRootException(e);
                writer.WriteLine(realExeption.Message);
                writer.WriteLine();
                writer.WriteLine(realExeption.StackTrace);
            }
        }

        private static Exception GetTopRootException(Exception value)
        {
            if (value == null) return null;
            if (value is AggregateException agg)
            {
                return GetTopRootException(agg.InnerException);
            }
            return value;
        }

        public override string ToString()
        {
            return RunName + " " + Child;
        }

        private IConfiguration Config;

        public MultiRunModelSystem(IConfiguration config)
        {
            Config = config;
        }

        private IEnumerable<string> ExecuteRuns()
        {
            XmlNode root = GetRootOfDocument(BatchRunFile);
            foreach (var run in CommandProcessor(root))
            {
                yield return run;
            }
        }

        private XmlNode GetRootOfDocument(string fileName)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);
            var root = doc.FirstChild;
            return root;
        }

        private IEnumerable<string> CommandProcessor(XmlNode root)
        {
            if (root.HasChildNodes)
            {
                foreach (XmlNode topLevelCommand in root.ChildNodes)
                {
                    if (topLevelCommand.LocalName.Equals("Run", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string name = "Unnamed Run";
                        SetupRun(topLevelCommand, ref name);
                        yield return name;
                    }
                    else
                    {
                        if (topLevelCommand.NodeType != XmlNodeType.Comment)
                        {
                            var commandName = topLevelCommand.LocalName;
                            if (!BatchCommands.TryGetValue(commandName.ToLowerInvariant(), out Action<XmlNode> command))
                            {
                                throw new XTMFRuntimeException(this, "We are unable to find a command named '" + commandName
                                    + "' for batch processing.  Please check your batch file!\r\n" + topLevelCommand.OuterXml);
                            }
                            var initialCount = ExecutionStack.Count;
                            command.Invoke(topLevelCommand);
                            if (initialCount < ExecutionStack.Count)
                            {
                                var current = ExecutionStack.Pop();
                                foreach (var run in CommandProcessor(current))
                                {
                                    yield return run;
                                }
                            }
                        }
                    }
                }
            }
        }

        Dictionary<string, Action<XmlNode>> BatchCommands = new Dictionary<string, Action<XmlNode>>();
        private IModelSystemStructure ChildStructure;

        private void InitializeCommands()
        {
            BatchCommands.Clear();
            // Add all of the basic commands to our dictionary for the execution engine
            TryAddBatchCommand("copy", CopyFiles, true);
            TryAddBatchCommand("changeparameter", ChangeParameter, true);
            TryAddBatchCommand("changelinkedparameter", ChangeLinkedParameter, true);
            TryAddBatchCommand("delete", DeleteFiles, true);
            TryAddBatchCommand("write", WriteToFile, true);
            TryAddBatchCommand("unload", UnloadResource, true);
            TryAddBatchCommand("template", Template, true);
            TryAddBatchCommand("executetemplate", ExecuteTemplate, true);
            TryAddBatchCommand("import", ImportMultiRunFile, true);
            TryAddBatchCommand("define", DefineVariable, true);
            TryAddBatchCommand("if", IfStatement, true);
            TryAddBatchCommand("fail", Fail, true);
        }

        /// <summary>
        /// Tries to add a batch command, this will fail if there is already a command with the same name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="command"></param>
        /// <param name="overwrite">If true, this command will overwrite any previous command with the same name.</param>
        /// <returns></returns>
        public bool TryAddBatchCommand(string name, Action<XmlNode> command, bool overwrite)
        {
            name = name.ToLowerInvariant();
            if (!overwrite && BatchCommands.ContainsKey(name))
            {
                return false;
            }
            BatchCommands[name] = command;
            return true;
        }

        /// <summary>
        /// Gets the attribute from the xmlnode or throws an XTMFRuntimeException
        /// </summary>
        /// <param name="node">The node the is being processed</param>
        /// <param name="attribute">The name as the attribute to lookup</param>
        /// <param name="errorMessage">The error message to give if it was not found.</param>
        /// <returns>The value of the attribute.</returns>
        public string GetAttributeOrError(XmlNode node, string attribute, string errorMessage)
        {
            var at = node.Attributes?[attribute];
            if (at == null)
            {
                throw new XTMFRuntimeException(this, errorMessage + "\r\n" + node.OuterXml);
            }
            return at.InnerText;
        }

        private void CopyFiles(XmlNode command)
        {
            var origin = GetAttributeOrError(command, "Origin", "There was a copy command without an 'Origin' attribute!");
            var destination = GetAttributeOrError(command, "Destination", "There was a copy command without an 'Destination' attribute!");
            bool move = false;
            var moveAt = command.Attributes?["Move"];
            if (moveAt != null)
            {
                if (bool.TryParse(moveAt.InnerText, out bool result))
                {
                    move = result;
                }
            }
            Copy(origin, destination, move);
        }

        public bool Copy(string origin, string destination, bool move)
        {
            try
            {
                if (Directory.Exists(origin))
                {
                    // check to see if we don't need to make a copy
                    if (move)
                    {
                        if (Directory.Exists(destination))
                        {
                            Directory.Delete(destination);
                        }
                        Directory.Move(origin, destination);
                    }
                    else
                    {
                        DirectoryCopy(origin, destination);
                    }
                }
                else
                {
                    if (move)
                    {
                        if (File.Exists(destination))
                        {
                            File.Delete(destination);
                        }
                        File.Move(origin, destination);
                    }
                    else
                    {
                        File.Copy(origin, destination, true);
                    }
                }
            }
            catch (IOException e)
            {
                throw new XTMFRuntimeException(this, "Failed to copy '" + origin + "' to '" + destination + "'!\r\nThe CWD is" + Directory.GetCurrentDirectory()
                    + "\r\n" + e.Message);
            }
            return true;
        }

        private static void DirectoryCopy(string sourceDirectory, string destinationDirectory)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirectory);
            DirectoryInfo[] dirs = dir.GetDirectories();
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirectory);
            }

            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destinationDirectory, file.Name);
                file.CopyTo(temppath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destinationDirectory, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        private void ChangeParameter(XmlNode command)
        {
            string value = GetAttributeOrError(command, "Value", "The attribute 'Value' was not found!");
            string error = null;
            if (command.HasChildNodes)
            {
                foreach (XmlNode child in command)
                {
                    if (child.LocalName.ToLowerInvariant() == "parameter")
                    {
                        string parameterName = GetAttributeOrError(child, "ParameterPath", "The attribute 'ParameterPath' was not found!");
                        if (!ModelSystemReflection.AssignValue(Config, ChildStructure, parameterName, value, ref error))
                        {
                            throw new XTMFRuntimeException(this, $"In '{Name}' we were unable assign a variable.\r\n{error}");
                        }
                    }
                }
            }
            else
            {
                string parameterName = GetAttributeOrError(command, "ParameterPath", "The attribute 'ParameterPath' was not found!");
                if (!ModelSystemReflection.AssignValue(Config, ChildStructure, parameterName, value, ref error))
                {
                    throw new XTMFRuntimeException(this, $"In '{Name}' we were unable assign a variable.\r\n{error}");
                }
            }
        }

        private void ChangeLinkedParameter(XmlNode command)
        {
            string name = GetAttributeOrError(command, "Name", "The attribute 'Name' was not found!");
            string value = GetAttributeOrError(command, "Value", "The attribute 'Value' was not found!");
            var project = Config.ProjectRepository.ActiveProject;
            var modelSystemIndex = project.IndexOf(ModelSystemReflection.BuildModelStructureChain(Config, this)[0]);
            var ourLinkedParameters = project.LinkedParameters[modelSystemIndex];
            bool any = false;
            foreach (var lp in ourLinkedParameters)
            {
                if (lp.Name == name)
                {
                    any = true;
                    foreach (var parameter in lp.Parameters)
                    {
                        ModelSystemReflection.AssignValue(Config, parameter, value);
                    }
                }
            }
            if (!any)
            {
                throw new XTMFRuntimeException(this, "In '" + Name + "' a linked parameter '" + name + "' was not found in order to assign it the value of '" + value + "'.");
            }
        }

        private void Fail(XmlNode command)
        {
            string message = GetAttributeOrError(command, "Message", "The attribute 'Message' was not found!");
            throw new XTMFRuntimeException(this, message);
        }

        private sealed class MultirunTemplate
        {
            internal string Name;
            internal XmlNode Node;
            string[] Parameters;

            public MultirunTemplate(string name, XmlNode node, string[] parameters)
            {
                Name = name;
                Node = node;
                Parameters = parameters;
            }

            private bool ValidateParameters(KeyValuePair<string, string>[] parameterAssignment, ref string error)
            {
                // validate the parameters
                if (parameterAssignment.Length != Parameters.Length)
                {
                    error = Name + " was executed with the wrong number of parameters! The parameters are [" + string.Join(" ", Parameters) + "]!";
                }
                for (int i = 0; i < parameterAssignment.Length; i++)
                {
                    if (Array.IndexOf(Parameters, parameterAssignment[i].Key) < 0)
                    {
                        error = Name + " was executed but no parameter was defined as '" + parameterAssignment[i].Key + "'";
                        return false;
                    }
                }
                return true;
            }

            internal bool PrepareForExecution(KeyValuePair<string, string>[] parameters, out MultirunTemplate toExecute, ref string error)
            {
                if (!ValidateParameters(parameters, ref error))
                {
                    toExecute = null;
                    return false;
                }
                var newNode = Node.CloneNode(true);
                if (!ReplaceStrings(newNode, parameters, ref error))
                {
                    toExecute = null;
                    return false;
                }
                toExecute = new MultirunTemplate(Name, newNode, Parameters);
                return true;
            }

            private static bool ReplaceStrings(XmlNode currentNode, KeyValuePair<string, string>[] parameters, ref string error)
            {
                // check each attribute and replace as needed
                var attributes = currentNode.Attributes;
                if (attributes != null)
                {
                    foreach (XmlAttribute at in attributes)
                    {
                        if (!ReplaceStrings(at, parameters, ref error))
                        {
                            return false;
                        }
                    }
                }
                // now look at all of the children
                if (currentNode.HasChildNodes)
                {
                    foreach (XmlNode child in currentNode.ChildNodes)
                    {
                        if (!ReplaceStrings(child, parameters, ref error))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            private static bool ReplaceStrings(XmlAttribute at, KeyValuePair<string, string>[] parameters, ref string error)
            {
                var originalString = at.Value;
                // only execute if the escape character is found
                int firstIndex = originalString.IndexOf('%');
                if (firstIndex >= 0)
                {
                    // initialize the builder up until we found the first escape character
                    StringBuilder builder = new StringBuilder(originalString.Length);
                    StringBuilder nameBuilder = new StringBuilder();
                    builder.Append(originalString, 0, firstIndex);
                    // invoking string length is quite slow, so since it isn't changing just store it
                    var length = originalString.Length;
                    for (int i = firstIndex; i < length; i++)
                    {
                        if (originalString[i] == '%')
                        {
                            if (i + 1 < length)
                            {
                                if (originalString[i + 1] == '%')
                                {
                                    builder.Append('%');
                                    // skip the next %
                                    i = i + 1;
                                }
                                else
                                {
                                    // in this case we need to gather the variable name and then assign the value
                                    int j = i + 1;
                                    for (; j < length; j++)
                                    {
                                        var c = originalString[j];
                                        // now we are at the end of the variable name
                                        if (c == '%')
                                        {
                                            break;
                                        }
                                        nameBuilder.Append(c);
                                    }
                                    // check to make sure we actually finished
                                    if (j == length)
                                    {
                                        error = "No closing % was given in order to use a parameter!";
                                        return false;
                                    }
                                    // move our main loop ahead to the end of the variable
                                    i = j;
                                    var parameterName = nameBuilder.ToString();
                                    var parameter = parameters.FirstOrDefault(p => p.Key == parameterName);
                                    // if the parameter does not exist
                                    if (parameter.Key == null)
                                    {
                                        error = "A parameter with the name '" + parameterName + "' was not found!";
                                        return false;
                                    }
                                    // now that we have our variable, insert its value in our place
                                    builder.Append(parameter.Value);
                                    nameBuilder.Clear();
                                }
                            }
                            else
                            {
                                builder.Append('%');
                            }
                        }
                        else
                        {
                            builder.Append(originalString[i]);
                        }
                    }
                    at.Value = builder.ToString();
                }
                return true;
            }
        }

        Stack<XmlNode> ExecutionStack = new Stack<XmlNode>();

        Dictionary<string, MultirunTemplate> Templates = new Dictionary<string, MultirunTemplate>();

        Dictionary<string, float> Variables = new Dictionary<string, float>();

        private void Template(XmlNode command)
        {
            var name = GetAttributeOrError(command, "Name", "The template was not given a name!\r\n" + command.OuterXml);
            var parameterAttribute = GetAttributeOrError(command, "Parameters", "The template was not given any parameters!\r\n" + command.OuterXml);
            // gather the parameters semi-colon separated
            var parameters = parameterAttribute.Split(';');
            for (int i = 0; i < parameters.Length; i++)
            {
                parameters[i] = parameters[i].Trim();
            }
            // this will allow overwriting templates
            Templates[name] = new MultirunTemplate(name, command.CloneNode(true), parameters);
        }

        private void ExecuteTemplate(XmlNode command)
        {
            var name = GetAttributeOrError(command, "Name", "The template's name was not given!\r\n" + command.OuterXml);
            if (!Templates.TryGetValue(name, out MultirunTemplate template))
            {
                // then no template with this name exists
                throw new XTMFRuntimeException(this, "No template with the name '" + name + "' exists!");
            }
            KeyValuePair<string, string>[] parameters = GetParameters(command.Attributes);
            string error = null;
            if (!template.PrepareForExecution(parameters, out MultirunTemplate toExecute, ref error))
            {
                throw new XTMFRuntimeException(this, "Unable to execute template\n\r" + error + "\r\n" + template.Node.OuterXml);
            }
            ExecutionStack.Push(toExecute.Node);
        }

        private void DefineVariable(XmlNode command)
        {
            var name = GetAttributeOrError(command, "Name", "The name of the variable was not given!\r\n" + command.OuterXml);
            var value = GetAttributeOrError(command, "Value", "The value to assign the variable was not given!\r\n" + command.OuterXml);
            if (!float.TryParse(value, out float fValue))
            {
                throw new XTMFRuntimeException(this, $"In '{Name}' we were unable to extract the value of {value} into a number!\r\n{command.OuterXml}");
            }
            Variables[name] = fValue;
        }

        private float GetVariableValue(string name, XmlNode command)
        {
            if (Variables.TryGetValue(name, out float value))
            {
                return value;
            }
            if (float.TryParse(name, out value))
            {
                return value;
            }
            throw new XTMFRuntimeException(this, $"In '{Name}' we were unable to get a value from {name} while executing the command:\r\n{command.OuterXml}");
        }

        private void IfStatement(XmlNode command)
        {
            var lhs = GetVariableValue(GetAttributeOrError(command, "LHS", "The LHS was not defined!\r\n" + command.OuterXml), command);
            var comp = GetAttributeOrError(command, "OP", "The comparison OPerator was not defined!\r\n" + command.OuterXml);
            var rhs = GetVariableValue(GetAttributeOrError(command, "RHS", "The RHS was not defined!\r\n" + command.OuterXml), command);
            bool isTrue;
            switch(comp.ToLowerInvariant())
            {
                case "<":
                case "lt":
                    isTrue = lhs < rhs;
                    break;
                case "<=":
                case "lte":
                    isTrue = lhs <= rhs;
                    break;
                case "=":
                case "==":
                case "eq":
                    isTrue = lhs == rhs;
                    break;
                case "!":
                case "!=":
                case "neq":
                    isTrue = lhs != rhs;
                    break;
                case ">":
                case "gt":
                    isTrue = lhs > rhs;
                    break;
                case ">=":
                case "gte":
                    isTrue = lhs >= rhs;
                    break;
                default:
                    throw new XTMFRuntimeException(this, $"In '{Name}' we found an invalid operator while executing the multi-run script.\r\n" + command.OuterXml);
            }
            if (isTrue)
            {
                ExecutionStack.Push(command);
            }
        }

        private void ImportMultiRunFile(XmlNode command)
        {
            var path = GetAttributeOrError(command, "Path", "The multirun's file path was not given!\r\n" + command.OuterXml);
            ExecutionStack.Push(GetRootOfDocument(Path.Combine(Path.GetDirectoryName(BatchRunFile.GetFilePath()), path)));
        }

        private KeyValuePair<string, string>[] GetParameters(XmlAttributeCollection attributes)
        {
            var ret = new List<KeyValuePair<string, string>>();
            foreach (XmlAttribute at in attributes)
            {
                // make sure the attribute isn't the name of the template to execute
                if (at.Name != "Name")
                {
                    ret.Add(new KeyValuePair<string, string>(at.Name, at.InnerText));
                }
            }
            return ret.ToArray();
        }

        private void UnloadResource(XmlNode command)
        {
            string path = GetAttributeOrError(command, "Path", "We were unable to find an attribute called 'Path'!");
            bool recursive = false;
            var attribute = command.Attributes?["Recursive"];
            if (attribute != null)
            {
                if (!bool.TryParse(attribute.InnerText, out recursive))
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' an unload command had a recursive parameter with the value '" + attribute.InnerText + "', which is not true/false!");
                }
            }
            IModelSystemStructure referencedModule = null;
            if (!ModelSystemReflection.GetModelSystemStructureFromPath(ChildStructure, path, ref referencedModule))
            {
                throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to find the child with the path '" + path + "'!");
            }
            if (referencedModule.IsCollection)
            {
                var children = referencedModule.Children;
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        if (recursive)
                        {
                            UnloadRecursively(child);
                        }
                        else
                        {
                            var mod = child.Module;
                            var dataSource = mod as IDataSource;
                            if (mod is IResource res)
                            {
                                res.ReleaseResource();
                            }
                            else if (dataSource != null)
                            {
                                dataSource.UnloadData();
                            }
                        }
                    }
                }
            }
            else
            {
                if (recursive)
                {
                    UnloadRecursively(referencedModule);
                }
                else
                {
                    var dataSource = referencedModule.Module as IDataSource;
                    if (referencedModule.Module is IResource res)
                    {
                        res.ReleaseResource();
                    }
                    else if (dataSource != null)
                    {
                        dataSource.UnloadData();
                    }
                    else
                    {
                        throw new XTMFRuntimeException(this, "In '" + Name + "' the referenced module '" + path + "' is not a resource or data source! Only resources or data sources can be unloaded!");
                    }
                }

            }
        }

        private void UnloadRecursively(IModelSystemStructure child)
        {
            var children = child.Children;
            if (children != null)
            {
                foreach (var subChild in children)
                {
                    UnloadRecursively(subChild);
                }
            }
            var mod = child.Module;
            var dataSource = mod as IDataSource;
            if (mod is IResource res)
            {
                res.ReleaseResource();
            }
            else if (dataSource != null)
            {
                dataSource.UnloadData();
            }
        }

        private static bool IsDirectory(string path)
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        }

        private void DeleteFiles(XmlNode command)
        {
            if (command.HasChildNodes)
            {
                foreach (XmlNode child in command.ChildNodes)
                {
                    DeleteCommand(child);
                }
            }
            else
            {
                // delete one file
                DeleteCommand(command);
            }
        }

        private void DeleteCommand(XmlNode command)
        {
            var filePath = GetAttributeOrError(command, "Path", "There is a Delete file command that does not define a path to delete!");
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (IsDirectory(filePath))
                    {
                        Directory.Delete(filePath, IsRecursiveDelete(command));
                    }
                    else
                    {
                        File.Delete(filePath);
                    }
                    return;
                }
                catch
                {
                    Thread.Sleep(200);
                }
            }
        }

        private bool IsRecursiveDelete(XmlNode command)
        {
            var attribute = command.Attributes?["Recursive"];
            if (attribute != null)
            {
                if (bool.TryParse(attribute.InnerText, out bool rec))
                {
                    return rec;
                }
            }
            return true;
        }

        private void WriteToFile(XmlNode command)
        {
            var path = GetAttributeOrError(command, "Path", "The attribute 'Path' was not defined!");
            using (StreamWriter writer = new StreamWriter(path, true))
            {
                writer.WriteLine(command.InnerText);
            }
        }

        private string OriginalDirectory;

        private void SetupRun(XmlNode run, ref string name)
        {
            var runName = run.Attributes?["Name"];
            if (runName != null)
            {
                name = runName.InnerText;
            }
            var saveAndRunAs = run.Attributes?["RunAs"];
            if (saveAndRunAs != null)
            {
                if (OriginalDirectory == null)
                {
                    OriginalDirectory = Directory.GetCurrentDirectory();
                }
                else
                {
                    Directory.SetCurrentDirectory(OriginalDirectory);
                }
                var newDirectoryName = saveAndRunAs.InnerText;
                DirectoryInfo info = new DirectoryInfo(newDirectoryName);
                if (!info.Exists)
                {
                    info.Create();
                }
                Directory.SetCurrentDirectory(newDirectoryName);
            }
            if (run.HasChildNodes)
            {
                foreach (XmlNode runChild in run.ChildNodes)
                {
                    if (runChild.NodeType != XmlNodeType.Comment)
                    {
                        var commandName = runChild.LocalName;
                        if (!BatchCommands.TryGetValue(commandName.ToLowerInvariant(), out Action<XmlNode> command))
                        {
                            throw new XTMFRuntimeException(this, "We are unable to find a command named '" + commandName + "' for batch processing.  Please check your batch file!\r\n" + runChild.OuterXml);
                        }
                        command.Invoke(runChild);
                    }
                }
            }
            if (saveAndRunAs != null)
            {
                GetRoot().Save("RunParameters.xml");
            }
        }

        private IModelSystemStructure GetRoot()
        {
            return ChildStructure;
        }
    }

}
