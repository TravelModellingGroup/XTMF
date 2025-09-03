/*
    Copyright 2014-2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using System.Resources;

namespace XTMF.Gui.UserControls;

/// <summary>
/// Static localization helper class for ModelSystemDisplay controls
/// </summary>
public static class ModelSystemDisplayLocalization
{
    private static ResourceManager ResManager = new ResourceManager("XTMF.Gui.Properties.Resources", typeof(ModelSystemDisplay).Assembly);

    // Tooltip strings
    public static string SaveModelSystemTooltip => ResManager.GetString("SaveModelSystemTooltip");
    public static string LinkedParametersTooltip => ResManager.GetString("LinkedParametersTooltip");
    public static string RunModelSystemTooltip => ResManager.GetString("RunModelSystemTooltip");
    public static string OpenProjectFolderTooltip => ResManager.GetString("OpenProjectFolderTooltip");
    public static string ReloadModelSystemTooltip => ResManager.GetString("ReloadModelSystemTooltip");
    public static string SearchModulesTooltip => ResManager.GetString("SearchModulesTooltip");
    public static string ToggleQuickParametersTooltip => ResManager.GetString("ToggleQuickParametersTooltip");
    public static string ToggleModuleParametersTooltip => ResManager.GetString("ToggleModuleParametersTooltip");
    public static string FilterParametersTooltip => ResManager.GetString("FilterParametersTooltip");
    public static string FilterQuickParametersTooltip => ResManager.GetString("FilterQuickParametersTooltip");
    public static string QuickParameterTooltip => ResManager.GetString("QuickParameterTooltip");
    public static string SystemParameterTooltip => ResManager.GetString("SystemParameterTooltip");
    public static string AssociatedModuleDisabledTooltip => ResManager.GetString("AssociatedModuleDisabledTooltip");
    public static string DisabledModulesTooltip => ResManager.GetString("DisabledModulesTooltip");
    public static string RemoveModuleFromList => ResManager.GetString("RemoveModuleFromList");

    // UI Labels
    public static string ModuleParametersLabel => ResManager.GetString("ModuleParametersLabel");
    public static string QuickParametersLabel => ResManager.GetString("QuickParametersLabel");
    public static string ModuleDescriptionLabel => ResManager.GetString("ModuleDescriptionLabel");
    public static string NoDescriptionAvailable => ResManager.GetString("NoDescriptionAvailable");
    public static string NoModuleSelected => ResManager.GetString("NoModuleSelected");
    public static string ModelSystemRequirements => ResManager.GetString("ModelSystemRequirements");
    public static string NoModuleCurrentlySelected => ResManager.GetString("NoModuleCurrentlySelected");
    public static string EnableModule => ResManager.GetString("EnableModule");
    public static string DisableModule => ResManager.GetString("DisableModule");
    public static string NoRequiredModulesMissingLabel => ResManager.GetString("NoRequiredModulesMissingLabel");
    public static string MissingRequiredModulesLabel => ResManager.GetString("MissingRequiredModulesLabel");
    public static string DisabledModulesLabel => ResManager.GetString("DisabledModulesLabel");

    // Parameter Context Menu Items
    public static string ParameterMenuItem => ResManager.GetString("ParameterMenuItem");
    public static string CopyParameterMenuItem => ResManager.GetString("CopyParameterMenuItem");
    public static string RenameParameterMenuItem => ResManager.GetString("RenameParameterMenuItem");
    public static string ResetParameterNameMenuItem => ResManager.GetString("ResetParameterNameMenuItem");
    public static string HideParameterMenuItem => ResManager.GetString("HideParameterMenuItem");
    public static string ShowParameterMenuItem => ResManager.GetString("ShowParameterMenuItem");
    public static string CopyParametersToClipboardMenuItem => ResManager.GetString("CopyParametersToClipboardMenuItem");
    public static string PasteFromSpreadsheetMenuItem => ResManager.GetString("PasteFromSpreadsheetMenuItem");
    public static string OpenMenuItem => ResManager.GetString("OpenMenuItem");
    public static string OpenFileMenuItem => ResManager.GetString("OpenFileMenuItem");
    public static string OpenFileWithMenuItem => ResManager.GetString("OpenFileWithMenuItem");
    public static string OpenFileLocationMenuItem => ResManager.GetString("OpenFileLocationMenuItem");
    public static string SelectFileMenuItem => ResManager.GetString("SelectFileMenuItem");
    public static string SelectDirectoryMenuItem => ResManager.GetString("SelectDirectoryMenuItem");
    public static string AssignLinkedParameterMenuItem => ResManager.GetString("AssignLinkedParameterMenuItem");
    public static string RecentLinkedParametersMenuItem => ResManager.GetString("RecentLinkedParametersMenuItem");
    public static string RemoveFromLinkedParameterMenuItem => ResManager.GetString("RemoveFromLinkedParameterMenuItem");
    public static string ResetToDefaultMenuItem => ResManager.GetString("ResetToDefaultMenuItem");
    public static string CopyParameterNameMenuItem => ResManager.GetString("CopyParameterNameMenuItem");
    public static string GoToModuleMenuItem => ResManager.GetString("GoToModuleMenuItem");

    // Module Context Menu Items
    public static string CopyModuleMenuItem => ResManager.GetString("CopyModuleMenuItem");
    public static string CloneModuleMenuItem => ResManager.GetString("CloneModuleMenuItem");
    public static string PasteModuleMenuItem => ResManager.GetString("PasteModuleMenuItem");
    public static string RenameModuleMenuItem => ResManager.GetString("RenameModuleMenuItem");
    public static string EditDescriptionMenuItem => ResManager.GetString("EditDescriptionMenuItem");
    public static string MetaModuleMenuItem => ResManager.GetString("MetaModuleMenuItem");
    public static string ConvertToMetaModuleMenuItem => ResManager.GetString("ConvertToMetaModuleMenuItem");
    public static string SplitMetaModuleMenuItem => ResManager.GetString("SplitMetaModuleMenuItem");
    public static string ModuleMenuItem => ResManager.GetString("ModuleMenuItem");
    public static string DisableModuleMenuItem => ResManager.GetString("DisableModuleMenuItem");
    public static string RemoveModuleMenuItem => ResManager.GetString("RemoveModuleMenuItem");
    public static string HelpMenuItem => ResManager.GetString("HelpMenuItem");
    public static string MoveMenuItem => ResManager.GetString("MoveMenuItem");
    public static string MoveUpMenuItem => ResManager.GetString("MoveUpMenuItem");
    public static string MoveDownMenuItem => ResManager.GetString("MoveDownMenuItem");
    public static string LinkedParametersMenuItem => ResManager.GetString("LinkedParametersMenuItem");
    public static string ExpandAllModulesMenuItem => ResManager.GetString("ExpandAllModulesMenuItem");
    public static string CollapseAllModulesMenuItem => ResManager.GetString("CollapseAllModulesMenuItem");
}