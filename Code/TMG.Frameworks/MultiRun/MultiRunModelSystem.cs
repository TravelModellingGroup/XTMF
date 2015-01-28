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


        public float Progress { get; set; }

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
            return LoadChildFromXTMF(ref error);
        }

        private bool FindUs(IModelSystemStructure mst, ref IModelSystemStructure modelSystemStructure)
        {
            if(mst.Module == this)
            {
                modelSystemStructure = mst;
                return true;
            }
            if(mst.Children != null)
            {
                foreach(var child in mst.Children)
                {
                    if(FindUs(child, ref modelSystemStructure))
                    {
                        return true;
                    }
                }
            }
            // Then we didn't find it in this tree
            return false;
        }

        private bool LoadChildFromXTMF(ref string error)
        {
            IModelSystemStructure ourStructure = null;
            foreach(var mst in Config.ProjectRepository.ActiveProject.ModelSystemStructure)
            {
                if(FindUs(mst, ref ourStructure))
                {
                    foreach(var child in ourStructure.Children)
                    {
                        if(child.ParentFieldName == "Child")
                        {
                            ChildStructure = child;
                            break;
                        }
                    }
                    break;
                }
            }
            if(ChildStructure == null)
            {
                error = "In '" + this.Name + "' we were unable to find the Client Model System!";
                return false;
            }
            return true;
        }

        private string RunName = "Initializing";

        public void Start()
        {
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
            InitializeCommands();
            XmlDocument doc = new XmlDocument();
            doc.Load(BatchRunFile);
            var root = doc.FirstChild;
            if(root.HasChildNodes)
            {
                foreach(XmlNode run in root.ChildNodes)
                {
                    if(run.LocalName.Equals("Run", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string name = "Unnamed Run";
                        SetupRun(run, ref name);
                        yield return name;
                    }
                }
            }
        }

        Dictionary<string, Action<XmlNode>> BatchCommands = new Dictionary<string, Action<XmlNode>>();
        private IModelSystemStructure ChildStructure;

        private void InitializeCommands()
        {
            BatchCommands.Clear();
            // Add all of the available commands to our dictionary for the execution engine
            BatchCommands.Add("copyfiles", CopyFiles);
            BatchCommands.Add("changeparameter", ChangeParameter);
            BatchCommands.Add("deletefiles", DeleteFiles);
            BatchCommands.Add("writetofile", WriteToFile);
        }

        private void CopyFiles(XmlNode command)
        {
            throw new NotImplementedException("Copy Files Coming Soon...");
        }

        private void ChangeParameter(XmlNode command)
        {
            throw new NotImplementedException("Change Parameter Coming Soon...");
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
            var path = command.Attributes["Path"];
            if(path == null)
            {
                throw new XTMFRuntimeException("There is a Delete file command that does not define a path to delete!");
            }
            var filePath = path.InnerText;
            if(IsDirectory(filePath))
            {
                Directory.Delete(filePath, IsRecursiveDelete(command));
            }
            else
            {
                File.Delete(filePath);
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
            throw new NotImplementedException("WriteToFile Coming Soon...");
        }

        private void SetupRun(XmlNode run, ref string name)
        {
            var runName = run.Attributes["Name"];
            if(runName != null)
            {
                name = runName.InnerText;
            }
            if(run.HasChildNodes)
            {
                foreach(XmlNode runChild in run.ChildNodes)
                {
                    var commandName = runChild.LocalName;
                    Action<XmlNode> command;
                    if(!BatchCommands.TryGetValue(commandName.ToLowerInvariant(), out command))
                    {
                        throw new XTMFRuntimeException("We are unable to find a command named '" + commandName + "' for batch processing.  Please check your batch file!");
                    }
                    command.Invoke(runChild);
                }
            }
        }
    }

}
