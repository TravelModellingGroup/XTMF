/*
    Copyright 2023 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Input;
using XTMF;
using System.IO;
using System.Text.Json;
using Datastructure;
using TMG.Functions;
using System.Text.Unicode;
using System.Text;

namespace TMG.Emme.Tools.NetworkEditing.TransitFareHypernetworks;

[ModuleInformation(Description = "This tool is designed to modify the transfer times by reading in a" +
    " CSV file containing station node numbers and the desired transfer times.")]
public sealed class AddTransferTimeAdjustmentsFromCSV : IEmmeTool
{
    [SubModelInformation(Required = true, Description = "The CSV (StationNode,TransferTime) to apply.")]
    public FileLocation StationTimeCSV;

    [RunParameter("Scenario Number", 0, "The scenario number to operate on.")]
    public int ScenarioNumber;

    [RunParameter("Transfer Modes", "*", "A string list containing all transfer modes available. A lone * means all aux_transit.")]
    public string TransferModes;

    private const string ToolName = "tmg.network_editing.transit_fare_hypernetworks.add_transfer_time_adjustments";

    public bool Execute(Controller controller)
    {
        if (controller is ModellerController modellerController)
        {
            return modellerController.Run(this, ToolName, GetParameters());
        }
        return false;
    }

    private ModellerControllerParameter[] GetParameters()
    {
        return new ModellerControllerParameter[]
        {
            new("parameters", GetJsonParameters())
        };
    }

    private struct StationAdjustment
    {
        internal int StationNode;
        internal float AdjustedTime;

        public StationAdjustment(int stationNode, float adjustedTime)
        {
            StationNode = stationNode;
            AdjustedTime = adjustedTime;
        }
    }

    private string GetJsonParameters()
    {
        var adjustments = new List<StationAdjustment>();
        // Read in the adjustments
        using (var reader = new CsvReader(StationTimeCSV, true))
        {
            reader.LoadLine(); // Burn the header
            while (reader.LoadLine(out int columns))
            {
                if (columns >= 2)
                {
                    reader.Get(out int station, 0);
                    reader.Get(out float adjustedTime, 1);
                    adjustments.Add(new StationAdjustment(station, adjustedTime));
                }
            }
        }

        //Write out the found stations
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("scenario", ScenarioNumber);
            writer.WritePropertyName("stations");
            writer.WriteStartArray();
            foreach (var adjustment in adjustments)
            {
                writer.WriteStartObject();
                writer.WriteNumber("station_number", adjustment.StationNode);
                writer.WriteNumber("transfer_time", adjustment.AdjustedTime);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteString("transfer_modes", TransferModes);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        if (ScenarioNumber <= 0)
        {
            error = $"In {Name} the scenario number was set to {ScenarioNumber}, however only positively numbered scenarios are valid!";
            return false;
        }
        // Check the transfer modes
        if (String.IsNullOrEmpty(TransferModes))
        {
            error = $"In {Name} the there are no transfer modes defined!";
            return false;
        }
        foreach (char c in TransferModes)
        {
            if (!char.IsLetterOrDigit(c) && (c != '*' && TransferModes.Length > 1))
            {
                error = $"In {Name} the transfer mode \"{c}\" is not a valid transfer mode!";
                return false;
            }
        }
        return true;
    }
}
