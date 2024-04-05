/*
    Copyright 2015-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using XTMF;
namespace TMG.Emme.Tools.Analysis.Transit;

[ModuleInformation(Description =
    "For a given subgroup of transit lines, this tool constructs origin and destination vectors for combined walk - access and drive - access trips"
    )]
// ReSharper disable once InconsistentNaming
public class ExtractTransitODVectors : IEmmeTool
{

    [RunParameter("Scenario Number", 0, "The scenario numbers to run against.")]
    public int ScenarioNumber;

    [RunParameter("Line Filter Expression", "", "A filter to select what lines to include.")]
    public string LineFilterExpression;

    [RunParameter("Line OD Matrix Number", 0, "The matrix to save the OD demand using the lines.")]
    // ReSharper disable once InconsistentNaming
    public int LineODMatrixNumber;

    [RunParameter("Aggregation Origin Matrix Number", 0, "The origin vector to save the summed demand going through the filter.")]
    public int AggOriginMatrixNumber;

    [RunParameter("Aggregation Destination Matrix Number", 0, "The destination vector to save the summed demand going through the filter.")]
    public int AggDestinationMatrixNumber;

    [RunParameter("Auto OD Matrix Number", 0, "The auto OD demand matrix.")]
    // ReSharper disable once InconsistentNaming
    public int AutoODMatrixId;

    [RunParameter("Zone Centroids", "1-8999", typeof(RangeSet), "The regular TAZ zones to get the ODs for.")]
    public RangeSet ZoneCentroids;

    [RunParameter("Station Centroids", "9000-9999", typeof(RangeSet), "The zones that are used for access stations.")]
    public RangeSet StationCentroids;


    private const string ToolName = "tmg.analysis.transit.strategy_analysis.extract_transit_OD_vectors";

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public bool Execute(Controller controller)
    {
        var modeller = controller as ModellerController;
        if(modeller == null)
        {
            throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to run since the controller is not connected through modeller.");
        }
        return modeller.Run(this, ToolName, GetParameters());
    }

    private ModellerControllerParameter[] GetParameters()
    {
        /*
           def __call__(self, xtmf_ScenarioNumber, LineFilterExpression, xtmf_LineODMatrixNumber,
              xtmf_AggOriginMatrixNumber, xtmf_AggDestinationMatrixNumber, xtmf_AutoODMatrixId, xtmf_AccessStationRange, xtmf_ZoneCentroidRange):
        */
        return
        [
            new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
            new ModellerControllerParameter("LineFilterExpression", LineFilterExpression),
            new ModellerControllerParameter("xtmf_LineODMatrixNumber", LineODMatrixNumber.ToString()),
            new ModellerControllerParameter("xtmf_AggOriginMatrixNumber", AggOriginMatrixNumber.ToString()),
            new ModellerControllerParameter("xtmf_AggDestinationMatrixNumber", AggDestinationMatrixNumber.ToString()),
            new ModellerControllerParameter("xtmf_AutoODMatrixId", AutoODMatrixId.ToString()),
            new ModellerControllerParameter("xtmf_AccessStationRange", StationCentroids.ToString()),
            new ModellerControllerParameter("xtmf_ZoneCentroidRange", ZoneCentroids.ToString())
        ];
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
