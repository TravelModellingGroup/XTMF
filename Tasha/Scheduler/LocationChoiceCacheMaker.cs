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
using Datastructure;
using TMG;
using XTMF;
// ReSharper disable InconsistentNaming
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Tasha.Scheduler;

public class LocationChoiceCacheMaker : ITravelDemandModel
{
    [RunParameter("Activity Levels File", "ActivityLevels.zfc", "The .zfc file that has the Activity Levels information")]
    public string ActivityLevels;

    [RunParameter("BGP1 -> BGPMaxDist", "0,0,0,0,0,0,0", "Please make sure that the parameters are separated by only a comma (BGP1,BGP2,BGP3,BGP4,BGP5,BGP6,BGPMaxDist)")]
    public string BGPString;

    [RunParameter("BMP1 -> BMPMaxDist", "0,0,0,0,0,0,0", "Please make sure that the parameters are separated by only a comma (BMP1,BMP2,BMP3,BMP4,BMP5,BMP6,BMPMaxDist)")]
    public string BMPString;

    [RunParameter("BPP1 -> BPPMaxDist", "0,0,0,0,0,0", "Please make sure that the parameters are separated by only a comma (BPP1,BPP2,BPP3,BPP4,BPP5,BPPMaxDist)")]
    public string BPPString;

    [RunParameter("BSP1 -> BSPMaxDist", "0,0,0,0,0,0", "Please make sure that the parameters are separated by only a comma (BSP1,BSP2,BSP3,BSP4,BSP5,BSPMaxDist)")]
    public string BSPString;

    [RunParameter("Location Choice Model Home Cache file", "LocationChoiceModelHomeCache", "The name of the file we are going to output")]
    public string LocatonChoiceModelHomeCache;

    [RunParameter("Location Choice Model Work Cache file", "LocationChoiceModelWorkCache", "The name of the file we are going to output")]
    public string LocatonChoiceModelWorkCache;

    [RunParameter("MAP1 -> MAMaxDist", "0,0,0,0,0", "Please make sure that the parameters are separated by only a comma (MAP1,MAP2,MAP3,MAP4,MAMaxDist)")]
    public string MAPString;

    [RunParameter("MBP1 -> MBMaxDist", "0,0,0,0,0,0", "Please make sure that the parameters are separated by only a comma (MBP1,MBP2,MBP3,MBP4,MBMaxDist)")]
    public string MBPString;

    [RunParameter("MMMaxDist1)", 0, "The MMMaxDist1 Parameter")]
    public float MMMaxDist1;

    [RunParameter("MMMaxDist2)", 0, "The MMMaxDist3 Parameter")]
    public float MMMaxDist2;

    [RunParameter("MMMaxDist3)", 0, "The MMMaxDist3 Parameter")]
    public float MMMaxDist3;

    [RunParameter("MMP1 -> MMP16", "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0", "Please make sure that the parameters are seperated by only a comma (MMP1,MMP2,...")]
    public string MMPString;

    [RunParameter("MOP1 -> MOMaxDist", "0,0,0,0,0", "Please make sure that the parameters are seperated by only a comma (MOP1,MOP2,MOP3,MOP4,MOMaxDist)")]
    public string MOPString;

    [RunParameter("Zones", 7150, "The number of internal zones in the system.")]
    public int NumberOfInternalZones;

    [RunParameter("Input Base Directory", "../../Input", "The root of the input files for this model system.")]
    public string InputBaseDirectory
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    }

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
        get;
        set;
    }

    [SubModelInformation(Description = "The zone system to create the location choices for", Required = true)]
    public IZoneSystem ZoneSystem
    {
        get;
        set;
    }

    public void BuildLocationChoiceCache(float[,] p, IZone[] flatZones)
    {
        string temp = Path.GetTempFileName();
        //string temp = Directory.GetCurrentDirectory() + "\\" + "test.csv";
        Console.WriteLine(temp);
        StreamWriter writer = new(temp);
        //fire up the cache
        //each row comparing one zone to another zone
        //column 1 zone ID, column 2 zone(comparing zone),column 3 distance,
        //column 4 sumExp1,
        //column5 exponentExpression1: exp( (p1*distkm) + (p2*log(empP)) + (p3*log(empG)) + (p4*sh0) + (p5*sh1) + (p6*sh2) )
        //column 6 sumExp1,
        //column7 exponentExpression1: exp( (p1*distkm) + (p2*log(empP)) + (p3*log(empG)) + (p4*sh0) + (p5*sh1) + (p6*sh2) )
        //column 8 sumExp1,
        //column9 exponentExpression1: exp( (p1*distkm) + (p2*log(empP)) + (p3*log(empG)) + (p4*sh0) + (p5*sh1) + (p6*sh2) )
        //column 10 sumExp1,
        //column11 exponentExpression1: exp( (p1*distkm) + (p2*log(empP)) + (p3*log(empG)) + (p4*sh0) + (p5*sh1) + (p6*sh2) )
        //n = number of zones, so n*n number of rows
        StringBuilder line;

        for (int i = 0; i < flatZones.Length; i++)
        {
            IZone iz = flatZones[i];
            if ((iz.Population == 0 &&
               iz.X == 0 && iz.Y == 0) || iz.InternalDistance == 0) continue;

            line = new StringBuilder(100000);
            line.Append(i);

            BuildOfficeWorkCache(line, iz, p, flatZones);
            BuildManufacturingWorkCache(line, iz, p, flatZones);
            BuildRetailWorkCache(line, iz, p, flatZones);
            BuildProfessionalWorkCache(line, iz, p, flatZones);

            writer.WriteLine(line.ToString(0, line.Length - 1));
        }
        writer.Close();

        //IConfigurationDirectory directory =
        //    TashaConfiguration.GetDirectory("LocationChoiceModelParameters");
        SparseZoneCreator creator = new(3, (4 * flatZones.Last().ZoneNumber) + 1);
        creator.LoadCsv(temp, false);
        creator.Save(LocatonChoiceModelWorkCache);
        File.Delete(temp);
    }

    public void BuildLocationChoiceCacheHome(float[,] p, IZone[] flatZones)
    {
        string temp = Path.GetTempFileName();
        //string temp = Directory.GetCurrentDirectory() + "\\" + "test2.csv";
        StreamWriter writer = new(temp);
        //fire up the cache

        StringBuilder line;
        Console.WriteLine("Building LocationChoiceHomeCache");
        for (int i = 0; i < flatZones.Length; i++)
        {
            IZone iz = flatZones[i];
            if (iz.Population == 0 &&
               iz.X == 0 && iz.Y == 0) continue;

            double distkm;
            //sum EXP for G---------------------------------------------------
            double sumExp = 0;
            double empR, sh0, sh1, sh2, empT;
            line = new StringBuilder(100000);
            line.Append(i);

            //h=0

            for (int k = 0; k < flatZones.Length; k++)
            {
                IZone kz = flatZones[k];

                if (kz.InternalDistance == 0)
                    continue;

                if (kz.TotalEmployment > 0)
                {
                    empT = Math.Log((kz.TotalEmployment / 1000.0) + 1.0);

                    //distance in KM
                    distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                    if (distkm >= 0 && distkm <= p[0, 4])
                    {
                        sh0 = 0.0;
                        sh1 = 0.0;
                        if (distkm >= 0 && distkm < 1) sh0 = 1;
                        else if (distkm >= 1 && distkm < 2) sh1 = 1;

                        sumExp += Math.Exp((p[0, 0] * distkm) + (p[0, 1] * empT) + (p[0, 2] * sh0) + (p[0, 3] * sh1));
                    }
                }
            }
            //now calculate CDF sums from ... k .. num InternalZones

            double cdf = 0.0;
            for (int k = 0; k < flatZones.Length; k++)
            {
                IZone kz = flatZones[k];

                if (kz.TotalEmployment > 0)
                {
                    empT = Math.Log((kz.TotalEmployment / 1000.0) + 1.0);

                    distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                    if (distkm >= 0 && distkm <= p[0, 4] && kz.InternalDistance > 0)
                    {
                        sh0 = 0.0;
                        sh1 = 0.0;
                        if (distkm >= 0 && distkm < 1) sh0 = 1;
                        else if (distkm >= 1 && distkm < 2) sh1 = 1;

                        cdf += (Math.Exp((p[0, 0] * distkm) + (p[0, 1] * empT) + (p[0, 2] * sh0) + (p[0, 3] * sh1)) / sumExp);
                    }
                }
                line.Append(",");
                line.Append(cdf);
            }

            sumExp = 0;
            for (int k = 0; k < flatZones.Length; k++)
            {
                IZone kz = flatZones[k];

                if (kz.InternalDistance == 0)
                    continue;
                if (kz.TotalEmployment > 0)
                {
                    empT = Math.Log((kz.TotalEmployment / 1000.0) + 1.0);
                    distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                    if (distkm >= 0 && distkm <= p[1, 5])
                    {
                        sh0 = 0.0;
                        sh1 = 0.0;
                        sh2 = 0.0;
                        if (distkm >= 0 && distkm < 1) sh0 = 1;
                        else if (distkm >= 1 && distkm < 2) sh1 = 1;
                        else if (distkm >= 2 && distkm < 3) sh2 = 1;

                        sumExp += Math.Exp(((p[1, 0] * distkm) + (p[1, 1] * empT) + (p[1, 2] * sh0) + (p[1, 3] * sh1) + (p[1, 4] * sh2)));
                    }
                }
            }

            cdf = 0.0;
            for (int k = 0; k < flatZones.Length; k++)
            {
                IZone kz = flatZones[k];

                if (kz.TotalEmployment > 0)
                {
                    empT = Math.Log(((double)kz.TotalEmployment / 1000) + 1.0);

                    distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                    if (distkm >= 0 && distkm <= p[1, 5] && kz.InternalDistance > 0)
                    {
                        sh0 = 0.0;
                        sh1 = 0.0;
                        sh2 = 0.0;
                        if (distkm >= 0 && distkm < 1) sh0 = 1;
                        else if (distkm >= 1 && distkm < 2) sh1 = 1;
                        else if (distkm >= 2 && distkm < 3) sh2 = 1;

                        cdf += Math.Exp(((p[1, 0] * distkm) + (p[1, 1] * empT) + (p[1, 2] * sh0) + (p[1, 3] * sh1) + (p[1, 4] * sh2))) / sumExp;
                    }
                }
                line.Append(",");
                line.Append(cdf);
            }

            sumExp = 0;
            for (int k = 0; k < flatZones.Length; k++)
            {
                IZone kz = flatZones[k];

                if (kz.InternalDistance == 0 || kz.TotalEmployment <= 0)
                    continue;

                empT = Math.Log((kz.TotalEmployment / 1000.0) + 1.0);
                double pop = Math.Log((kz.Population / 1000.0) + 1.0);
                distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                if (distkm >= 0 && distkm <= p[2, 4])
                {
                    sh0 = 0.0;
                    if (distkm >= 0 && distkm < 1) sh0 = 1;

                    sumExp += Math.Exp((p[2, 0] * distkm) + (p[2, 1] * empT) + (p[2, 2] * pop) + (p[2, 3] * sh0));
                }
            }

            //line.Append(sumExp);
            cdf = 0.0;
            for (int k = 0; k < flatZones.Length; k++)
            {
                IZone kz = flatZones[k];

                if (kz.TotalEmployment > 0)
                {
                    empT = Math.Log((kz.TotalEmployment / 1000.0) + 1.0);
                    double pop = Math.Log((kz.Population / 1000.0) + 1.0);

                    distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                    if (distkm >= 0 && distkm <= p[2, 4] && kz.InternalDistance > 0)
                    {
                        sh0 = 0.0;
                        if (distkm >= 0 && distkm < 1) sh0 = 1;

                        cdf += Math.Exp((p[2, 0] * distkm) + (p[2, 1] * empT) + (p[2, 2] * pop) + (p[2, 3] * sh0)) / sumExp;
                    }
                }
                line.Append(",");
                line.Append(cdf);
            }

            sumExp = 0;
            for (int k = 0; k < flatZones.Length; k++)
            {
                IZone kz = flatZones[k];

                if (kz.InternalDistance == 0 || kz.TotalEmployment <= 0)
                    continue;

                empR = Math.Log((kz.RetailEmployment / 1000.0) + 1.0);
                distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;

                double maxDist;
                int index;
                if (kz.PlanningDistrict == 1)
                {
                    maxDist = p[3, 16]; //config <MMaxDist1>
                    index = 16;
                }
                //if retail activity > 3
                else if (ActivityDistribution.GetDistribution(kz, 0) >= 3)
                {
                    maxDist = p[3, 17];
                    index = 17;
                }
                else
                {
                    maxDist = p[3, 18];
                    index = 18;
                }

                if (distkm >= 0 && distkm <= maxDist)
                {
                    //empR = ( kz.RetailActivityLevel / 1000 ) + 0.001;
                    sh0 = 0.0;
                    sh1 = 0.0;
                    sh2 = 0.0;
                    if (distkm >= 0 && distkm < 1) sh0 = 1;
                    else if (distkm >= 1 && distkm < 2) sh1 = 1;

                    switch (index)
                    {
                        case 16:
                            sumExp += Math.Exp((p[3, 0] * distkm) + (p[3, 1] * empR) + (p[3, 2] * sh0) + (p[3, 3] * sh1) + (p[3, 4] * sh2));
                            break;

                        case 17:
                            sumExp += Math.Exp(p[3, 6] + (p[3, 5] * distkm) + (p[3, 7] * empR) + (p[3, 8] * sh0) + (p[4, 9] * sh1) + (p[3, 10] * sh2));
                            break;

                        case 18:
                            sumExp += Math.Exp(p[3, 12] + (p[3, 11] * distkm) + (p[3, 13] * empR) + (p[3, 14] * sh0) + (p[3, 15] * sh1));
                            break;
                    }
                }
            }

            //line.Append(sumExp);
            cdf = 0.0;
            for (int k = 0; k < flatZones.Length; k++)
            {
                IZone kz = flatZones[k];

                if (kz.TotalEmployment > 0)
                {
                    empR = Math.Log((kz.RetailEmployment / 1000.0) + 1.0);
                    distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;

                    double maxDist;
                    int index;
                    if (kz.PlanningDistrict == 1)
                    {
                        maxDist = p[3, 16]; //config <MMaxDist1>
                        index = 16;
                    }
                    //if retail activity > 3
                    else if (ActivityDistribution.GetDistribution(kz, 0) >= 3)
                    {
                        maxDist = p[3, 17];
                        index = 17;
                    }
                    else
                    {
                        maxDist = p[3, 18];
                        index = 18;
                    }

                    if (distkm >= 0 && distkm <= maxDist && kz.InternalDistance > 0)
                    {
                        // empR = ( kz.RetailActivityLevel / 1000 ) + 0.001;
                        sh0 = 0.0;
                        sh1 = 0.0;
                        sh2 = 0.0;
                        if (distkm >= 0 && distkm < 1) sh0 = 1;
                        else if (distkm >= 1 && distkm < 2) sh1 = 1;

                        switch (index)
                        {
                            case 16:

                                cdf += Math.Exp((p[3, 0] * distkm) + (p[3, 1] * empR) + (p[3, 2] * sh0) + (p[3, 3] * sh1) + (p[3, 4] * sh2)) / sumExp;
                                break;

                            case 17:
                                cdf += Math.Exp(p[3, 6] + (p[3, 5] * distkm) + (p[3, 7] * empR) + (p[3, 8] * sh0) + (p[4, 9] * sh1) + (p[3, 10] * sh2)) / sumExp;
                                break;

                            case 18:
                                cdf += Math.Exp(p[3, 12] + (p[3, 11] * distkm) + (p[3, 13] * empR) + (p[3, 14] * sh0) + (p[3, 15] * sh1)) / sumExp;
                                break;
                        }
                    }
                }
                line.Append(",");
                line.Append(cdf);
            }

            writer.WriteLine(line.ToString(0, line.Length - 1));
        }
        writer.Close();
        //IConfigurationDirectory directory =
        //    TashaConfiguration.GetDirectory("LocationChoiceModelParameters");
        SparseZoneCreator creator = new(3, (4 * flatZones.Last().ZoneNumber) + 1);
        creator.LoadCsv(temp, false);
        creator.Save(LocatonChoiceModelHomeCache);
        File.Delete(temp);
    }

    public bool ExitRequest()
    {
        return false;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void Start()
    {
        ZoneSystem.LoadData();
        ActivityDistribution.LoadDistributions(ActivityLevels, ZoneSystem.ZoneArray);

        //G = office
        //M = Manufacturing
        //S = Retail
        //P = Professional

        char[] separators = { ',' };

        string[] BGP = BGPString.Split(separators);
        string[] BMP = BMPString.Split(separators);
        string[] BSP = BSPString.Split(separators);
        string[] BPP = BPPString.Split(separators);
        string[] MAP = MAPString.Split(separators);
        string[] MBP = MBPString.Split(separators);
        string[] MOP = MOPString.Split(separators);
        string[] MMP = MMPString.Split(separators);

        string[][] workParams = { BGP, BMP, BSP, BPP };

        float[,] parArray = new float[4, 7];
        float[,] parArray2 = new float[4, 19];

        ///////////////////////////WORK PARAMS/////////////////////////

        for (int i = 0; i < workParams.Length; i++)
        {
            if (i <= 1)
            {
                for (int j = 0; j <= 6; j++)
                {
                    parArray[i, j] = float.Parse(workParams[i][j]);
                }
            }
            else
            {
                for (int j = 0; j <= 5; j++)
                {
                    parArray[i, j] = float.Parse(workParams[i][j]);
                }
            }
        }

        /////////////////////////////HOMEPARAMS//////////////////

        for (int j = 0; j < 5; j++)
        {
            parArray2[0, j] = float.Parse(MAP[j]);
            parArray2[2, j] = float.Parse(MOP[j]);
        }

        for (int j = 0; j < 6; j++)
        {
            parArray2[1, j] = float.Parse(MBP[j]);
        }

        for (int j = 0; j < MMP.Length; j++)
        {
            parArray2[3, j] = float.Parse(MMP[j]);
        }

        parArray2[3, 16] = MMMaxDist1;
        parArray2[3, 17] = MMMaxDist2;
        parArray2[3, 18] = MMMaxDist3;

        IZone[] flatZones = ZoneSystem.ZoneArray.GetFlatData();

        BuildLocationChoiceCache(parArray, flatZones);
        BuildLocationChoiceCacheHome(parArray2, flatZones);
        ZoneSystem.UnloadData();
    }

    private void BuildManufacturingWorkCache(StringBuilder line, IZone iz, float[,] p, IZone[] flatZones)
    {
        int h = 1;
        double distkm;
        //sum EXP for G---------------------------------------------------
        double sumExp, empM;
        double sh0, sh1, sh2, sh3;
        sumExp = 0;

        for (int k = 0; k < flatZones.Length; k++)
        {
            IZone kz = flatZones[k];

            if (kz.TotalEmployment > 0)
            {
                //distance in KM
                distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;

                if (distkm >= 0 && distkm <= p[h, 6] && kz.InternalDistance > 0)
                {
                    empM = (kz.ManufacturingEmployment / 1000.0) + 1.0;
                    sh0 = 0.0; // sh0 is a flag for trips between 0 and 1 km
                    sh1 = 0.0; // sh1 is a flag for trips between 1 and 2 km
                    sh2 = 0.0; // sh2 is a flag for trips between 2 and 3 km
                    sh3 = 0.0; // sh3 is a flag for trips between 3 and 4 km

                    if (distkm >= 0.0 && distkm < 1.0) sh0 = 1.0;
                    else if (distkm >= 1.0 && distkm < 2.0) sh1 = 1.0;
                    else if (distkm >= 2.0 && distkm < 3.0) sh2 = 1.0;
                    else if (distkm >= 3.0 && distkm < 4.0) sh3 = 1.0;

                    //Add to the logit denominator
                    sumExp += Math.Exp((p[h, 0] * distkm) + (p[h, 1] * Math.Log(empM)) + (p[h, 2] * sh0) + (p[h, 3] * sh1) + (p[h, 4] * sh2) + (p[h, 5] * sh3));
                }
            }
        }

        //now calculate CDF sums from ... k .. num InternalZones
        double cdf = 0.0;
        for (int k = 0; k < flatZones.Length; k++)
        {
            IZone kz = flatZones[k];
            if (kz.TotalEmployment > 0)
            {
                distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                if (distkm >= 0 && distkm <= p[h, 6] && kz.InternalDistance > 0)
                {
                    empM = (kz.ManufacturingEmployment / 1000.0) + 1.0;
                    sh0 = 0.0; // sh0 is a flag for trips between 0 and 1 km
                    sh1 = 0.0; // sh1 is a flag for trips between 1 and 2 km
                    sh2 = 0.0; // sh2 is a flag for trips between 2 and 3 km
                    sh3 = 0.0; // sh3 is a flag for trips between 3 and 4 km

                    if (distkm >= 0.0 && distkm < 1.0) sh0 = 1.0;
                    else if (distkm >= 1.0 && distkm < 2.0) sh1 = 1.0;
                    else if (distkm >= 2.0 && distkm < 3.0) sh2 = 1.0;
                    else if (distkm >= 3.0 && distkm < 4.0) sh3 = 1.0;

                    //Add to the CDF
                    cdf += Math.Exp((p[h, 0] * distkm) + (p[h, 1] * Math.Log(empM)) + (p[h, 2] * sh0) + (p[h, 3] * sh1) + (p[h, 4] * sh2) + (p[h, 5] * sh3)) / sumExp;
                    // Console.WriteLine("K " + cdf);
                }
            }
            line.Append(",");
            line.Append(cdf);
        }
    }

    private void BuildOfficeWorkCache(StringBuilder line, IZone iz, float[,] p, IZone[] flatZones)
    {
        double distkm;
        //sum EXP for G---------------------------------------------------
        double sumExp;
        double empP, empG;
        double sh0, sh1, sh2;

        sumExp = 0;
        int h = 0;

        for (int k = 0; k < flatZones.Length; k++)
        {
            IZone kz = flatZones[k];

            if (kz.TotalEmployment > 0)
            {
                //if (!TashaConfiguration.ZoneRetriever.ZoneHasEmploymentData(kz))
                //    continue;

                empP = Math.Log((kz.ProfessionalEmployment / 1000.0) + 1.0);
                empG = Math.Log((kz.GeneralEmployment / 1000.0) + 1.0);

                //distance in KM

                distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;

                if (distkm >= 0 && distkm <= p[h, 6] && kz.InternalDistance > 0)
                {
                    sh0 = 0.0;
                    sh1 = 0.0;
                    sh2 = 0.0;
                    if (distkm >= 0 && distkm < 1) sh0 = 1;
                    else if (distkm >= 1 && distkm < 2) sh1 = 1;
                    else if (distkm >= 2 && distkm < 3) sh2 = 1;

                    sumExp += Math.Exp((p[h, 0] * distkm) + (p[0, 1] * empP) + (p[h, 2] * empG) + (p[h, 3] * sh0) + (p[h, 4] * sh1) + (p[h, 5] * sh2));
                }
            }
        }

        //now calculate CDF sums from ... k .. num InternalZones
        double cdf = 0.0;
        for (int k = 0; k < flatZones.Length; k++)
        {
            IZone kz = flatZones[k];

            if (kz.TotalEmployment > 0)
            {
                empP = Math.Log((kz.ProfessionalEmployment / 1000.0) + 1.0);
                empG = Math.Log((kz.GeneralEmployment / 1000.0) + 1.0);

                distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                if (distkm >= 0 && distkm <= p[h, 6] && kz.InternalDistance > 0)
                {
                    sh0 = 0.0;
                    sh1 = 0.0;
                    sh2 = 0.0;
                    if (distkm >= 0 && distkm < 1) sh0 = 1;
                    else if (distkm >= 1 && distkm < 2) sh1 = 1;
                    else if (distkm >= 2 && distkm < 3) sh2 = 1;

                    cdf += (Math.Exp((p[h, 0] * distkm) + (p[0, 1] * empP) + (p[h, 2] * empG) + (p[h, 3] * sh0) + (p[h, 4] * sh1) + (p[h, 5] * sh2)) / sumExp);
                    // Console.WriteLine("K " + cdf);
                }
            }
            line.Append(",");
            line.Append(cdf);
        }
    }

    private void BuildProfessionalWorkCache(StringBuilder line, IZone iz, float[,] p, IZone[] flatZones)
    {
        double distkm;
        //sum EXP for G---------------------------------------------------
        double sumExp;
        double empP;
        double empG;
        double sh0 ;
        double sh1;

        sumExp = 0;
        int h = 3;

        for (int k = 0; k < flatZones.Length; k++)
        {
            IZone kz = flatZones[k];

            if (kz.InternalDistance == 0 || kz.TotalEmployment <= 0)
                continue;

            // empR = Math.Log((kz.RetailEmployment / 1000.0) + 0.001);
            empP = (kz.ProfessionalEmployment / 1000.0) + 1.0;
            empG = (kz.GeneralEmployment / 1000.0) + 1.0;
            distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;

            if (distkm >= 0 && distkm <= p[h, 5])
            {
                sh0 = 0.0;
                sh1 = 0.0;

                if (distkm >= 0 && distkm < 1) sh0 = 1;
                else if (distkm >= 1 && distkm < 2) sh1 = 1;

                sumExp += Math.Exp((p[h, 0] * distkm) + (p[h, 1] * Math.Log(empP)) + (p[h, 2] * Math.Log(empG)) + (p[h, 3] * sh0) + (p[h, 4] * sh1));
            }
        }

        //line.Append(sumExp);
        double cdf = 0.0;
        for (int k = 0; k < flatZones.Length; k++)
        {
            IZone kz = flatZones[k];

            if (kz.TotalEmployment > 0)
            {
                empP = Math.Log((kz.ProfessionalEmployment / 1000.0) + 1.0);
                empG = Math.Log((kz.GeneralEmployment / 1000.0) + 1.0);

                distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                if (distkm >= 0 && distkm <= p[h, 5] && kz.InternalDistance > 0)
                {
                    sh0 = 0.0;
                    sh1 = 0.0;

                    if (distkm >= 0 && distkm < 1) sh0 = 1;
                    else if (distkm >= 1 && distkm < 2) sh1 = 1;

                    cdf += Math.Exp((p[h, 0] * distkm) + (p[h, 1] * empP) + (p[h, 2] * empG) + (p[h, 3] * sh0) + (p[h, 4] * sh1)) / sumExp;
                }
            }
            line.Append(",");
            line.Append(cdf);
        }
    }

    private void BuildRetailWorkCache(StringBuilder line, IZone iz, float[,] p, IZone[] flatZones)
    {
        int h = 2;
        double distkm;
        //sum EXP for G---------------------------------------------------
        double sumExp = 0.0;
        double sh0;
        double sh1;
        double sh2;
        double empR;

        for (int k = 0; k < flatZones.Length; k++)
        {
            IZone kz = flatZones[k];

            if (kz.TotalEmployment > 0)
            {
                empR = Math.Log((kz.RetailEmployment / 1000.0) + 1.0);
                distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                if (distkm >= 0 && distkm <= p[h, 5] && kz.InternalDistance > 0)
                {
                    sh0 = 0.0;
                    sh1 = 0.0;
                    sh2 = 0.0;
                    if (distkm >= 0 && distkm < 1) sh0 = 1;
                    else if (distkm >= 1 && distkm < 2) sh1 = 1;
                    else if (distkm >= 2 && distkm < 3) sh2 = 1;

                    sumExp += Math.Exp(((p[h, 0] * distkm) + (p[h, 1] * empR) + (p[h, 2] * sh0) + (p[h, 3] * sh1) + (p[h, 4] * sh2)));
                }
            }
        }

        // line.Append(sumExp);
        //now calculate CDF sums from ... k .. num InternalZones

        double cdf = 0.0;
        for (int k = 0; k < flatZones.Length; k++)
        {
            IZone kz = flatZones[k];

            if (kz.TotalEmployment > 0)
            {
                empR = Math.Log((kz.RetailEmployment / 1000.0) + 1.0);

                distkm = ZoneSystem.Distances[iz.ZoneNumber, kz.ZoneNumber] / 1000;
                if (distkm >= 0 && distkm <= p[h, 5] && kz.InternalDistance > 0)
                {
                    sh0 = 0.0;
                    sh1 = 0.0;
                    sh2 = 0.0;
                    if (distkm >= 0 && distkm < 1) sh0 = 1;
                    else if (distkm >= 1 && distkm < 2) sh1 = 1;
                    else if (distkm >= 2 && distkm < 3) sh2 = 1;

                    cdf += Math.Exp(((p[h, 0] * distkm) + (p[h, 1] * empR) + (p[h, 2] * sh0) + (p[h, 3] * sh1) + (p[h, 4] * sh2))) / sumExp;
                }
            }
            line.Append(",");
            line.Append(cdf);
        }
    }
}