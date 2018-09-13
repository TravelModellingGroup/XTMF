using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using XTMF.Interfaces;

namespace XTMF
{
    public sealed partial class Project : IProject
    {
        /// <summary>
        /// Async, load all of the data for this project.
        /// If it doesn't exist then we will create all of the default data.
        /// </summary>
        private bool Load(ref string error)
        {
            if (Path.IsPathRooted(Name))
            {
                _DirectoryLocation = Path.GetDirectoryName(Name);
            }
            else
            {
                _DirectoryLocation = Path.Combine(_Configuration.ProjectDirectory, Name);
            }

            if (_DirectoryLocation == null)
            {
                error = "Invalid directory path!";
                return false;
            }

            _IsLoaded = false;
            string fileLocation = Path.Combine(_DirectoryLocation, "Project.xml");
            if (RemoteProject)
            {
                _IsLoaded = true;
                return true;
            }

            _ProjectModelSystems.Clear();
            if (!Directory.Exists(_DirectoryLocation) || !File.Exists(fileLocation))
            {
                _IsLoaded = true;
                return true;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(fileLocation);
                XmlNode rootNode = doc["Root"];
                if (rootNode != null)
                {
                    var description = rootNode.Attributes?["Description"];
                    if (description != null)
                    {
                        Description = description.InnerText;
                    }

                    var rootChildren = rootNode.ChildNodes;
                    var toLoad = new ProjectModelSystem[rootChildren.Count];
                    Parallel.For(0, rootChildren.Count, i =>
                    {
                        XmlNode child = rootChildren[i];
                        // check for the 3.0 file name
                        switch (child.Name)
                        {
                            case "AdvancedModelSystem":
                                {
                                    if (LoadAdvancedModelSystem(child, i, Guid.NewGuid().ToString(), out var pms))
                                    {
                                        toLoad[i] = pms;
                                    }
                                }
                                break;
                            case "DetachedModelSystem":
                                {
                                    var guid = child.Attributes["GUID"]?.InnerText ?? string.Empty;
                                    if (LoadDetachedModelSystem(_DirectoryLocation, guid, out var pms))
                                    {
                                        toLoad[i] = pms;
                                    }
                                }
                                break;
                        }
                    });
                    _ProjectModelSystems.AddRange(toLoad);
                    _IsLoaded = true;
                    return true;
                }
            }
            catch (Exception e)
            {
                error = String.Concat(e.InnerException?.Message ?? e.Message, "\r\n",
                    e.InnerException?.StackTrace ?? e.StackTrace);
                Console.WriteLine(e.Message + "\r\n" + e.StackTrace);
            }

            return false;
        }

        private bool LoadDetachedModelSystem(string directory, string guid, out ProjectModelSystem pms)
        {
            var msPath = Path.Combine(_DirectoryLocation, "._ModelSystems", $"Project.ms-{guid}.xml");
            if(!File.Exists(msPath))
            {
                msPath = Path.Combine(_DirectoryLocation, $"Project.ms-{guid}.xml");
                if (!File.Exists(msPath))
                {
                    pms = null;
                    return false;
                }
                else
                {
                    var newMsPath = Path.Combine(EnsureModelSystemDirectoryExists(_DirectoryLocation), $"Project.ms-{guid}.xml");
                    File.Move(msPath, newMsPath);
                    msPath = newMsPath;
                }
            }
            XmlDocument msDoc = new XmlDocument();
            msDoc.Load(msPath);
            pms = new ProjectModelSystem()
            {
                GUID = guid
            };
            var child = msDoc["Root"] ?? msDoc["AdvancedModelSystem"];
            bool hasDescription = false;
            var attributes = child.Attributes;
            if (attributes != null)
            {
                foreach (XmlAttribute attribute in attributes)
                {
                    if (attribute.Name == "Description")
                    {
                        hasDescription = true;
                        pms.Description = attribute.InnerText;
                        break;
                    }
                }
            }

            if (child.HasChildNodes)
            {
                ModelSystemStructure ms = XTMF.ModelSystemStructure.Load(child, _Configuration);
                if (ms != null)
                {
                    pms.Root = ms;
                }
            }

            if (pms.Root == null)
            {
                return false;
            }

            if (!hasDescription)
            {
                pms.Description = pms.Root.Description;
            }

            // now do a second pass for Linked parameters, since we need the current model system to actually link things
            for (int i = 0; i < child.ChildNodes.Count; i++)
            {
                switch (child.ChildNodes[i].Name)
                {
                    case "LinkedParameters":
                        {
                            pms.LinkedParameters = LoadLinkedParameters(child.ChildNodes[i], pms.Root);
                            break;
                        }
                    case "Regions":
                        {
                            pms.RegionDisplays = LoadRegionDisplays(child.ChildNodes[i], pms.Root);
                        }
                        break;
                }
            }


            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="child"></param>
        /// <param name="index"></param>
        /// <param name="guid"></param>
        /// <param name="pms"></param>
        /// <returns></returns>
        private bool LoadAdvancedModelSystem(XmlNode child, int index, string guid, out ProjectModelSystem pms)
        {
            pms = new ProjectModelSystem()
            {
                GUID = guid
            };
            bool hasDescription = false;
            var attributes = child.Attributes;
            if (attributes != null)
            {
                foreach (XmlAttribute attribute in attributes)
                {
                    if (attribute.Name == "Description")
                    {
                        hasDescription = true;
                        pms.Description = attribute.InnerText;
                        break;
                    }
                }
            }

            if (!hasDescription)
            {
                pms.Description = "No Description";
            }

            if (child.HasChildNodes)
            {
                for (int i = 0; i < child.ChildNodes.Count; i++)
                {
                    switch (child.ChildNodes[i].Name)
                    {
                        case "ModelSystem":
                            {
                                if (pms.Root == null)
                                {
                                    if (child.ChildNodes[i].FirstChild != null)
                                    {
                                        ModelSystemStructure ms =
                                            XTMF.ModelSystemStructure.Load(child.ChildNodes[i], _Configuration);
                                        if (ms != null)
                                        {
                                            pms.Root = ms;
                                        }
                                    }
                                }
                            }
                            break;
                    }
                }

                if (pms.Root == null)
                {
                    return false;
                }

                // now do a second pass for Linked parameters, since we need the current model system to actually link things
                for (int i = 0; i < child.ChildNodes.Count; i++)
                {
                    switch (child.ChildNodes[i].Name)
                    {
                        case "LinkedParameters":
                            {
                                pms.LinkedParameters = LoadLinkedParameters(child.ChildNodes[i], pms.Root);
                                break;
                            }
                        case "Regions":
                            {
                                pms.RegionDisplays = LoadRegionDisplays(child.ChildNodes[i], pms.Root);
                            }
                            break;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        private void LoadDescription()
        {
            try
            {
                var fileName = Path.Combine(_Configuration.ProjectDirectory, Name, "Project.xml");
                if (File.Exists(fileName))
                {
                    using (XmlReader reader = XmlReader.Create(fileName))
                    {
                        while (!reader.EOF && reader.Read())
                        {
                            if (reader.NodeType != XmlNodeType.Element) continue;
                            switch (reader.LocalName)
                            {
                                case "Root":
                                    Description = reader.GetAttribute("Description");
                                    // we can just exit at this point since using will clean up for us
                                    return;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="mss"></param>
        /// <returns></returns>
        private List<IRegionDisplay> LoadRegionDisplays(XmlNode regionsNode, IModelSystemStructure mss)
        {
            List<IRegionDisplay> regionDisplays = new List<IRegionDisplay>();
            if (!regionsNode.HasChildNodes)
            {
                return regionDisplays;
            }

            foreach (XmlNode node in regionsNode.ChildNodes)
            {
                RegionDisplay regionDisplay = new RegionDisplay()
                {
                    Name = node.Attributes?["Name"].Value
                };

                var xmlRegionGroupNodes = node.SelectNodes("RegionGroup");

                if (xmlRegionGroupNodes != null)
                {
                    foreach (XmlNode regionGroupNode in xmlRegionGroupNodes)
                    {
                        RegionGroup regionGroup = new RegionGroup()
                        {
                            Name = regionGroupNode.Attributes?["Name"].Value
                        };

                        var xmlGroupModuleNodes = regionGroupNode.SelectNodes("Module");

                        if (xmlGroupModuleNodes != null)
                        {
                            foreach (XmlNode moduleNode in xmlGroupModuleNodes)
                            {
                                //get reference to this module
                                string reference = moduleNode.Attributes?["Reference"].Value;
                                var modelSystemStructure = GetModuleFromReference(reference, mss);

                                regionGroup.Modules.Add((IModelSystemStructure2)modelSystemStructure);
                            }
                        }

                        regionDisplay.RegionGroups.Add(regionGroup);
                    }
                }

                regionDisplays.Add(regionDisplay);
            }

            return regionDisplays;
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlNode"></param>
        /// <param name="mss"></param>
        /// <returns></returns>
        private List<ILinkedParameter> LoadLinkedParameters(XmlNode xmlNode, IModelSystemStructure mss)
        {
            List<ILinkedParameter> lpl = new List<ILinkedParameter>();
            // if there is nothing to load just return back a blank list
            if (!xmlNode.HasChildNodes)
            {
                return lpl;
            }

            foreach (XmlNode lpNode in xmlNode.ChildNodes)
            {
                if (lpNode.Name == "LinkedParameter")
                {
                    var name = "unnamed";
                    var value = string.Empty;
                    var attributes = lpNode.Attributes;
                    if (attributes != null)
                    {
                        foreach (XmlAttribute attribute in attributes)
                        {
                            switch (attribute.Name)
                            {
                                case "Name":
                                    {
                                        name = attribute.InnerText;
                                    }
                                    break;

                                case "Value":
                                    {
                                        value = attribute.InnerText;
                                    }
                                    break;
                            }
                        }
                    }

                    LinkedParameter lp = new LinkedParameter(name);
                    string error = null;
                    lp.SetValue(value, ref error);
                    lpl.Add(lp);
                    // if there are no references just continue
                    if (!lpNode.HasChildNodes)
                    {
                        continue;
                    }

                    foreach (XmlNode lpCNode in lpNode)
                    {
                        if (lpCNode.Name == "Reference")
                        {
                            if (lpCNode.Attributes != null)
                            {
                                foreach (XmlAttribute attribute in lpCNode.Attributes)
                                {
                                    if (attribute.Name == "Name")
                                    {
                                        var param = GetParameterFromLink(attribute.InnerText, mss);
                                        if (param != null)
                                        {
                                            lp.Add(param, ref error);
                                        }

                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return lpl;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="lpl"></param>
        /// <param name="mss"></param>
        private void WriteRegions(XmlTextWriter writer, List<IRegionDisplay> regionDisplays, IModelSystemStructure mss)
        {
            foreach (var regionDisplay in regionDisplays)
            {
                writer.WriteStartElement("RegionDisplay");
                writer.WriteAttributeString("Name", regionDisplay.Name);

                foreach (var regionGroup in regionDisplay.RegionGroups)
                {
                    writer.WriteStartElement("RegionGroup");
                    writer.WriteAttributeString("Name", regionGroup.Name);

                    foreach (var module in regionGroup.Modules)
                    {
                        writer.WriteStartElement("Module");
                        var referencePath = this.GetModuleReferencePath(module, new List<string>());
                        writer.WriteAttributeString("Reference", referencePath);

                        writer.WriteEndElement();
                    }


                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelSystemStructure"></param>
        /// <param name="referencePath"></param>
        /// <returns></returns>
        private string GetModuleReferencePath(IModelSystemStructure modelSystemStructure, List<string> referencePath)
        {
            if (modelSystemStructure.Parent == null)
            {
                referencePath?.Insert(0, modelSystemStructure.Name);

                

                return string.Join(".", referencePath?.ToArray());
            }
            else
            {
                referencePath.Insert(0, modelSystemStructure.Name);
                if (modelSystemStructure.Parent.IsCollection)
                {
                    referencePath.Insert(0, modelSystemStructure.Parent.Children.IndexOf(modelSystemStructure).ToString());
                }
                return GetModuleReferencePath(modelSystemStructure.Parent, referencePath);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="lpl"></param>
        /// <param name="mss"></param>
        private void WriteLinkedParameters(XmlTextWriter writer, List<ILinkedParameter> lpl, IModelSystemStructure mss)
        {
            foreach (var lp in lpl)
            {
                writer.WriteStartElement("LinkedParameter");
                writer.WriteAttributeString("Name", lp.Name);
                if (lp.Value != null)
                {
                    writer.WriteAttributeString("Value", lp.Value.ToString());
                }

                foreach (var reference in lp.Parameters)
                {
                    writer.WriteStartElement("Reference");
                    writer.WriteAttributeString("Name", LookupName(reference, mss));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool Save(string path, ref string error)
        {
            // We need this in case it is a new project that has no model systems
            if (_ProjectModelSystems == null)
            {
                _ProjectModelSystems = new List<ProjectModelSystem>();
            }

            var dirName = Path.GetDirectoryName(path);
            if (dirName == null)
            {
                error = $"The path '{path}' is invalid!";
                return false;
            }

            var tempFileName = Path.GetTempFileName();
            if (!Directory.Exists(dirName))
            {
                bool directoryExists = false;
                while (!directoryExists)
                {
                    Directory.CreateDirectory(dirName);
                    for (int i = 0; i < 10; i++)
                    {
                        if (Directory.Exists(dirName))
                        {
                            directoryExists = true;
                            break;
                        }
                        Thread.Sleep(18);
                    }
                }
            }
            string modelSystemDirectoryPath = EnsureModelSystemDirectoryExists(Path.GetDirectoryName(path));

            try
            {
                List<Task> writeTasks = new List<Task>(_ProjectModelSystems.Count);
                using (XmlTextWriter writer = new XmlTextWriter(tempFileName, Encoding.Unicode))
                {
                    writer.Formatting = Formatting.Indented;
                    writer.WriteStartDocument();
                    writer.WriteStartElement("Root");

                    if (Description != null)
                    {
                        writer.WriteAttributeString("Description", Description);
                    }

                    foreach (var pms in _ProjectModelSystems)
                    {
                        writer.WriteStartElement("DetachedModelSystem");
                        writer.WriteAttributeString("Name", pms.Name);
                        writer.WriteAttributeString("Description", pms.Description);
                        writer.WriteAttributeString("GUID", pms.GUID);
                        writer.WriteEndElement();
                        var ms = pms;
                        writeTasks.Add(Task.Run(() =>
                        {
                            if (ms.Root.Type != null)
                            {
                                var tempMSFileName = Path.GetTempFileName();
                                using (XmlTextWriter msWriter = new XmlTextWriter(tempMSFileName, Encoding.Unicode))
                                {
                                    msWriter.WriteStartDocument();
                                    msWriter.WriteStartElement("Root");
                                    ms.Root.Save(msWriter);
                                    msWriter.WriteStartElement("LinkedParameters");
                                    WriteLinkedParameters(msWriter, ms.LinkedParameters, ms.Root);
                                    msWriter.WriteEndElement();

                                    msWriter.WriteStartElement("Regions");
                                    WriteRegions(msWriter, ms.RegionDisplays, ms.Root);
                                    msWriter.WriteEndElement();
                                    msWriter.WriteEndElement();
                                }

                                File.Copy(tempMSFileName,
                                    Path.Combine(modelSystemDirectoryPath, $"Project.ms-{ms.GUID}.xml"), true);
                                File.Delete(tempMSFileName);
                            }
                        }));
                    }

                    writer.WriteEndElement();
                }

                Task.WaitAll(writeTasks.ToArray());
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }

            if (File.Exists(path))
            {
                File.Copy(path, Path.Combine(dirName, "Project.bak.xml"), true);
            }

            File.Copy(tempFileName, path, true);
            File.Delete(tempFileName);
            HasChanged = false;
            return true;
        }

        private static string EnsureModelSystemDirectoryExists(string projectDirectory)
        {
            // Create the model system directory
            var modelSystemDirectoryPath = Path.Combine(projectDirectory, "._ModelSystems");
            if (!Directory.Exists(modelSystemDirectoryPath))
            {
                DirectoryInfo msDirectory = Directory.CreateDirectory(modelSystemDirectoryPath);
                msDirectory.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }
            return modelSystemDirectoryPath;
        }
    }
}
