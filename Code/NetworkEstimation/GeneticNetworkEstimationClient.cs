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
using System.Text;
using System.Xml;
using TMG.Emme;
using XTMF;
using XTMF.Networking;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.NetworkEstimation;

public class GeneticNetworkEstimationClient : I4StepModel
{
    [SubModelInformation(Description = "The AI that will be used to evaluate the given configuration", Required = true)]
    public INetworkEstimationAI Ai;

    public IClient Client;

    [RunParameter("Emme Column Size", 6, "The amount of data that we can fit into a column for emme.")]
    public int EmmeColumnSize;

    [RunParameter("EMME To TTS", @"../../Input/EMMEToTTS.csv", "CSV file to link EMME Lines to the TTS data.")]
    public string EmmetoTtsFile;

    [RunParameter("Emme Input Output", @"D:\EMMENetworks\Test_Transit-1\Database\cache\scalars.311", "The name of the file the macro loads")]
    public string MacroInputFile;

    [RunParameter("Emme Macro Output", @"D:\EMMENetworks\Test_Transit-1\Database\cache\boardings_predicted.621", "The name of the file the macro creates")]
    public string MacroOutputFile;

    [RunParameter("Parameter Instructions", "../../Input/ParameterInstructions.xml", "Describes which and how the parameters will be estimated.")]
    public string ParameterInstructions;

    [RunParameter("TruthFile", @"../../Input/TransitLineTruth.csv", "The name of the file the macro creates")]
    public string TruthFile;

    private static Tuple<byte, byte, byte> _ProgressColour = new(50, 150, 50);

    private static char[] Comma = [','];

    private static int SummeryNumber;

    private float BestRunError = float.MaxValue;

    private bool Exit;

    private ParameterSetting[] Parameters;

    private MessageQueue<Job> ParametersToProcess;

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
    public IList<INetworkData> NetworkData
    {
        get;
        set;
    }

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
        get { return _ProgressColour; }
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
        return true;
    }

    public bool RuntimeValidation(ref string error)
    {
        if (Client == null)
        {
            error = "The Genetic Network Estimation Client needs to be run as a remote client.";
            return false;
        }
        return true;
    }

    public void Start()
    {
        using (ParametersToProcess = new MessageQueue<Job>())
        {
            InitializeClient();
            while (!Exit)
            {
                var job = ParametersToProcess.GetMessageOrTimeout(200);
                if (job != null)
                {
                    // Process the system, and then return the result back to the server
                    float result = ProcessParameters(job);
                    Client.SendCustomMessage(new ProcessedResult { Generation = job.Generation, Index = job.Index, Result = result }, 0);
                }
            }
        }
        Exit = true;
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
                current.Id = [(currentName = split[1])];
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
            temp.Id = [.. nameList];
            truthList[i] = temp;
        }
        Truth = [.. truthList];
    }

    private void InitializeClient()
    {
        LoadInstructions();
        InitializeAssignment();
        Client.RegisterCustomSender(0, SendResultToHost);
        Client.RegisterCustomReceiver(1, ReceiveNewParameters);
        Client.RegisterCustomMessageHandler(1, QueueProcessing);
        // send the message to start the chain reaction of processing
        Client.SendCustomMessage(new ProcessedResult { Index = -1 }, 0);
    }

    private void LoadInstructions()
    {
        XmlDocument doc = new();
        doc.Load(ParameterInstructions);
        List<ParameterSetting> parameters = [];
        var childNodes = doc["Root"]?.ChildNodes;
        if (childNodes != null)
        {
            foreach (XmlNode child in childNodes)
            {
                if (child.Name == "Parameter")
                {
                    ParameterSetting current = new();
                    var attributes = child.Attributes;
                    if (attributes != null)
                    {
                        current.ParameterName = attributes["Name"].InnerText;
                        current.MsNumber = int.Parse(attributes["MS"].InnerText);
                        current.Start = float.Parse(attributes["Start"].InnerText);
                        current.Stop = float.Parse(attributes["Stop"].InnerText);
                    }
                    current.Current = current.Start;
                    parameters.Add(current);
                }
            }
        }
        Parameters = [.. parameters];
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

    private float ProcessParameters(Job job)
    {
        // Step 1, figure out our parameters
        var length = job.Parameters.Length;
        for (int i = 0; i < length; i++)
        {
            Parameters[i].Current = job.Parameters[i];
        }
        SetupInputFiles(Parameters);
        NetworkAssignment.RunNetworkAssignment();
        return ProcessResults();
    }

    private float ProcessResults()
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
        var value = Ai.UseComplexErrorFunction ? Ai.ComplexErrorFunction(Parameters, Truth, predicted, aggToTruth) : Ai.ErrorCombinationFunction(rmse, mabs, terror);
        if (value < BestRunError)
        {
            SaveBordingData(aggToTruth, orphans);
            BestRunError = value;
        }
        return value;
    }

    private void QueueProcessing(object o)
    {
        var job = o as Job;
        ParametersToProcess.Add(job);
    }

    private object ReceiveNewParameters(Stream s)
    {
        Job job = new();
        BinaryReader reader = new(s);
        job.Generation = reader.ReadInt32();
        job.Index = reader.ReadInt32();
        var length = reader.ReadInt32();
        job.Parameters = new float[length];
        for (int i = 0; i < length; i++)
        {
            job.Parameters[i] = reader.ReadSingle();
        }
        return job;
    }

    private void SaveBordingData(float[] aggToTruth, List<KeyValuePair<string, float>> orphans)
    {
        File.Copy(MacroOutputFile, "Best-" + Path.GetFileName(MacroOutputFile), true);
        PrintSummery(aggToTruth, orphans);
    }

    private void SendResultToHost(object o, Stream s)
    {
        BinaryWriter writer = new(s);
        var res = (ProcessedResult)o;
        writer.Write(res.Generation);
        writer.Write(res.Index);
        if (res.Index != -1)
        {
            writer.Write(res.Result);
        }
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
            writer.WriteLine($" all all: {ToEmmeFloat(p.Current)}");
        }
    }

    /// <summary>
    /// Process floats to work with emme
    /// </summary>
    /// <param name="p">The float you want to send</param>
    /// <returns>A limited precision non scientific number in a string</returns>
    private string ToEmmeFloat(float p)
    {
        StringBuilder builder = new();
        builder.Append((int)p);
        p = p - (int)p;
        if (p > 0)
        {
            var integerSize = builder.Length;
            builder.Append('.');
            for (int i = integerSize; i < EmmeColumnSize; i++)
            {
                p = p * 10;
                builder.Append((int)p);
                p = p - (int)p;
                if (p == 0)
                {
                    break;
                }
            }
        }
        return builder.ToString();
    }

    private class Job
    {
        internal int Generation;
        internal int Index;
        internal float[] Parameters;
    }

    private class ProcessedResult
    {
        internal int Generation;
        internal int Index;
        internal float Result;
    }
}