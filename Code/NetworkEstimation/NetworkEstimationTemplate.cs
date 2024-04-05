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
using System.IO;
using System.Linq;
using System.Xml;
using TMG.Emme;
using XTMF;
using XTMF.Networking;

namespace TMG.NetworkEstimation;

public class NetworkEstimationTemplate : I4StepModel
{
    public IClient Client;

    [RunParameter("EMME To TTS", @"../../Input/TTSToEMME.csv", "CSV file to link EMME Lines to the TTS data.")]
    public string EmmetoTtsFile;

    [SubModelInformation(Description = "The AI to use for estimation", Required = true)]
    public INetworkEstimationAI EstimationAi;

    [RunParameter("Evaluation File", @"../ParameterEvaluation.csv", "The file where the parameters and their evaluated fitness are stored.")]
    public string EvaluationFile;

    [RunParameter("Emme Input Output", @"C:\Users\James\Documents\Project\scalars.311", "The name of the file the macro Loads")]
    public string MacroInputFile;

    [RunParameter("Emme Macro Output", @"C:\Users\James\Documents\Project\output.621", "The name of the file the macro creates")]
    public string MacroOutputFile;

    [RunParameter("Number Of Runs", 80, "The Number of runs to do.")]
    public int NumberOfRuns;

    [RunParameter("Parameter Instructions", "../../Input/ParameterInstructions.xml", "Describes which and how the parameters will be estimated.")]
    public string ParameterInstructions;

    [RunParameter("ResultPort", 12345, "The Custom Port to use for sending back the results")]
    public int ResultPort;

    [RunParameter("TruthFile", @"../../Input/TransitLineTruth.csv", "The file that contains the boardings on transit lines.")]
    public string TruthFile;

    private static Tuple<byte, byte, byte> Colour = new(100, 200, 100);

    private static char[] Comma = { ',' };

    private static int SummeryNumber;

    private float BestRunError = float.MaxValue;

    private volatile bool Exit;

    private bool FirstRun;

    private ParameterSetting[] Parameters;

    private TransitLine[] Truth;

    public int CurrentIteration
    {
        get;
        set;
    }

    public string InputBaseDirectory
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<IModeChoiceNode> Modes
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The network model that we want to estimate", Required = true)]
    public INetworkAssignment NetworkAssignment { get; set; }

    [DoNotAutomate]
    public IList<INetworkData> NetworkData { get { return null; } }

    public string OutputBaseDirectory
    {
        get;
        set;
    }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return Colour; }
    }

    [DoNotAutomate]
    public List<IPurpose> Purpose
    {
        get;
        set;
    }

    public int TotalIterations
    {
        get;
        set;
    }

    [DoNotAutomate]
    public IZoneSystem ZoneSystem
    {
        get;
        set;
    }

    public bool ExitRequest()
    {
        Exit = true;
        EstimationAi.CancelExploration();
        return true;
    }

    public bool RuntimeValidation(ref string error)
    {
        if (!File.Exists(ParameterInstructions))
        {
            error = "The file \"" + ParameterInstructions + "\" was not found!";
            return false;
        }
        return true;
    }

    public void Start()
    {
        InitializeAssignment();
        LoadParameterInstructions();
        FirstRun = true;
        if (Client != null)
        {
            Client.RegisterCustomSender(ResultPort, delegate (object o, Stream s)
           {
               var results = o as float[];
               if (results == null) return;
               var length = results.Length;
               BinaryWriter writer = new(s);
               for (int i = 0; i < length; i++)
               {
                   writer.Write(results[i]);
               }
           });
        }
        for (int run = 0; run < NumberOfRuns; run++)
        {
            float currentPoint = (float)run / NumberOfRuns;
            float inverse = 1f / NumberOfRuns;
            Progress = 0;
            EstimationAi.Explore(Parameters, () => Progress = currentPoint + (inverse * EstimationAi.Progress), EvaluteParameters);
            Progress = currentPoint + inverse;
            if (Exit) break;
        }
    }

    private float EvaluteParameters(ParameterSetting[] parameters)
    {
        SetupInputFiles(parameters);
        NetworkAssignment.RunNetworkAssignment();
        return ProcessResults(parameters);
    }

    private void InitializeAssignment()
    {
        // Get all of the initial ground truth data
        List<TransitLine> truthList = [];
        // On the first pass go through all of the data and store the records of the TTS boardings
        using (StreamReader reader = new(TruthFile))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var split = line.Split(Comma, StringSplitOptions.RemoveEmptyEntries);
                TransitLine current = new();
                string currentName;
                current.Id = new[] { (currentName = split[1]) };
                current.Bordings = float.Parse(split[0]);
                if (split.Length > 2)
                {
                    current.Mode = split[2][0];
                }
                else
                {
                    current.Mode = 'b';
                }
                // Check to make sure that there isn't another ID with this name already
                int count = truthList.Count;
                for (int j = 0; j < count; j++)
                {
                    if (truthList[j].Id[0] == currentName)
                    {
                        throw new XTMFRuntimeException(this,
                            $"The TTS record {currentName} at line {j + 1} has a duplicate entry on line {count + 1}");
                    }
                }
                truthList.Add(current);
            }
        }
        // now on the second pass go through and find all of the EMME Links that connect to the TTS data
        var truthEntries = truthList.Count;
        List<string>[] nameLinks = new List<string>[truthEntries];
        using (StreamReader reader = new(EmmetoTtsFile))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var split = line.Split(Comma, StringSplitOptions.RemoveEmptyEntries);
                string ttsName = split[0];
                string emmeName = split[1];
                for (int i = 0; i < truthEntries; i++)
                {
                    if (truthList[i].Id[0] == ttsName)
                    {
                        List<string> ourList;
                        if ((ourList = nameLinks[i]) == null)
                        {
                            nameLinks[i] = ourList = [];
                        }
                        ourList.Add(emmeName);
                        break;
                    }
                }
            }
        }
        // Now on the third pass we go through and apply all of the EMME ID's
        for (int i = 0; i < truthEntries; i++)
        {
            List<string> nameList;
            if ((nameList = nameLinks[i]) == null)
            {
                throw new XTMFRuntimeException(this,
                    $"The TTS record {truthList[i].Id[0]} has no EMME Links associated with it.  Aborting.");
            }
            var temp = truthList[i];
            temp.Id = nameList.ToArray();
            truthList[i] = temp;
        }
        Truth = truthList.ToArray();
    }

    private void LoadParameterInstructions()
    {
        XmlDocument doc = new();
        doc.Load(ParameterInstructions);
        List<ParameterSetting> parameters = [];
        var children = doc["Root"]?.ChildNodes;
        if (children != null)
        {
            foreach (XmlNode child in children)
            {
                if (child.Name == "Parameter")
                {
                    var attributes = child.Attributes;
                    if (attributes != null)
                    {
                        ParameterSetting current = new()
                        {
                            ParameterName = attributes["Name"].InnerText,
                            MsNumber = int.Parse(attributes["MS"].InnerText),
                            Start = float.Parse(attributes["Start"].InnerText),
                            Stop = float.Parse(attributes["Stop"].InnerText)
                        };
                        current.Current = current.Start;
                        parameters.Add(current);
                    }
                }
            }
        }
        Parameters = parameters.ToArray();
    }

    private void PrintSummery(float[] aggToTruth, List<KeyValuePair<string, float>> orphans)
    {
        using StreamWriter writer = new("LineSummery" + (SummeryNumber++) + ".csv");
        writer.WriteLine("Truth,Predicted,Error,Error^2,EmmeLines");
        for (int i = 0; i < aggToTruth.Length; i++)
        {
            float error = aggToTruth[i] - Truth[i].Bordings;
            writer.Write(Truth[i].Bordings);
            writer.Write(',');
            writer.Write(aggToTruth[i]);
            writer.Write(',');
            writer.Write(error);
            writer.Write(',');
            writer.Write(error * error);
            for (int j = 0; j < Truth[i].Id.Length; j++)
            {
                writer.Write(',');
                writer.Write(Truth[i].Id[j]);
            }
            writer.WriteLine();
        }
        writer.WriteLine();
        writer.WriteLine();
        writer.WriteLine("Orphans");
        foreach (var orphan in orphans)
        {
            writer.Write(orphan.Value);
            writer.Write(',');
            writer.WriteLine(orphan.Key);
        }
    }

    private float ProcessResults(ParameterSetting[] param)
    {
        TransitLines currentLines = new(MacroOutputFile);
        var predicted = currentLines.Lines;
        var numberOfLines = predicted.Length;
        double rmse = 0;
        double mabs = 0;
        double terror = 0;
        float[] aggToTruth = new float[Truth.Length];
        List<KeyValuePair<string, float>> orphans = [];
        for (int i = 0; i < numberOfLines; i++)
        {
            bool orphan = true;
            for (int j = 0; j < Truth.Length; j++)
            {
                bool found = false;
                foreach (var line in predicted[i].Id)
                {
                    if (Truth[j].Id.Contains(line))
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    orphan = false;
                    aggToTruth[j] += predicted[i].Bordings;
                    break;
                }
            }
            if (orphan)
            {
                orphans.Add(new KeyValuePair<string, float>(predicted[i].Id[0], predicted[i].Bordings));
            }
        }

        for (int i = 0; i < Truth.Length; i++)
        {
            var error = aggToTruth[i] - Truth[i].Bordings;
            rmse += error * error;
            mabs += Math.Abs(error);
            terror += error;
        }
        var value = EstimationAi.UseComplexErrorFunction ? EstimationAi.ComplexErrorFunction(Parameters, Truth, predicted, aggToTruth) : EstimationAi.ErrorCombinationFunction(rmse, mabs, terror);
        if (value < BestRunError)
        {
            SaveBordingData(aggToTruth, orphans);
            BestRunError = value;
        }
        SaveEvaluation(param, value, rmse, mabs, terror);
        return value;
    }

    private void SaveBordingData(float[] aggToTruth, List<KeyValuePair<string, float>> orphans)
    {
        File.Copy(MacroOutputFile, "Best-" + Path.GetFileName(MacroOutputFile), true);
        PrintSummery(aggToTruth, orphans);
    }

    private void SaveEvaluation(ParameterSetting[] param, float value, double rmse, double mabs, double terror)
    {
        if (Client != null)
        {
            var paramLength = param.Length;
            float[] results = new float[paramLength + 4];
            for (int i = 0; i < paramLength; i++)
            {
                results[i] = param[i].Current;
            }
            results[paramLength] = value;
            results[paramLength + 1] = (float)rmse;
            results[paramLength + 2] = (float)mabs;
            results[paramLength + 3] = (float)terror;
            Client.SendCustomMessage(results, ResultPort);
        }
        bool exists = File.Exists(EvaluationFile);
        using StreamWriter writer = new(EvaluationFile, true);
        if (!exists)
        {
            writer.Write(param[0].ParameterName);
            for (int i = 1; i < param.Length; i++)
            {
                writer.Write(',');
                writer.Write(param[i].ParameterName);
            }
            writer.Write(',');
            writer.Write("Value");
            writer.Write(',');
            writer.Write("rmse");
            writer.Write(',');
            writer.Write("mabs");
            writer.Write(',');
            writer.WriteLine("terror");
        }
        if (FirstRun && exists)
        {
            writer.WriteLine();
        }
        FirstRun = false;
        writer.Write(param[0].Current);
        for (int i = 1; i < param.Length; i++)
        {
            writer.Write(',');
            writer.Write(param[i].Current);
        }
        writer.Write(',');
        writer.Write(value);
        writer.Write(',');
        writer.Write(rmse);
        writer.Write(',');
        writer.Write(mabs);
        writer.Write(',');
        writer.WriteLine(terror);
    }

    private void SetupInputFiles(ParameterSetting[] param)
    {
        /*
         * t matrices
         * m ms[MS:##] [NAME]
         *  all all: [VALUE]
         */
        using StreamWriter writer = new(MacroInputFile);
        writer.WriteLine("t matrices");
        foreach (var p in param)
        {
            writer.WriteLine($"m ms{p.MsNumber} {p.ParameterName}");
            writer.WriteLine($" all all: {p.Current}");
        }
    }
}