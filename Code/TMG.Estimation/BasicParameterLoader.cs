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
using XTMF;
using System.Xml;
using TMG.Input;
using System.IO;

namespace TMG.Estimation
{
    public class BasicParameterLoader : IDataSource<List<ParameterSetting>>
    {
        List<ParameterSetting> Parameters;

        [SubModelInformation(Required = true, Description = "The location of the parameter file.")]
        public FileLocation ParameterFileLocation;

        public List<ParameterSetting> GiveData()
        {
            return Parameters;
        }

        public bool Loaded
        {
            get { return Parameters != null; }
        }

        public void LoadData()
        {

            if(!File.Exists(ParameterFileLocation.GetFilePath()))
            {
                throw new XTMFRuntimeException(this, $"In {Name} the parameter file '{Path.GetFullPath(ParameterFileLocation.GetFilePath())}' does not exist!");
            }
            XmlDocument doc = new XmlDocument();
            doc.Load( ParameterFileLocation.GetFilePath() );
            List<ParameterSetting> parameters = [];
            var root = doc["Root"];
            if (root == null)
            {
                throw new XTMFRuntimeException(this, $"In {Name} the parameter file '{Path.GetFullPath(ParameterFileLocation.GetFilePath())}' contained an invalid parameter file!");

            }
            foreach ( XmlNode child in root.ChildNodes )
            {
                if ( child.Name == "Parameter" )
                {
                    ParameterSetting current = new ParameterSetting();
                    if (child.HasChildNodes)
                    {
                        var nodes = child.ChildNodes;
                        current.Names = new string[nodes.Count];
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            XmlNode name = nodes[i];
                            var parameterPath = name.Attributes?["ParameterPath"]?.InnerText;
                            current.Names[i] = parameterPath ?? throw new XTMFRuntimeException(this, $"In {Name} Parameter Path was not defined in {child.OuterXml}!");
                        }
                    }
                    else
                    {
                        var parameterAttribute = child.Attributes?["ParameterPath"];
                        if (parameterAttribute == null)
                        {
                            throw new XTMFRuntimeException(this, $"In {Name} ParameterPath was not defined in {child.OuterXml}!");
                        }
                        var parameterPath = parameterAttribute.InnerText;
                        current.Names = new[] { parameterPath };
                    }
                    var minimumAttribute = child.Attributes?["Minimum"];
                    var maximumAttribute = child.Attributes?["Maximum"];
                    if (minimumAttribute == null)
                    {
                        throw new XTMFRuntimeException(this, $"In {Name} The Minimum attribute was not defined in {child.OuterXml}!");
                    }
                    if (maximumAttribute == null)
                    {
                        throw new XTMFRuntimeException(this, $"In {Name} The Maximum attribute was not defined in {child.OuterXml}!");
                    }
                    current.Minimum = float.Parse( minimumAttribute.InnerText);
                    current.Maximum = float.Parse( maximumAttribute.InnerText);
                    current.Current = current.Minimum;
                    XmlAttribute nullHypothesis;
                    if ( ( nullHypothesis = child.Attributes["NullHypothesis"] ) != null )
                    {
                        current.NullHypothesis = float.Parse( nullHypothesis.InnerText );
                    }
                    parameters.Add( current );
                }
            }
            Parameters = parameters;
        }

        public void UnloadData()
        {
            Parameters = null;
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
