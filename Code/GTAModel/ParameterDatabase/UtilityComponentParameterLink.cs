/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using System.Linq;
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.ParameterDatabase
{
    [ModuleInformation(Description = "This module allows for linking to the Utility Components of IUtilityComponentModes."
    + "  The 'Mode Parameter Name' is the name of the parameter of the Utility component to bind to.")]
    public sealed class UtilityComponentParameterLink : ParameterLink
    {
        [RunParameter("Utility Component Name", "", "The name of the Utility Component to bind to.")]
        public string UtilityComponentName;

        protected override bool LinkModeParameter(ref string error)
        {
            // get the mode
            var mode = Parent.Mode as IUtilityComponentMode;
            if (mode == null)
            {
                error = "In '" + Parent.Name + "' it failed to present an IUtilityComponentMode mode for '" + Name + "'!";
                return false;
            }
            // get the Util Component
            var utilComponent = mode.UtilityComponents.FirstOrDefault(uc => uc.UtilityComponentName == UtilityComponentName);
            if (utilComponent == null)
            {
                error = "In '" + mode.ModeName + "' we were unable to find a Utility Component with the Utility Component Name '" + UtilityComponentName + "'.";
                return false;
            }
            AssignTo = utilComponent;
            var moduleType = utilComponent.GetType();
            var parameterType = typeof(ParameterAttribute);
            foreach (var field in moduleType.GetFields())
            {
                var attributes = field.GetCustomAttributes(parameterType, true);
                for (int i = 0; i < attributes.Length; i++)
                {
                    if (((ParameterAttribute)attributes[i]).Name == ModeParameterName)
                    {
                        Field = field;
                        return true;
                    }
                }
            }

            foreach (var field in moduleType.GetProperties())
            {
                var attributes = field.GetCustomAttributes(parameterType, true);
                for (int i = 0; i < attributes.Length; i++)
                {
                    if (((ParameterAttribute)attributes[i]).Name == ModeParameterName)
                    {
                        Property = field;
                        return true;
                    }
                }
            }
            error = "We were unable to find a parameter in the mode '" + mode.ModeName + "' in the utility component '" + utilComponent.UtilityComponentName
                + "' called '" + ModeParameterName + "'!";
            return false;
        }
    }
}