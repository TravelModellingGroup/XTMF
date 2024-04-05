﻿/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using TMG.Input;
using XTMF;

namespace TMG.Emme.Tools.Analysis.Traffic;


public class ExportScreenlineResults : IEmmeTool
{
    private const string ToolName = "tmg.analysis.traffic.export_screenline_results";
    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    [RunParameter("Scenario Number", "1", typeof(int), "The scenario to interact with")]
    public int ScenarioNumber;

    [RunParameter("CountpostAttributeFlag", "@stn1", typeof(string), "The attribute name to use for identifying countposts.")]
    public string CountpostAttributeFlag;

    [RunParameter("AlternateCountpostAttributeFlag", "@stn2", typeof(string), "The alternate attribute name to use for identifying countposts.")]
    public string AlternateCountpostAttributeFlag;


    [SubModelInformation(Required = true, Description = "The location to save the results to")]
    public FileLocation SaveTo;

    [SubModelInformation(Required = true, Description = "The location for the definition file for screenlines")]
    public FileLocation ScreenlineDefinitions;

    public bool Execute(Controller controller)
    {
        var modeller = controller as ModellerController ?? throw new XTMFRuntimeException(this, "In '" + Name + "' we require the use of EMME Modeller in order to execute.");
        EnsureDirectoryExists(SaveTo);
        return modeller.Run(this, ToolName, GetParameters());
    }

    private void EnsureDirectoryExists(FileLocation saveTo)
    {
        try
        {
            DirectoryInfo directoryInfo = new(Path.GetDirectoryName(saveTo));
            if(!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }
        }
        catch(IOException e)
        {
            throw new XTMFRuntimeException(this, e, e.Message);
        }
    }

    private string GetParameters()
    {
        return string.Join(" ", ScenarioNumber, AddQuotes(CountpostAttributeFlag), AddQuotes(AlternateCountpostAttributeFlag), AddQuotes(ScreenlineDefinitions), AddQuotes(SaveTo));
    }

    private static string AddQuotes(string toQuote)
    {
        return String.Concat("\"", toQuote, "\"");
    }

    public bool RuntimeValidation(ref string error)
    {
        if (ErrorIfBlank(CountpostAttributeFlag, "CountpostAttributeFlag", ref error)
            || ErrorIfBlank(AlternateCountpostAttributeFlag, "AlternateCountpostAttributeFlag", ref error))
        {
            return false;
        }
        return true;
    }

    private bool ErrorIfBlank(string flag, string nameOfAttribute, ref string error)
    {
        if (String.IsNullOrWhiteSpace(flag))
        {
            error = "In '" + Name + "' the attribute '" + nameOfAttribute + "' is not assigned to!";
            return true;
        }
        return false;
    }
}
