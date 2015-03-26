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
using System.IO;
using System.Xml;
using TMG.Input;
namespace TMG.Estimation
{
    public class BasicParameterLoader : IDataSource<List<ParameterSetting>>
    {
        List<ParameterSetting> Parameters;

        [SubModelInformation(Required = true, Description = "The location of the parameter file.")]
        public FileLocation ParameterFileLocation;

        public List<ParameterSetting> GiveData()
        {
            return this.Parameters;
        }

        public bool Loaded
        {
            get { return this.Parameters != null; }
        }

        public void LoadData()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load( this.ParameterFileLocation.GetFilePath() );
            List<ParameterSetting> parameters = new List<ParameterSetting>();
            foreach ( XmlNode child in doc["Root"].ChildNodes )
            {
                if ( child.Name == "Parameter" )
                {
                    ParameterSetting current = new ParameterSetting();
                    current.Minimum = float.Parse( child.Attributes["Minimum"].InnerText );
                    current.Maximum = float.Parse( child.Attributes["Maximum"].InnerText );
                    current.Current = current.Minimum;
                    XmlAttribute nullHypothesis;
                    if ( ( nullHypothesis = child.Attributes["NullHypothesis"] ) != null )
                    {
                        current.NullHypothesis = float.Parse( nullHypothesis.InnerText );
                    }
                    if ( child.HasChildNodes )
                    {
                        var nodes = child.ChildNodes;
                        current.Names = new string[nodes.Count];
                        for ( int i = 0; i < nodes.Count; i++ )
                        {
                            XmlNode name = nodes[i];
                            var parameterPath = name.Attributes["ParameterPath"].InnerText;
                            current.Names[i] = parameterPath;
                        }
                    }
                    else
                    {
                        var parameterPath = child.Attributes["ParameterPath"].InnerText;
                        current.Names = new string[] { parameterPath };
                    }
                    parameters.Add( current );
                }
            }
            this.Parameters = parameters;
        }

        public void UnloadData()
        {
            this.Parameters = null;
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
