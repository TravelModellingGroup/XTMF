/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XTMF.Editing;
using XTMF.Interfaces;

namespace XTMF;

public sealed class ModelSystemEditingSession : IDisposable
{
    private static readonly object _RunningLock = new();

    private List<XTMFCommand> _CombinedCommands;

    private volatile bool _InCombinedContext;

    private readonly ModelSystem _ModelSystem;

    private int _ModelSystemIndex;

    /// <summary>
    ///     The command stack that commands that have been undone will go on
    /// </summary>
    private readonly EditingStack _RedoStack = new(100);

    /// <summary>
    ///     The runtime we are in
    /// </summary>
    public XTMFRuntime Runtime { get; private set; }

    private readonly Semaphore _saveSemaphor;

    public List<string> GetPreviousRunNames()
    {
        var pes = ProjectEditingSession;
        if (pes != null)
        {
            return pes.GetPreviousRunPaths()
                .AsParallel()
                .Select(r => new DirectoryInfo(r))
                .OrderByDescending(r => r.LastWriteTime)
                .Select(r => r.Name)
                .ToList();
        }
        return [];
    }
    
    public Task<List<string>> GetPreviousRunNamesAsync()
    {
        return new Task<List<string>>(GetPreviousRunNames);
    }

    private readonly object _SessionLock = new();

    /// <summary>
    ///     The command stack that contains commands that have been done will go on
    /// </summary>
    private readonly EditingStack _UndoStack = new(100);

    /// <summary>
    ///     Create a new session to edit a model system
    /// </summary>
    /// <param name="modelSystem">The model system to edit</param>
    public ModelSystemEditingSession(XTMFRuntime runtime, ModelSystem modelSystem)
    {
        Runtime = runtime;
        _ModelSystem = modelSystem;
        ModelSystemModel = new ModelSystemModel(this, modelSystem);
        ModelSystemModel.PropertyChanged += ModelSystemModel_PropertyChanged;
        _saveSemaphor = new Semaphore(1, 1);
    }

    /// <summary>
    ///     Create a new editing session to edit a project's model system
    /// </summary>
    /// <param name="ProjectEditingSession">The editing session to read from</param>
    /// <param name="modelSystemIndex">The model system's index inside of the project</param>
    public ModelSystemEditingSession(XTMFRuntime runtime, ProjectEditingSession ProjectEditingSession,
        int modelSystemIndex)
    {
        Runtime = runtime;
        this.ProjectEditingSession = ProjectEditingSession;
        _ModelSystemIndex = modelSystemIndex;
        Reload();
        _saveSemaphor = new Semaphore(1, 1);
    }

    /// <summary>
    ///     Create a model editing session for a previous run.  This will be read-only!
    /// </summary>
    /// <param name="runtime">A link to the XTMFRuntime</param>
    /// <param name="projectSession">The project this is for.</param>
    /// <param name="runFile">The location of the previous run.</param>
    public ModelSystemEditingSession(XTMFRuntime runtime, ProjectEditingSession projectSession, string runFile)
    {
        Runtime = runtime;
        ProjectEditingSession = projectSession;
        _ModelSystemIndex = -1;
        ModelSystemModel = new ModelSystemModel(Runtime, this, projectSession.Project, runFile);
    }

    public ProjectEditingSession ProjectEditingSession { get; }


    /// <summary>
    ///     Lets you know if the model system is able to run.
    /// </summary>
    /// <returns>If you can run this model system.</returns>
    public bool CanRun => EditingProject;

    /// <summary>
    ///     Is the session editing a model system inside of a project?
    /// </summary>
    /// <returns>If it is a project, true.</returns>
    public bool EditingProject => ProjectEditingSession != null;

    /// <summary>
    ///     Is the session editing a model system that is not in a project?
    /// </summary>
    /// <returns>If it is not a project, true.</returns>
    public bool EditingModelSystem => _ModelSystem != null;

    /// <summary>
    ///     The model system's model that we will interact with.
    /// </summary>
    public ModelSystemModel ModelSystemModel { get; private set; }

    public IConfiguration Configuration => Runtime.Configuration;

    /// <summary>
    ///     Contains if the model system has changed since the last save.
    /// </summary>
    public bool HasChanged { get; set; }

    /// <summary>
    ///     This will return true if closing a window will terminate this session.
    /// </summary>
    public bool CloseWillTerminate
    {
        get
        {
            lock (_SessionLock)
            {
                if (EditingModelSystem)
                {
                    return Runtime.ModelSystemController.WillCloseTerminate(this);
                }

                return ProjectEditingSession.WillCloseTerminate(_ModelSystemIndex);
            }
        }
    }

    public string Name => ModelSystemModel.Name;

    public bool CanUndo
    {
        get
        {
            lock (_SessionLock)
            {
                return _UndoStack.Count > 0;
            }
        }
    }

    public bool CanRedo
    {
        get
        {
            lock (_SessionLock)
            {
                return _RedoStack.Count > 0;
            }
        }
    }

    public string PreviousRunName { get; set; }

    public void Dispose()
    {
        Dispose(false);
    }

    /// <summary>
    ///     Get the available types for the selected module
    /// </summary>
    /// <param name="selectedModule">The module to find the valid types for</param>
    /// <returns>A list of the allowed types.</returns>
    public List<Type> GetValidModules(ModelSystemStructureModel selectedModule)
    {
        return selectedModule.RealModelSystemStructure.GetPossibleModules(ModelSystemModel.Root
            .RealModelSystemStructure);
    }

    public List<XTMFCommand> CopyOnUndo()
    {
        return [.. _UndoStack];
    }

    public List<XTMFCommand> CopyOnRedo()
    {
        return [.. _RedoStack];
    }

    /// <summary>
    ///     This event fires when the project containing this model system
    ///     was saved externally
    /// </summary>
    public event EventHandler<ProjectWasExternallySavedEventArgs> ProjectWasExternallySaved;

    public event EventHandler Saved;

    internal void ProjectWasExternalSaved()
    {
        // we need to reload first so the GUI knows how to rebuild the display model.
        Reload();
        ProjectWasExternallySaved?.Invoke(this, new ProjectWasExternallySavedEventArgs(this.ModelSystemModel));
    }

    /// <summary>
    ///     This event occurs when no references are left to this editing session.
    /// </summary>
    public event EventHandler SessionClosed;

    public bool SaveWait()
    {
        return _saveSemaphor.WaitOne();
    }

    public void SaveRelease()
    {
        _saveSemaphor.Release(1);
    }

    private void ModelSystemModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender == ModelSystemModel)
        {
            switch (e.PropertyName)
            {
                case "Name":
                    NameChanged?.Invoke(this, e);
                    break;
                case "Root":
                    ProjectWasExternallySaved?.Invoke(this, new ProjectWasExternallySavedEventArgs(this.ModelSystemModel));
                    break;
            }

        }
    }

    /// <summary>
    ///     Rebuild the model system session by recreating the model system model
    /// </summary>
    private void Reload()
    {
        if (ModelSystemModel != null)
        {
            ModelSystemModel.PropertyChanged -= ModelSystemModel_PropertyChanged;
            _RedoStack.Clear();
            _UndoStack.Clear();
            HasChanged = false;
        }

        ModelSystemModel = new ModelSystemModel(this, ProjectEditingSession.Project, _ModelSystemIndex);
        ModelSystemModel.PropertyChanged += ModelSystemModel_PropertyChanged;
    }

    internal bool IsEditing(IModelSystemStructure root)
    {
        return EditingProject && ProjectEditingSession.Project.ModelSystemStructure[_ModelSystemIndex] == root;
    }

    /// <summary>
    ///     Run the model if it is contained inside of a project.
    /// </summary>
    /// <param name="runName"></param>
    /// <param name="error">A message in case of error</param>
    /// <param name="executeNow">Should the run start executing now or wait for all other runs to finish first?</param>
    /// <param name="forceLocal">Execute in the local process, overriding default behaviour.</param>
    /// <returns>A reference to the run.</returns>
    public XTMFRun Run(string runName, ref string error, bool overwrite = false, bool executeNow = true,
        bool forceLocal = false)
    {
        // this needs to block as if a command is running
        lock (_SessionLock)
        {
            if (!CanRun)
            {
                error = "You can not run this model system.";
                return null;
            }

            lock (_RunningLock)
            {
                XTMFRun run;
                if (_ModelSystemIndex >= 0)
                {
                    var cloneProject = ProjectEditingSession.CreateCloneProject(ProjectEditingSession.Project);
                    cloneProject.SetModelSystem(
                        _ModelSystemIndex,
                        ModelSystemModel.Root.RealModelSystemStructure,
                        ModelSystemModel.LinkedParameters.GetRealLinkedParameters(),
                        [],
                         ModelSystemModel.Description
                        );
                    if (((Configuration)Configuration).RemoteHost)
                    {
                        run = XTMFRun.CreateRemoteHost(cloneProject, _ModelSystemIndex, ModelSystemModel,
                            Runtime.Configuration, runName, overwrite);
                    }
                    else
                    {
                        run =
                            Debugger.IsAttached || forceLocal ||
                            !((Configuration)Configuration).RunInSeperateProcess
                                ? XTMFRun.CreateLocalRun(cloneProject, _ModelSystemIndex, ModelSystemModel,
                                    Runtime.Configuration, runName, overwrite)
                                : XTMFRun.CreateRemoteHost(cloneProject, _ModelSystemIndex, ModelSystemModel,
                                    Runtime.Configuration, runName, overwrite);
                    }
                    run.ProjectSavedByRun += (theRun, newMSS) =>
                    {
                        string e = null;
                        ProjectEditingSession.Project.UpdateModelSystemStructure(_ModelSystemIndex, newMSS);
                        ProjectEditingSession.Project.Save(ref e);
                        Reload();
                        ProjectWasExternallySaved?.Invoke(this, new ProjectWasExternallySavedEventArgs(ModelSystemModel));
                    };
                }
                else
                {
                    run = XTMFRun.CreateLocalRun(ProjectEditingSession.Project, ModelSystemModel,
                        Runtime.Configuration, runName, overwrite);
                }
                return run;
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="run"></param>
    /// <param name="executeNow"></param>
    public void ExecuteRun(XTMFRun run, bool executeNow)
    {
        Runtime.RunController.ExecuteRun(run, executeNow);
    }

    /// <summary>
    /// Reorders a queued run - inserts the passed run into the new Queue index.
    /// </summary>
    /// <param name="run"></param>
    /// <param name="newQueueIndex"></param>
    public void ReorderQueuedRun(XTMFRun run, int newQueueIndex)
    {
        Runtime.RunController.ReorderQueuedRun(run,newQueueIndex);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="run"></param>
    /// <param name="delayedStartTime"></param>
    public void ExecuteDelayedRun(XTMFRun run, DateTime delayedStartTime)
    {
        Runtime.RunController.ExecuteDelayedRun(run, delayedStartTime);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    internal bool SaveAsModelSystem(string name, ref string error)
    {
        lock (_SessionLock)
        {
            var ms = Runtime.ModelSystemController.LoadOrCreate(name);
            ms.Name = name;
            ms.Description = ModelSystemModel.Description;
            if (EditingProject)
            {
                ms.ModelSystemStructure =
                    ProjectEditingSession.CloneModelSystemStructure(out var lp, out List<IRegionDisplay> regionDisplays, _ModelSystemIndex);
                ms.LinkedParameters = lp;
                ms.RegionDisplays = regionDisplays;
                return ms.Save(ref error);
            }

            ms.ModelSystemStructure = ModelSystemModel.Root.RealModelSystemStructure;
            ms.LinkedParameters = ModelSystemModel.LinkedParameters.LinkedParameters
                .Select(lpm => (ILinkedParameter)lpm.RealLinkedParameter).ToList();
            using var otherSession = Runtime.ModelSystemController.EditModelSystem(ms);
            return otherSession.Save(ref error);
        }
    }

    public event PropertyChangedEventHandler NameChanged;

    /// <summary>
    ///     Save the session.
    /// </summary>
    /// <param name="error">If there is an error it will provide a message as to why.</param>
    /// <returns>If we were able to save or not.</returns>
    public bool Save(ref string error)
    {
        lock (_SessionLock)
        {
            if (!ModelSystemModel.Save(ref error))
            {
                return false;
            }

            Saved?.Invoke(this, new EventArgs());
            HasChanged = false;
            return true;
        }
    }

    public void ExecuteCombinedCommands(string name, Action combinedCommandContext)
    {
        lock (_SessionLock)
        {
            _InCombinedContext = true;
            var list = _CombinedCommands = [];
            combinedCommandContext();
            // only add to the undo list if a command was added successfully
            if (list.Count > 0)
            {
                // create a command to undo everything in a single shot [do is empty]
                _UndoStack.Add(XTMFCommand.CreateCommand(name, (ref string error) => { return true; },
                    (ref string error) =>
                    {
                        foreach (var command in ((IEnumerable<XTMFCommand>)list).Reverse())
                        {
                            if (command.CanUndo())
                            {
                                if (!command.Undo(ref error))
                                {
                                    return false;
                                }
                            }
                        }

                        return true;
                    },
                    (ref string error) =>
                    {
                        foreach (var command in list)
                        {
                            if (command.CanUndo())
                            {
                                if (!command.Redo(ref error))
                                {
                                    return false;
                                }
                            }
                        }

                        return true;
                    }));
                CommandExecuted?.Invoke(this, new EventArgs());
            }

            _InCombinedContext = false;
            _CombinedCommands = null;
        }
    }

    public bool RunCommand(XTMFCommand command, ref string error)
    {
        lock (_SessionLock)
        {
            if (command.Do(ref error))
            {
                HasChanged = true;
                if (_InCombinedContext)
                {
                    var list = _CombinedCommands;
                    list.Add(command);
                }
                else
                {
                    if (command.CanUndo())
                    {
                        _UndoStack.Add(command);
                    }

                    CommandExecuted?.Invoke(this, new EventArgs());
                }

                // if we do something new, redo no long is available
                _RedoStack.Clear();
                return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     Checks to see if the run directory already exists with the given name
    /// </summary>
    /// <param name="runName">The run name to check</param>
    /// <returns>True if the directory already exists, false otherwise</returns>
    public bool RunNameExists(string runName)
    {
        if (!EditingProject)
        {
            throw new InvalidOperationException(
                "You can only call RunNameExists when it is editing a project's model system.");
        }

        return ProjectEditingSession.RunNameExists(runName);
    }

    /// <summary>
    /// Checks for validitiy of the passed run name
    /// </summary>
    /// <param name="runName"></param>
    /// <returns></returns>
    public bool IsValidRunName(string runName)
    {
        return ProjectEditingSession.IsValidRunName(runName);
    }

    /// <summary>
    ///     This event occurs whenever a command is executed, undone, or redone
    /// </summary>
    public event EventHandler CommandExecuted;

    public bool SaveAs(string modelSystemName, ref string error)
    {
        lock (_SessionLock)
        {
            return ProjectEditingSession.CloneModelSystemAs(Runtime,
                ModelSystemModel.Root.RealModelSystemStructure,
                ModelSystemModel.LinkedParameters.GetRealLinkedParameters(),
                ModelSystemModel.Description, modelSystemName, ref error);
        }
    }

    /// <summary>
    ///     Undo the last command
    /// </summary>
    /// <param name="error">The reason it failed</param>
    public bool Undo(ref string error)
    {
        lock (_SessionLock)
        {
            if (_UndoStack.TryPop(out var command))
            {
                if (command != null)
                {
                    if (command.Undo(ref error))
                    {
                        HasChanged = true;
                        _RedoStack.Add(command);
                        CommandExecuted?.Invoke(this, new EventArgs());
                        return true;
                    }

                    return false;
                }
            }

            error = "There was nothing to undo.";
            return false;
        }
    }

    /// <summary>
    ///     Redo the last undone command
    /// </summary>
    /// <param name="error">The reason why it failed</param>
    public bool Redo(ref string error)
    {
        lock (_SessionLock)
        {
            if (_RedoStack.TryPop(out var command))
            {
                if (command != null)
                {
                    if (command.Redo(ref error))
                    {
                        HasChanged = true;
                        _UndoStack.Add(command);
                        CommandExecuted?.Invoke(this, new EventArgs());
                        return true;
                    }

                    return false;
                }
            }

            error = "There was nothing to redo.";
            return false;
        }
    }

    /// <summary>
    ///     Close the editing session
    /// </summary>
    /// <param name="error">The reason why we were unable to close.</param>
    /// <returns>True if the model system is saved.</returns>
    public bool Close(ref string error)
    {
        lock (_SessionLock)
        {
            if (ModelSystemModel.IsDirty)
            {
                error = "The project has changed and has not been saved.";
                return false;
            }

            Dispose();
            return true;
        }
    }

    /// <summary>
    ///     Update the model system index.
    ///     This should be called when a model system has been deleted from a project
    /// </summary>
    /// <param name="newIndex">The new index of this model system</param>
    internal void SetModelSystemIndex(int newIndex)
    {
        _ModelSystemIndex = newIndex;
    }

    /// <summary>
    ///     Forces the session to terminate, even if we will lose data.
    /// </summary>
    private void ForceClose()
    {
        lock (_SessionLock)
        {
            if (EditingModelSystem)
            {
                Runtime.ModelSystemController.ReleaseEditingSession(this);
            }
            else
            {
                ProjectEditingSession.ModelSystemEditingSessionClosed(this, _ModelSystemIndex);
            }
        }
    }

    /// <summary>
    ///     Check to see if we are editing the given model system
    /// </summary>
    /// <param name="modelSystem"></param>
    /// <returns></returns>
    internal bool IsEditing(ModelSystem modelSystem)
    {
        return _ModelSystem == modelSystem;
    }

    /// <summary>
    ///     This gets called after a session has been terminated by either
    ///     the modeller controller or the project session.
    /// </summary>
    internal void SessionTerminated()
    {
        SessionClosed?.Invoke(this, new EventArgs());
    }

    private void Dispose(bool destructor)
    {
        ForceClose();
    }

    ~ModelSystemEditingSession()
    {
        _saveSemaphor?.Dispose();
    }

    public ModelSystemStructureModel GetRoot(ModelSystemStructureModel currentModule)
    {
        return ModelSystemModel.GetModelFor(
            currentModule.RealModelSystemStructure.GetRoot(ModelSystemModel.Root.RealModelSystemStructure));
    }

    public ModelSystemStructureModel GetRoot(ModelSystemStructure currentModule)
    {
        return ModelSystemModel.GetModelFor(currentModule.GetRoot(ModelSystemModel.Root.RealModelSystemStructure));
    }

    public ModelSystemStructureModel GetParent(ModelSystemStructureModel currentModule)
    {
        return ModelSystemModel.GetModelFor(
            currentModule.RealModelSystemStructure.GetParent(ModelSystemModel.Root.RealModelSystemStructure));
    }

    public ModelSystemStructureModel GetModelSystemStructureModel(ModelSystemStructure modelSystemStructure)
    {
        return ModelSystemModel.GetModelFor(modelSystemStructure);
    }

    public ICollection<Type> GetValidGenericVariableTypes(Type[] conditions)
    {
        return ((Configuration)Configuration).GetValidGenericVariableTypes(conditions);
    }

    public void CancelRun(XTMFRun run)
    {
        Runtime.RunController.CancelRun(run);
    }

    public bool ReloadModelSystem(ref string error)
    {
        error = null;
        lock (_SessionLock)
        {
            if (ModelSystemModel.ReloadModelSystem(this, ref error))
            {
                _UndoStack.Clear();
                _RedoStack.Clear();
                return true;
            }
            error = "Feature is not available yet.";
            return false;
        }
    }

    /// <summary>
    /// Test to see if the model system is in the middle of saving.
    /// </summary>
    /// <returns>True if the model system session is currently saving.</returns>
    public bool IsSaving()
    {
        var gotLock = _saveSemaphor.WaitOne(0, false);
        if(gotLock)
        {
            _saveSemaphor.Release();
            return false;
        }
        return false;
    }
}


public class ProjectWasExternallySavedEventArgs : EventArgs
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="model"></param>
    public ProjectWasExternallySavedEventArgs(ModelSystemModel model)
    {
        this.ModelSystem = model;
    }

    public ModelSystemModel ModelSystem { get; private set; }
}