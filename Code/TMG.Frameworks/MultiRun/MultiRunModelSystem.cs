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

namespace TMG.Frameworks.MultiRun
{

    public class MultiRunModelSystem : IModelSystemTemplate
    {
        [RunParameter("Input Directory", "../../Input", "The input directory to use for this model system template.")]
        public string InputBaseDirectory { get; set; }

        [SubModelInformation(Required = true, Description = "The child model system to chain the execution of.")]
        public IModelSystemTemplate Child;

        public string Name { get; set; }

        public string OutputBaseDirectory { get; set; }


        public float Progress
        {
            get
            {
                return CurrentProgress();
            }
        }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private bool Exit = false;

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
            return LoadChildFromXTMF(ref error);
        }

        private bool LoadChildFromXTMF(ref string error)
        {
            IModelSystemStructure ourStructure = null;
            if(ModelSystemReflection.FindModuleStructure(Config, this, ref ourStructure))
            {
                foreach(var child in ourStructure.Children)
                {
                    if(child.ParentFieldName == "Child")
                    {
                        ChildStructure = child;
                        break;
                    }
                }
            }
            if(ChildStructure == null)
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
            foreach(var runName in ExecuteRuns())
            {
                RunName = runName;
                Child.Start();
                if(Exit) return;
            }
        }

        public override string ToString()
        {
            return RunName + " " + Child.ToString();
        }

        private IConfiguration Config;

        public MultiRunModelSystem(IConfiguration config)
        {
            Config = config;
        }

        private IEnumerable<string> ExecuteRuns()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(BatchRunFile);
            var root = doc.FirstChild;
            if(root.HasChildNodes)
            {
                foreach(XmlNode topLevelCommand in root.ChildNodes)
                {
                    if(topLevelCommand.LocalName.Equals("Run", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string name = "Unnamed Run";
                        SetupRun(topLevelCommand, ref name);
                        yield return name;
                    }
                    else
                    {
                        if(topLevelCommand.NodeType != XmlNodeType.Comment)
                        {
                            var commandName = topLevelCommand.LocalName;
                            Action<XmlNode> command;
                            if(!BatchCommands.TryGetValue(commandName.ToLowerInvariant(), out command))
                            {
                                throw new XTMFRuntimeException("We are unable to find a command named '" + commandName
                                    + "' for batch processing.  Please check your batch file!\r\n" + topLevelCommand.OuterXml);
                            }
                            command.Invoke(topLevelCommand);
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
            TryAddBatchCommand("delete", DeleteFiles, true);
            TryAddBatchCommand("write", WriteToFile, true);
            TryAddBatchCommand("unload", UnloadResource, true);
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
            if(!overwrite && BatchCommands.ContainsKey(name))
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
            var at = node.Attributes[attribute];
            if(at == null)
            {
                throw new XTMFRuntimeException(errorMessage + "\r\n" + node.OuterXml);
            }
            return at.InnerText;
        }

        private void CopyFiles(XmlNode command)
        {
            var origin = GetAttributeOrError(command, "Origin", "There was a copy command without an 'Origin' attribute!");
            var destination = GetAttributeOrError(command, "Destination", "There was a copy command without an 'Destination' attribute!");
            bool move = false;
            var moveAt = command.Attributes["Move"];
            if(moveAt != null)
            {
                bool result = false;
                if(bool.TryParse(moveAt.InnerText, out result))
                {
                    move = result;
                }
            }
            Copy(origin, destination, move);
        }

        public static bool Copy(string origin, string destination, bool move)
        {
            try
            {
                if(Directory.Exists(origin))
                {
                    // check to see if we don't need to make a copy
                    if(move)
                    {
                        if(Directory.Exists(destination))
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
                    if(move)
                    {
                        if(File.Exists(destination))
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
            catch (IOException)
            {
                return false;
            }
            return true;
        }

        private static void DirectoryCopy(string sourceDirectory, string destinationDirectory)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirectory);
            DirectoryInfo[] dirs = dir.GetDirectories();
            if(!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirectory);
            }

            // If the destination directory doesn't exist, create it.
            if(!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach(FileInfo file in files)
            {
                string temppath = Path.Combine(destinationDirectory, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            foreach(DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destinationDirectory, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        private void ChangeParameter(XmlNode command)
        {
            string value = GetAttributeOrError(command, "Value", "The attribute 'Value' was not found!");
            if(command.HasChildNodes)
            {
                foreach(XmlNode child in command)
                {
                    if(child.LocalName.ToLowerInvariant() == "parameter")
                    {
                        string parameterName = GetAttributeOrError(child, "ParameterPath", "The attribute 'ParameterPath' was not found!");
                        ModelSystemReflection.AssignValue(ChildStructure, parameterName, value);
                    }
                }
            }
            else
            {
                string parameterName = GetAttributeOrError(command, "ParameterPath", "The attribute 'ParameterPath' was not found!");
                ModelSystemReflection.AssignValue(ChildStructure, parameterName, value);
            }
        }

        private void UnloadResource(XmlNode command)
        {
            string path = GetAttributeOrError(command, "Path", "We were unable to find an attribute called 'Path'!");
            IModelSystemStructure referencedModule = null;
            if(!ModelSystemReflection.GetModelSystemStructureFromPath(ChildStructure, path, ref referencedModule))
            {
                throw new XTMFRuntimeException("In '" + Name + "' we were unable to find the child with the path '" + path + "'!");
            }
            var mod = referencedModule.Module as IResource;
            if(mod == null)
            {
                throw new XTMFRuntimeException("In '" + Name + "' the referenced module '" + path + "' is not a resource! Only resources can be unloaded!");
            }
            mod.ReleaseResource();
        }



        private static bool IsDirectory(string path)
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        }

        private void DeleteFiles(XmlNode command)
        {
            if(command.HasChildNodes)
            {
                foreach(XmlNode child in command.ChildNodes)
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
            for(int i = 0; i < 10; i++)
            {
                try
                {
                    if(IsDirectory(filePath))
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
            var attribute = command.Attributes["Recursive"];
            if(attribute != null)
            {
                bool rec = true;
                if(bool.TryParse(attribute.InnerText, out rec))
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

        private string OriginalDirectory = null;

        private void SetupRun(XmlNode run, ref string name)
        {
            var runName = run.Attributes["Name"];
            if(runName != null)
            {
                name = runName.InnerText;
            }
            var saveAndRunAs = run.Attributes["RunAs"];
            if(saveAndRunAs != null)
            {
                if(OriginalDirectory == null)
                {
                    OriginalDirectory = Directory.GetCurrentDirectory();
                }
                else
                {
                    Directory.SetCurrentDirectory(OriginalDirectory);
                }
                var newDirectoryName = saveAndRunAs.InnerText;
                DirectoryInfo info = new DirectoryInfo(newDirectoryName);
                if(!info.Exists)
                {
                    info.Create();
                }
                Directory.SetCurrentDirectory(newDirectoryName);
            }
            if(run.HasChildNodes)
            {
                foreach(XmlNode runChild in run.ChildNodes)
                {
                    if(runChild.NodeType != XmlNodeType.Comment)
                    {
                        var commandName = runChild.LocalName;
                        Action<XmlNode> command;
                        if(!BatchCommands.TryGetValue(commandName.ToLowerInvariant(), out command))
                        {
                            throw new XTMFRuntimeException("We are unable to find a command named '" + commandName + "' for batch processing.  Please check your batch file!\r\n" + runChild.OuterXml);
                        }
                        command.Invoke(runChild);
                    }
                }
            }
            if(saveAndRunAs != null)
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
