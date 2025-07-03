/*
    Copyright 2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using Datastructure;
using System;
using TMG.Input;
using System.IO;
using TMG.Emme;
using TMG.Functions;
using System.Linq;
using XTMF;

namespace TMG.Frameworks.Data.Loading;

public sealed class LoadZoneSystemFromEMMEMatrix : IZoneSystem
{
    public SparseTwinIndex<float> Distances => throw new XTMFRuntimeException(this, "No distances are available.");

    public int NumberOfExternalZones => throw new XTMFRuntimeException(this, "No external zones are available.");

    public int NumberOfInternalZones => throw new XTMFRuntimeException(this, "No internal zones are available.");

    public int NumberOfZones => ZoneArray.Count;

    public int RoamingZoneNumber { get; set; }

    public SparseArray<IZone> ZoneArray => _data;

    private SparseArray<IZone> _data;

    [SubModelInformation(Required = true, Description = "The EMME Matrix to get the zone system from.")]
    public FileLocation EMMEMatrixFileLocation;

    public IZone Get(int zoneNumber)
    {
        return _data[zoneNumber] ?? throw new XTMFRuntimeException(this, $"Zone {zoneNumber} does not exist in the zone system.");
    }

    public IZoneSystem GiveData()
    {
        return this;
    }

    public bool Loaded => _data is not null;

    public void LoadData()
    {
        // Don't double load.
        if (Loaded) return;

        lock (this)
        {
            if (Loaded) return;

            if (EMMEMatrixFileLocation.IsPathEmpty())
            {
                throw new XTMFRuntimeException(this, "The EMME Matrix file location is empty.");
            }
            if (!File.Exists(EMMEMatrixFileLocation.GetFilePath()))
            {
                throw new XTMFRuntimeException(this, $"The EMME Matrix file does not exist at {EMMEMatrixFileLocation.GetFilePath()}.");
            }
            using var reader = BinaryHelpers.CreateReader(this, EMMEMatrixFileLocation);
            var matrix = new EmmeMatrix(reader);
            // We really only need one dimension, throw an exception if we found a scalar.
            if (matrix.Indexes.Length < 1)
            {
                throw new XTMFRuntimeException(this, "The EMME Matrix does not contain any zones.");
            }
            var zoneIndexes = matrix.Indexes[0];
            SparseArray<IZone> data = SparseArray<IZone>.CreateSparseArray(zoneIndexes, ConvertToZones(zoneIndexes));
            _data = data;
        }
    }

    private IZone[] ConvertToZones(int[] zoneIndexes)
    {
        return [.. zoneIndexes.Select(index => new SimpleZone(index))];
    }

    private class SimpleZone : IZone
    {
        public SimpleZone(int zoneNumber)
        {
            ZoneNumber = zoneNumber;
        }

        public float ArterialRoadRatio { get; set; }
        public float Employment { get; set; }
        public float GeneralEmployment { get; set; }
        public float InternalArea { get; set; }
        public float InternalDistance { get; set; }
        public float IntrazonalDensity { get; set; }
        public float ManufacturingEmployment { get; set; }
        public float OtherActivityLevel { get; set; }
        public float ParkingCost { get; set; }
        public int PlanningDistrict { get; set; }
        public int Population { get; set; }
        public float ProfessionalEmployment { get; set; }
        public int RegionNumber { get; set; }
        public float RetailActivityLevel { get; set; }
        public float RetailEmployment { get; set; }
        public float TotalEmployment { get; set; }
        public float UnknownEmployment { get; set; }
        public float WorkActivityLevel { get; set; }
        public float WorkGeneral { get; set; }
        public float WorkManufacturing { get; set; }
        public float WorkProfessional { get; set; }
        public float WorkRetail { get; set; }
        public float WorkUnknown { get; set; }
        public float X { get; set; }
        public float Y { get; set; }

        public int ZoneNumber { get; private set; }
    }

    public void UnloadData()
    {
        _data = null;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

}
