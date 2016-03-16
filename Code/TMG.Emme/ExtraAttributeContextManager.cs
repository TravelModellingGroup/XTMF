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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;

namespace TMG.Emme
{

    public class ExtraAttributeData : IModule
    {
        [Parameter("Domain", "NODE", "Extra attribute domain (type). Accepted values are NODE, LINK, TURN, TRANSIT_LINE, or TRANSIT_SEGMENT")]
        public string Domain;

        [Parameter("Id", "@node1", "The 6-character ID string of the extra attribute, including the '@' symbol.")]
        public string Id;

        [Parameter("Default", 0.0f, "The default value for the extra attribute.")]
        public float DefaultValue;

        private HashSet<string> _AllowedDomains;

        public ExtraAttributeData()
        {
            this._AllowedDomains = new HashSet<string>();
            this._AllowedDomains.Add("NODE");
            this._AllowedDomains.Add("LINK");
            this._AllowedDomains.Add("TURN");
            this._AllowedDomains.Add("TRANSIT_LINE");
            this._AllowedDomains.Add("TRANSIT_SEGMENT");
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 1.0f; }
        }

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!this._AllowedDomains.Contains(this.Domain))
            {
                error = "Domain '" + this.Domain + "' is not supported. It must be one of NODE, LINK, TURN, TRANSIT_LINE, or TRANSIT_SEGMENT.";
                return false;
            }
            if (!this.Id.StartsWith("@"))
            {
                error = "Extra attribute ID must begin with the '@' symbol (current value is '" + this.Id + "').";
                return false;
            }

            return true;
        }
    }

    public class ExtraAttributeContextManager : IEmmeTool
    {

        [RunParameter("Scenario", 0, "The number of the Emme scenario.")]
        public int ScenarioNumber;

        [Parameter("Delete Flag", true, "Tf set to true, this module will delete the attributes after running. Otherwise, this module will just ensure that the attributes exist and are initialized.")]
        public bool DeleteFlag;

        [Parameter("Reset To Default", false, "Reset the attributes to their default values if they already exist.")]
        public bool ResetToDefault;

        [SubModelInformation(Description="Attribute to create.")]
        public List<ExtraAttributeData> AttributesToCreate;

        [SubModelInformation(Description="Emme tools to run")]
        public List<IEmmeTool> EmmeToolsToRun;

        private const string _ToolName = "tmg.XTMF_internal.temp_attribute_manager";
        private IEmmeTool _RunningTool;
        private float _progress;

        public bool Execute(Controller controller)
        {
            this._progress = 0.0f;
            this._RunningTool = null;

            if (this.EmmeToolsToRun.Count == 0) return true;

            var mc = controller as ModellerController;
            if (mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            float numberOfTasks = (float) (this.AttributesToCreate.Count + 1);
            bool[] createdAttributes = new bool[this.AttributesToCreate.Count];

            /*
            def __call__(self, xtmf_ScenarioNumber, xtmf_AttributeId, xtmf_AttributeDomain, 
                 xtmf_AttributeDefault, xtmf_DeleteFlag):
            */

            try
            {
                for (int i = 0; i < createdAttributes.Length; i++)
                {
                    var attData = this.AttributesToCreate[i];
                    var args = string.Join(" ", this.ScenarioNumber, attData.Id, attData.Domain, attData.DefaultValue, false, ResetToDefault);
                    createdAttributes[i] = mc.Run(_ToolName, args);
                    this._progress = (float)( i /  createdAttributes.Length / numberOfTasks);
                }
                this._progress = 1.0f / numberOfTasks;

                foreach (var tool in this.EmmeToolsToRun)
                {
                    tool.Execute(mc);
                    this._progress += 1.0f / numberOfTasks;
                }
            }
            finally
            {
                if (this.DeleteFlag)
                {
                    for (int i = 0; i < createdAttributes.Length; i++)
                    {
                        if (createdAttributes[i])
                        {
                            var attData = this.AttributesToCreate[i];
                            var args = string.Join(" ", this.ScenarioNumber, attData.Id, attData.Domain, attData.DefaultValue, true, ResetToDefault);
                            mc.Run(_ToolName, args);
                        }
                    }
                }
            }

            return true;
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get
            {
                if (this._RunningTool != null)
                {
                    return this._progress + (float)(this._RunningTool.Progress / (this.EmmeToolsToRun.Count));
                } else 
                {
                    return this._progress;
                }
            }
        }

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if(EmmeToolsToRun.Count == 0 && AttributesToCreate.Count > 0)
            {
                error = "In '"+Name+"' you must have at least one tool to run in order for ExtraAttributeContextManager to function properly.";
                return false;
            }
            return true;
        }
    }
}

