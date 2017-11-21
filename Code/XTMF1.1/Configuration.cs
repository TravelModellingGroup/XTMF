/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using XTMF.Networking;

namespace XTMF
{
    public sealed class Configuration : IConfiguration, IDisposable, INotifyPropertyChanged
    {
        public Dictionary<string, string> AdditionalSettings = new Dictionary<string, string>();

        // The configuration file name will be saved when initializing the object
        internal string ConfigurationFileName { get; private set; }
        private IClient _CurrentClient = null;
        private IHost _CurrentHost = null;
        private string _ModuleDirectory = "Modules";
        public Version XTMFVersion { get; private set; }
        public string BuildDate { get; private set; }
        public bool ExecuteRunsInADifferentProcess { get; private set; }
        public Configuration(Assembly baseAssembly = null)
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XTMF", "Configuration.xml"))
        {

        }

        public string Theme { get; set; }

        public Configuration(string configurationFileName, Assembly baseAssembly = null, bool loadModules = true)
        {
            HostPort = 1447;
            BaseAssembly = baseAssembly;
            ProgressReports = new BindingListWithRemoving<IProgressReport>();
            LoadUserConfiguration(configurationFileName);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            ModelRepository = new ModuleRepository();
            ModelSystemTemplateRepository = new ModelSystemTemplateRepository();
            LoadVersion();
            if (loadModules)
            {
                LoadModules();
            }
            try
            {
                ModelSystemRepository = new ModelSystemRepository(this);
            }
            catch (Exception e)
            {
                LoadError = "Unable to load model system: " + e.Message;
                LoadErrorTerminal = true;
            }
            try
            {
                ProjectRepository = new ProjectRepository(this);
            }
            catch (Exception e)
            {
                LoadError = "Unable to load projects: " + e.Message;
                LoadErrorTerminal = true;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void LoadVersion()
        {
            Version version;
            version = new Version(0, 0, 0);
            string buildDate = "Unknown Build Date";
            try
            {
                var assemblyLocation = Assembly.GetEntryAssembly().Location;
                var versionFile = Path.Combine(Path.GetDirectoryName(assemblyLocation), "version.txt");

                if (File.Exists(versionFile))
                {
                    using (StreamReader reader = new StreamReader(versionFile))
                    {
                        version = new Version(reader.ReadLine());
                        if (!reader.EndOfStream)
                        {
                            buildDate = reader.ReadLine();
                        }
                    }
                }
                else
                {

                }
            }
            catch
            {

            }
            XTMFVersion = version;
            BuildDate = buildDate;
        }

        public event Action OnModelSystemExit;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool AutoSave { get; set; }

        public Assembly BaseAssembly { get; set; }

        public string ConfigurationDirectory => Path.GetDirectoryName(ConfigurationFileName);

        public IModuleRepository ModelRepository { get; private set; }

        private string _ModelSystemDirectory;
        public string ModelSystemDirectory
        {
            get => _ModelSystemDirectory;
            set
            {
                if (_ModelSystemDirectory != value)
                {
                    _ModelSystemDirectory = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ModelSystemDirectory"));
                }
            }
        }

        public IModelSystemRepository ModelSystemRepository { get; private set; }

        public IModelSystemTemplateRepository ModelSystemTemplateRepository { get; private set; }

        public BindingListWithRemoving<IProgressReport> ProgressReports { get; private set; }

        public List<string> RecentProjects { get; private set; }

        private string _ProjectDirectory;
        public string ProjectDirectory
        {
            get
            {
                return _ProjectDirectory;
            }
            set
            {
                if (_ProjectDirectory != value)
                {
                    _ProjectDirectory = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ProjectDirectory"));
                }
            }
        }

        public IProjectRepository ProjectRepository { get; set; }

        internal string RemoteServerAddress { get; set; }

        internal int RemoteServerPort { get; set; }

        private int _HostPort;
        public int HostPort
        {
            get
            {
                return _HostPort;
            }
            set
            {
                if (_HostPort != value)
                {
                    _HostPort = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HostPort"));
                }
            }
        }

        public bool RunInSeperateProcess { get; set; } = true;

        public void CreateProgressReport(string name, Func<float> ReportProgress, Tuple<byte, byte, byte> c = null)
        {
            lock (this)
            {
                foreach (var report in ProgressReports)
                {
                    if (report.Name == name)
                    {
                        report.Colour = c;
                        return;
                    }
                }
                ProgressReports.Add(new ProgressReport() { Name = name, GetProgress = ReportProgress, Colour = c });
            }
        }

        public void DeleteAllProgressReport()
        {
            lock (this)
            {
                ProgressReports.Clear();
            }
        }

        public void DeleteProgressReport(string name)
        {
            lock (this)
            {
                for (int i = 0; i < ProgressReports.Count; i++)
                {
                    if (ProgressReports[i].Name == name)
                    {
                        ProgressReports.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public IHost GetActiveHost()
        {
            lock (this)
            {
                Thread.MemoryBarrier();
                return _CurrentHost;
            }
        }

        public bool InstallModule(string moduleFileName)
        {
            if (!File.Exists(moduleFileName))
            {
                return false;
            }
            var destName = Path.Combine(_ModuleDirectory, Path.GetFileName(moduleFileName));
            if (File.Exists(destName))
            {
                return false;
            }
            try
            {
                File.Copy(moduleFileName, destName, true);
            }
            catch
            {
                return false;
            }
            LoadAssembly(Assembly.LoadFrom(destName));
            return true;
        }

        public void ModelSystemExited()
        {
            if (OnModelSystemExit != null)
            {
                try
                {
                    OnModelSystemExit();
                }
                catch
                {
                }
                var dels = OnModelSystemExit.GetInvocationList();
                foreach (Delegate d in dels)
                {
                    OnModelSystemExit -= (Action)d;
                }
                OnModelSystemExit = null;
            }
        }

        public IClient RetriveCurrentNetworkingClient()
        {
            return _CurrentClient;
        }

        public void Save()
        {
            SaveConfiguration(ConfigurationFileName);
        }

        public bool CheckProjectExists(string name)
        {
            DirectoryInfo info = new DirectoryInfo(Path.Combine(ProjectDirectory, name));
            return (info.Exists && info.GetFiles().Any(fileInfo => fileInfo.Name == "Project.xml"));
        }

        public bool SetProjectDirectory(string dir, ref string error)
        {
            if (!ValidateProjectDirectory(dir, ref error))
            {
                return false;
            }

            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (UnauthorizedAccessException)
                {
                    error = "Unable to create directory " + dir + ". Access was denied!";
                    return false;
                }
                catch (IOException)
                {
                    error = "Unable to create directory " + dir + ". Unable to write to the location!";
                    return false;
                }
            }
            else
            {
                if (!HasFolderWritePermission(dir))
                {
                    error = "Unable to use directory " + dir + ". Access was denied!";
                    return false;
                }
            }
            ProjectDirectory = dir;
            return true;
        }

        /// <summary>
        /// Check to see if the user has access to write to the directory.
        /// </summary>
        /// <param name="destDir">The directory to write to.</param>
        /// <returns>True if the user has write access</returns>
        /// <see cref="http://stackoverflow.com/questions/1410127/c-sharp-test-if-user-has-write-access-to-a-folder"/>
        public static bool HasFolderWritePermission(string destDir)
        {
            if (string.IsNullOrEmpty(destDir) || !Directory.Exists(destDir)) return false;
            try
            {
                var ret = false;
                DirectorySecurity security = Directory.GetAccessControl(destDir);

                var user = WindowsIdentity.GetCurrent();
                SecurityIdentifier currentUser = user.User;
                foreach (AuthorizationRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                {
                    FileSystemAccessRule rights = ((FileSystemAccessRule)rule);
                    if (currentUser == rule.IdentityReference)
                    {
                        // user specific access overwrites and group level access so we can return immediately 
                        if (rights.AccessControlType == AccessControlType.Allow)
                        {
                            if (rights.FileSystemRights == (rights.FileSystemRights | FileSystemRights.Modify)) return true;
                        }
                        else
                        {
                            // if deny
                            if (rights.FileSystemRights == (rights.FileSystemRights | FileSystemRights.Modify)) return false;
                        }
                    }
                    else if (user.Groups.Contains(rule.IdentityReference))
                    {
                        // if the user is in any group that is allowed then they are allowed
                        // unless that user is specifically not allowed
                        if (rights.AccessControlType == AccessControlType.Allow)
                        {
                            if (rights.FileSystemRights == (rights.FileSystemRights | FileSystemRights.Modify))
                            {
                                ret = true;
                            }
                        }
                    }
                }
                return ret;
            }
            catch
            {
                return false;
            }
        }

        public bool SetModelSystemDirectory(string dir, ref string error)
        {
            if (!ValidateProjectDirectory(dir, ref error))
            {
                return false;
            }
            if (!Directory.Exists(dir))
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (UnauthorizedAccessException)
                {
                    error = "Unable to create directory " + dir + ". Access was denied!";
                    return false;
                }
                catch (IOException)
                {
                    error = "Unable to create directory " + dir + ". Unable to write to the location!";
                    return false;
                }
            }
            else
            {
                if (!HasFolderWritePermission(dir))
                {
                    error = "Unable to use directory " + dir + ". Access was denied!";
                    return false;
                }
            }
            ModelSystemDirectory = dir;
            return true;
        }

        public bool StartupNetworkingClient(out IClient networkingClient, ref string error)
        {
            networkingClient = null;
            lock (this)
            {
                Thread.MemoryBarrier();
                if (_CurrentClient != null)
                {
                    networkingClient = _CurrentClient;
                    return true;
                }
                else
                {
                    try
                    {
                        _CurrentClient = new Client(RemoteServerAddress, RemoteServerPort, this);
                    }
                    catch
                    {
                        return false;
                    }
                    Thread.MemoryBarrier();
                    networkingClient = _CurrentClient;
                    return true;
                }
            }
        }

        public bool StartupNetworkingHost(out IHost networkingHost, ref string error)
        {
            networkingHost = null;
            lock (this)
            {
                Thread.MemoryBarrier();
                if (_CurrentHost == null || _CurrentHost.IsShutdown)
                {
                    try
                    {
                        _CurrentHost = new Host(this);
                    }
                    catch
                    {
                        return false;
                    }
                }
                else
                {
                    ((Host)_CurrentHost).ReleaseRegisteredHandlers();
                }
                Thread.MemoryBarrier();
                networkingHost = _CurrentHost;
            }
            return true;
        }

        public void UpdateProgressReportColour(string name, Tuple<byte, byte, byte> c)
        {
            lock (this)
            {
                foreach (var report in ProgressReports)
                {
                    if (report.Name == name)
                    {
                        report.Colour = c;
                        return;
                    }
                }
            }
        }

        public bool ValidateProjectDirectory(string dir, ref string error)
        {
            if (!IsValidPath(dir, ref error))
            {
                return false;
            }
            return true;
        }

        private bool IsValidPath(string dir, ref string error)
        {
            var invalidCharacters = Path.GetInvalidPathChars();
            foreach (char c in invalidCharacters)
            {
                if (dir.Contains(c))
                {
                    error = "A path can not contain the character \"" + c + "\"!";
                    return false;
                }
            }
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(dir);
            }
            catch
            {
                error = "The path is invalid!";
                return false;
            }
            return true;
        }

        public string LoadError;
        public bool LoadErrorTerminal = false;

        private ConcurrentBag<Type> FreeVariableType = new ConcurrentBag<Type>();
        internal readonly ConcurrentDictionary<string, Type> ModuleRedirection = new ConcurrentDictionary<string, Type>();

        private void LoadAssembly(Assembly assembly)
        {
            Type module = typeof(IModule);
            Type modelSystem = typeof(IModelSystemTemplate);
            try
            {
                var types = assembly.GetTypes();
                for (int i = 0; i < types.Length; i++)
                {
                    var type = types[i];
                    if (type.IsNotPublic || InvalidClassName(type)) continue;
                    // Make sure that they are valid types
                    FreeVariableType.Add(type);
                    if (type.IsAbstract || !(type.IsClass || type.IsValueType)) continue;
                    if (module.IsAssignableFrom(type))
                    {
                        string error = null;
                        if (CheckTypeForErrors(type, ref error))
                        {
                            LoadError = error;
                        }
                        LoadRedirections(type);
                        // we know then that this is an IModel
                        lock (ModelRepository)
                        {
                            ModelRepository.AddModule(type);
                        }
                        if (modelSystem.IsAssignableFrom(type))
                        {
                            lock (ModelSystemTemplateRepository)
                            {
                                ModelSystemTemplateRepository.Add(type);
                            }
                        }
                    }
                }
            }
            catch (TypeLoadException e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Check the given type for module redirections
        /// </summary>
        /// <param name="type"></param>
        private void LoadRedirections(Type type)
        {
            foreach (var redirection in type.GetCustomAttributes(typeof(RedirectModule)))
            {
                // Remove the whitespace because it is inconstant between .Net versions
                ModuleRedirection[(redirection as RedirectModule).FromType.Replace(" ", "")] = type;
            }
        }

        /// <summarys
        /// Check to see if the given type should not be loaded given its name
        /// </summary>
        /// <param name="type">The type to check for</param>
        /// <returns>If it should not be loaded</returns>
        private static bool InvalidClassName(Type type)
        {
            var c = type.Name[0];
            return c == '_' || c == '<';
        }

        /// <summary>
        /// Checks to see if the module violates any rules
        /// </summary>
        /// <param name="type">The type to process</param>
        /// <param name="error">A message describing the error.</param>
        /// <returns>True if there is an error.</returns>
        private bool CheckTypeForErrors(Type type, ref string error) => 
            CheckForParameterDelcarationErrors(type, ref error) ||
            CheckForNonPublicRootAndParentTags(type, ref error);


        /// <summary>
        /// Check to see if the given type uses the RootModuleAttribute or the ParentModelAttribute but do
        /// not declare those variables public.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="error">A description of the error</param>
        /// <returns>True if there is an error, false otherwise</returns>
        private static bool CheckForNonPublicRootAndParentTags(Type type, ref string error)
        {
            var failures = from t in UnifiedFieldType.GetMembers(type)
                           where t.GetAttributes().Any(o => (o is RootModule) || (o is ParentModel) || (o is ParameterAttribute)) && !t.IsPublic
                           select t;
            var firstFailure = failures.FirstOrDefault();
            if (firstFailure != null)
            {
                error = "When analyzing the type '" + type.FullName + "' the member '" + firstFailure.Name
                    + "' used a header (RootModule/ParentModel/Parameter) to get a value from XTMF however it is not public.  This violates the XTMF coding conventions, and will not work as expected."
                     + "\r\nPlease close XTMF and recompile your module after correcting this issue.";
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check the given type to make sure that the parameters that are defined are all uniquely defined.
        /// </summary>
        /// <param name="type">The type to check for</param>
        /// <param name="error">A description of the issue in the code that needs to be resolved.</param>
        /// <returns>True if there is an error, false otherwise.</returns>
        private static bool CheckForParameterDelcarationErrors(Type type, ref string error)
        {
            var parameters = Project.GetParameters(type).Parameters;
            for (int i = 0; i < parameters.Count - 1; i++)
            {
                var firstName = parameters[i].Name;
                for (int j = i + 1; j < parameters.Count; j++)
                {
                    var secondName = parameters[j].Name;
                    if (firstName == secondName)
                    {
                        error = "In the module type '" + type.FullName + "' there are parameters sharing the same name '" + firstName + "' which is not allowed in XTMF.\r\n"
                            + "The field names are '" + parameters[i].VariableName + "' and '" + parameters[j].VariableName + "'.\r\n"
                            + "Please close XTMF and recompile your module after correcting this issue.";
                        return true;
                    }
                }
            }
            return false;
        }

        public void RenameRecentProject(string oldProjectName, string newProjectName)
        {
            RemoveRecentProject(oldProjectName);
            AddRecentProject(newProjectName);
        }

        public void RemoveRecentProject(string name)
        {
            RecentProjects.Remove(name);
        }
        public void AddRecentProject(string name)
        {
            RecentProjects.Remove(name);
            RecentProjects.Insert(0, name);
            if (RecentProjects.Count > 5)
            {
                RecentProjects.RemoveAt(5);
            }
        }

        private void LoadConfigurationFile(string configFileName)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement root;
            try
            {
                doc.Load(configFileName);
            }
            catch (XmlException)
            {
                SaveConfiguration(configFileName);
                return;
            }
            root = doc["Root"];
            if (root == null || !root.HasChildNodes)
            {
                SaveConfiguration(configFileName);
                return;
            }
            foreach (XmlNode child in root.ChildNodes)
            {
                switch (child.Name)
                {
                    case "RecentProjects":

                        foreach (XmlNode recentProjectNode in child.ChildNodes)
                        {
                            var projectName = recentProjectNode.Attributes["Name"].Value;
                            if (CheckProjectExists(projectName))
                            {
                                AddRecentProject(projectName);
                            }
                        }
                        break;
                    case "ExecuteRunsInADifferentProcess":
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                if (bool.TryParse(attribute.InnerText, out var result))
                                {
                                    ExecuteRunsInADifferentProcess = result;
                                }
                            }
                        }
                        break;
                    case "ProjectDirectory":
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                var dir = attribute.InnerText;
                                string error = null;
                                if (!SetProjectDirectory(dir, ref error))
                                {
                                    LoadError = error;
                                    LoadErrorTerminal = true;
                                }
                            }
                        }
                        break;
                    case "ModelSystemDirectory":
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                var dir = attribute.InnerText;
                                string error = null;
                                if (!SetModelSystemDirectory(dir, ref error))
                                {
                                    LoadError = error;
                                    LoadErrorTerminal = true;
                                }
                            }
                        }
                        break;
                    case "AutoSave":
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                var booleanText = attribute.InnerText;
                                if (bool.TryParse(booleanText, out bool b))
                                {
                                    AutoSave = b;
                                }
                            }
                        }
                        break;
                    case "HostPort":
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                var booleanText = attribute.InnerText;
                                if (int.TryParse(booleanText, out int portNumber))
                                {
                                    _HostPort = portNumber;
                                }
                            }
                        }
                        break;
                    case "Theme":
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                Theme = attribute.InnerText;
                            }
                        }
                        break;
                    default:
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                AdditionalSettings[child.Name] = attribute.InnerText;
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Get all of the types that satisfy the conditions that are presented.
        /// </summary>
        /// <param name="conditions">The conditions required for a type to be acceptable.</param>
        /// <returns>A collection of the types that are possible.</returns>
        public ICollection<Type> GetValidGenericVariableTypes(Type[] conditions)
        {
            var validTypes = new ConcurrentBag<Type>();
            if (conditions == null || conditions.Length == 0)
            {
                return FreeVariableType.ToList();
            }
            else
            {
                Parallel.ForEach(FreeVariableType, (Type t) =>
                {
                    foreach (var condition in conditions)
                    {
                        if (!condition.IsAssignableFrom(t))
                        {
                            return;
                        }
                    }
                    validTypes.Add(t);
                });
            }
            return validTypes.ToList();
        }

        public void LoadModules(Action loadModulesCompleteAction)
        {
            LoadModules();
            loadModulesCompleteAction.Invoke();
        }

        private void LoadModules()
        {
            // load in the types from system
            LoadAssembly(typeof(float).GetType().Assembly);
            // Load the given base assembly
            if (BaseAssembly != null)
            {
                LoadAssembly(BaseAssembly);
            }
            else
            {
                // if the base assembly has not been set then try to find it
                Assembly baseAssembly = Assembly.GetEntryAssembly();
                if (baseAssembly != null)
                {
                    LoadAssembly(baseAssembly);
                }
            }
            if (Directory.Exists(_ModuleDirectory))
            {
                var files = Directory.GetFiles(_ModuleDirectory, "*.dll");
                Parallel.For(0, files.Length,
                    (int i) =>
                    {
                        try
                        {
                            LoadAssembly((Assembly.Load(Path.GetFileNameWithoutExtension(files[i]))));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error when trying to load assembly '" + Path.GetFileNameWithoutExtension(files[i]) + "'");
                            Console.WriteLine(e.ToString());
                        }
                    });
            }
        }

        private void LoadUserConfiguration(string configFile)
        {
            RecentProjects = new List<string>();
            ConfigurationFileName = configFile;
            var directory = ConfigurationDirectory;
            var defaultProjectDirectory =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "XTMF", "Projects");
            ProjectDirectory = defaultProjectDirectory;
            AutoSave = true;
            var msDir = Path.Combine(directory, "ModelSystems");
            if (!Directory.Exists(msDir))
            {
                Directory.CreateDirectory(msDir);
            }
            ModelSystemDirectory = msDir;
            AdditionalSettings["UseGlass"] = "false";
            AdditionalSettings["EditProjects"] = "false";
            if (!File.Exists(configFile))
            {
                SaveConfiguration(configFile);
            }
            else
            {
                LoadConfigurationFile(configFile);
            }
        }

        private void SaveConfiguration(string configFileName)
        {
            using (XmlWriter writer = XmlWriter.Create(configFileName, new XmlWriterSettings() { Encoding = Encoding.Unicode, Indent = true, NewLineOnAttributes = true }))
            {
                // Start the document and create the default root node
                writer.WriteStartDocument(true);
                writer.WriteStartElement("Root");
                // Now that we have it all started we can go and write in all of the setting that we are going to need
                writer.WriteStartElement("ProjectDirectory");
                writer.WriteAttributeString("Value", ProjectDirectory);
                writer.WriteEndElement();
                writer.WriteStartElement("ModelSystemDirectory");
                writer.WriteAttributeString("Value", ModelSystemDirectory);
                writer.WriteEndElement();
                // Auto Save
                writer.WriteStartElement("AutoSave");
                writer.WriteAttributeString("Value", AutoSave.ToString());
                writer.WriteEndElement();
                // Host Port
                writer.WriteStartElement("HostPort");
                writer.WriteAttributeString("Value", HostPort.ToString());
                writer.WriteEndElement();
                // ExecuteRunsInADifferentProcess
                writer.WriteStartElement("ExecuteRunsInADifferentProcess");
                writer.WriteAttributeString("Value", ExecuteRunsInADifferentProcess.ToString());
                writer.WriteEndElement();
                if (AdditionalSettings != null)
                {
                    if (AdditionalSettings.Count > 0)
                    {
                        foreach (var setting in AdditionalSettings)
                        {
                            if (setting.Value != null && setting.Key != null)
                            {
                                writer.WriteStartElement(setting.Key);
                                writer.WriteAttributeString("Value", setting.Value);
                                writer.WriteEndElement();
                            }
                        }
                    }
                }
                // Write recent projects to configuration
                writer.WriteStartElement("RecentProjects");
                foreach (string project in RecentProjects)
                {
                    writer.WriteStartElement("Project");
                    writer.WriteAttributeString("Name", project);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("Theme");
                writer.WriteAttributeString("Value", Theme);
                writer.WriteEndElement();
                //Finished writing all of the settings so we can finish the document now
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private class ProgressReport : IProgressReport
        {
            public Tuple<byte, byte, byte> Colour { get; set; }

            public Func<float> GetProgress { get; internal set; }

            public string Name { get; internal set; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool all)
        {
            (_CurrentHost as IDisposable)?.Dispose();
            _CurrentHost = null;
            (_CurrentClient as IDisposable)?.Dispose();
            _CurrentClient = null;
        }
    }
}