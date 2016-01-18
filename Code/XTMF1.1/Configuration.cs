/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using XTMF.Networking;

namespace XTMF
{
    public class Configuration : IConfiguration, IDisposable, INotifyPropertyChanged
    {
        public Dictionary<string, string> AdditionalSettings = new Dictionary<string, string>();

        // The configuration file name will be saved when initializing the object
        private string ConfigurationFileName;
        private IClient CurrentClient = null;
        private IHost CurrentHost = null;
        private string ModuleDirectory = "Modules";

        public Configuration(Assembly baseAssembly = null)
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XTMF", "Configuration.xml"))
        {

        }

        public Configuration(string configurationFileName, Assembly baseAssembly = null)
        {
            HostPort = 1447;
            this.BaseAssembly = baseAssembly;
            this.ProgressReports = new BindingListWithRemoving<IProgressReport>();
            this.LoadUserConfiguration(configurationFileName);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            this.ModelRepository = new ModuleRepository();
            this.ModelSystemTemplateRepository = new ModelSystemTemplateRepository();
            LoadModules();
            this.ModelSystemRepository = new ModelSystemRepository(this);
            this.ProjectRepository = new ProjectRepository(this);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public event Action OnModelSystemExit;
        public event PropertyChangedEventHandler PropertyChanged;

        public bool AutoSave { get; set; }

        public Assembly BaseAssembly { get; set; }

        public string ConfigurationDirectory { get { return Path.GetDirectoryName(ConfigurationFileName); } }

        public IModuleRepository ModelRepository
        {
            get;
            private set;
        }

        private string _ModelSystemDirectory;
        public string ModelSystemDirectory
        {
            get
            {
                return _ModelSystemDirectory;
            }
            set
            {
                if (_ModelSystemDirectory != value)
                {
                    _ModelSystemDirectory = value;
                    var e = PropertyChanged;
                    if (e != null)
                    {
                        e(this, new PropertyChangedEventArgs("ModelSystemDirectory"));
                    }
                }
            }
        }

        public IModelSystemRepository ModelSystemRepository
        {
            get;
            private set;
        }

        public IModelSystemTemplateRepository ModelSystemTemplateRepository { get; private set; }

        public BindingListWithRemoving<IProgressReport> ProgressReports { get; private set; }

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
                    var e = PropertyChanged;
                    if (e != null)
                    {
                        e(this, new PropertyChangedEventArgs("ProjectDirectory"));
                    }
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
                    var e = PropertyChanged;
                    if (e != null)
                    {
                        e(this, new PropertyChangedEventArgs("HostPort"));
                    }
                }
            }
        }

        public void CreateProgressReport(string name, Func<float> ReportProgress, Tuple<byte, byte, byte> c = null)
        {
            lock (this)
            {
                foreach (var report in this.ProgressReports)
                {
                    if (report.Name == name)
                    {
                        report.Colour = c;
                        return;
                    }
                }
                this.ProgressReports.Add(new ProgressReport() { Name = name, GetProgress = ReportProgress, Colour = c });
            }
        }

        public void DeleteAllProgressReport()
        {
            lock (this)
            {
                this.ProgressReports.Clear();
            }
        }

        public void DeleteProgressReport(string name)
        {
            lock (this)
            {
                for (int i = 0; i < this.ProgressReports.Count; i++)
                {
                    if (this.ProgressReports[i].Name == name)
                    {
                        this.ProgressReports.RemoveAt(i);
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
                return this.CurrentHost;
            }
        }

        public bool InstallModule(string moduleFileName)
        {
            if (!File.Exists(moduleFileName))
            {
                return false;
            }
            var destName = Path.Combine(this.ModuleDirectory, Path.GetFileName(moduleFileName));
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
            this.LoadAssembly(Assembly.LoadFrom(destName));
            return true;
        }

        public void ModelSystemExited()
        {
            if (this.OnModelSystemExit != null)
            {
                try
                {
                    this.OnModelSystemExit();
                }
                catch
                {
                }
                var dels = this.OnModelSystemExit.GetInvocationList();
                foreach (Delegate d in dels)
                {
                    this.OnModelSystemExit -= (Action)d;
                }
                this.OnModelSystemExit = null;
            }
        }

        public IClient RetriveCurrentNetworkingClient()
        {
            return this.CurrentClient;
        }

        public void Save()
        {
            this.SaveConfiguration(this.ConfigurationFileName);
        }

        public bool SetProjectDirectory(string dir, ref string error)
        {
            if (!this.ValidateProjectDirectory(dir, ref error))
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
            this.ProjectDirectory = dir;
            return true;
        }

        public bool SetModelSystemDirectory(string dir, ref string error)
        {
            if (!this.ValidateProjectDirectory(dir, ref error))
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
            this.ModelSystemDirectory = dir;
            return true;
        }

        public bool StartupNetworkingClient(out Networking.IClient networkingClient, ref string error)
        {
            networkingClient = null;
            lock (this)
            {
                Thread.MemoryBarrier();
                if (this.CurrentClient != null)
                {
                    networkingClient = this.CurrentClient;
                    return true;
                }
                else
                {
                    try
                    {
                        this.CurrentClient = new Client(this.RemoteServerAddress, this.RemoteServerPort, this);
                    }
                    catch
                    {
                        return false;
                    }
                    Thread.MemoryBarrier();
                    networkingClient = this.CurrentClient;
                    return true;
                }
            }
        }

        public bool StartupNetworkingHost(out Networking.IHost networkingHost, ref string error)
        {
            networkingHost = null;
            lock (this)
            {
                Thread.MemoryBarrier();
                if (this.CurrentHost == null || this.CurrentHost.IsShutdown)
                {
                    try
                    {
                        this.CurrentHost = new Host(this);
                    }
                    catch
                    {
                        return false;
                    }
                }
                else
                {
                    ((Host)this.CurrentHost).ReleaseRegisteredHandlers();
                }
                Thread.MemoryBarrier();
                networkingHost = this.CurrentHost;
            }
            return true;
        }

        public void UpdateProgressReportColour(string name, Tuple<byte, byte, byte> c)
        {
            lock (this)
            {
                foreach (var report in this.ProgressReports)
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

        private void LoadAssembly(Assembly assembly)
        {
            Type module = typeof(IModule);
            Type modelSystem = typeof(IModelSystemTemplate);
            var types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                // Make sure that they are valid types
                //this.StoreForLookup(type, assembly);
                if (type.IsAbstract | type.IsNotPublic | !(type.IsClass | type.IsValueType)) continue;
                if (module.IsAssignableFrom(type))
                {
                    string error = null;
                    if (CheckTypeForErrors(type, ref error))
                    {
                        LoadError = error;
                    }
                    // we know then that this is an IModel
                    lock (this.ModelRepository)
                    {
                        this.ModelRepository.AddModule(type);
                    }
                    if (modelSystem.IsAssignableFrom(type))
                    {
                        lock (this.ModelSystemTemplateRepository)
                        {
                            this.ModelSystemTemplateRepository.Add(type);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks to see if the module violates any rules
        /// </summary>
        /// <param name="type">The type to process</param>
        /// <param name="error">A message describing the error.</param>
        /// <returns>True if there is an error.</returns>
        private bool CheckTypeForErrors(Type type, ref string error)
        {
            return 
                CheckForParameterDelcarationErrors(type, ref error) ||
                CheckForNonPublicRootAndParentTags(type, ref error);
        }

 
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
                           where t.GetAttributes().Any(o => (o is RootModule) || (o is ParentModel)) && !t.IsPublic
                           select t;
            var firstFailure = failures.FirstOrDefault();
            if(firstFailure != null)
            {
                error = "When analyzing the type '" + type.FullName + "' the member '" + firstFailure.Name 
                    + "' used a header (RootModule/ParentModel) to get a value from XTMF however it is not public.  This violates the XTMF coding conventions, and will not work as expected."
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

        private void LoadConfigurationFile(string configFileName)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(configFileName);
            var root = doc["Root"];
            if (root == null || !root.HasChildNodes)
            {
                SaveConfiguration(configFileName);
                return;
            }

            foreach (XmlNode child in root.ChildNodes)
            {
                switch (child.Name)
                {
                    case "ProjectDirectory":
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                var dir = attribute.InnerText;
                                string error = null;
                                this.SetProjectDirectory(dir, ref error);
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
                                this.SetModelSystemDirectory(dir, ref error);
                            }
                        }
                        break;
                    case "AutoSave":
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                var booleanText = attribute.InnerText;
                                bool b;
                                if (bool.TryParse(booleanText, out b))
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
                                int portNumber;
                                if (int.TryParse(booleanText, out portNumber))
                                {
                                    _HostPort = portNumber;
                                }
                            }
                        }
                        break;
                    default:
                        {
                            var attribute = child.Attributes["Value"];
                            if (attribute != null)
                            {
                                this.AdditionalSettings[child.Name] = attribute.InnerText;
                            }
                        }
                        break;
                }
            }
        }

        private void LoadModules()
        {
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            // Load the given base assembly
            if (this.BaseAssembly != null)
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
            if (Directory.Exists(this.ModuleDirectory))
            {
                var files = Directory.GetFiles(this.ModuleDirectory, "*.dll");
                Parallel.For(0, files.Length,
                    (int i) =>
                    {
                        try
                        {
                            this.LoadAssembly((Assembly.Load(Path.GetFileNameWithoutExtension(files[i]))));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error when trying to load assembly '" + Path.GetFileNameWithoutExtension(files[i]) + "'");
                            Console.WriteLine(e.ToString());
                        }
                    });
            }
            watch.Stop();
        }

        private void LoadUserConfiguration(string configFile)
        {
            this.ConfigurationFileName = configFile;
            var directory = this.ConfigurationDirectory;
            var defaultProjectDirectory =
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "XTMF", "Projects");
            this.ProjectDirectory = defaultProjectDirectory;
            this.AutoSave = true;
            var msDir = Path.Combine(directory, "ModelSystems");
            if (!Directory.Exists(msDir))
            {
                Directory.CreateDirectory(msDir);
            }
            this.ModelSystemDirectory = msDir;
            this.AdditionalSettings["UseGlass"] = "false";
            this.AdditionalSettings["EditProjects"] = "false";
            if (!File.Exists(configFile))
            {
                this.SaveConfiguration(configFile);
            }
            else
            {
                this.LoadConfigurationFile(configFile);
            }
        }

        private void SaveConfiguration(string configFileName)
        {
            using (XmlWriter writer = XmlTextWriter.Create(configFileName, new XmlWriterSettings() { Encoding = Encoding.Unicode }))
            {
                // Start the document and create the default root node
                writer.WriteStartDocument(true);
                writer.WriteStartElement("Root");
                // Now that we have it all started we can go and write in all of the setting that we are going to need
                writer.WriteStartElement("ProjectDirectory");
                writer.WriteAttributeString("Value", this.ProjectDirectory);
                writer.WriteEndElement();

                writer.WriteStartElement("ModelSystemDirectory");
                writer.WriteAttributeString("Value", this.ModelSystemDirectory);
                writer.WriteEndElement();
                // Auto Save
                writer.WriteStartElement("AutoSave");
                writer.WriteAttributeString("Value", this.AutoSave.ToString());
                writer.WriteEndElement();
                // Host Port
                writer.WriteStartElement("HostPort");
                writer.WriteAttributeString("Value", HostPort.ToString());
                writer.WriteEndElement();

                if (this.AdditionalSettings != null)
                {
                    if (this.AdditionalSettings.Count > 0)
                    {
                        foreach (var setting in this.AdditionalSettings)
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
                //Finished writing all of the settings so we can finish the document now
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private class ProgressReport : IProgressReport
        {
            public Tuple<byte, byte, byte> Colour
            {
                get;
                set;
            }

            public Func<float> GetProgress
            {
                get;
                internal set;
            }

            public string Name
            {
                get;
                internal set;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool all)
        {
            if (this.CurrentHost != null)
            {
                var disp = this.CurrentHost as IDisposable;
                if (disp != null)
                {
                    disp.Dispose();
                }
                this.CurrentHost = null;
            }
            if (this.CurrentClient != null)
            {
                var disp = this.CurrentClient as IDisposable;
                if (disp != null)
                {
                    disp.Dispose();
                }
                this.CurrentClient = null;
            }
        }
    }
}