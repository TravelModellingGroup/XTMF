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
using TMG.Input;
using Datastructure;
using System.IO;
namespace TMG.GTAModel.Analysis
{
    public class ExtractWorkers : ISelfContainedModule
    {
        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation( Required = true, Description = "The resource containing the worker information." )]
        public IResource WorkerResource;

        [SubModelInformation( Required = true, Description = "The place to save the file to." )]
        public FileLocation OututFile;

        public void Start()
        {
            var zones = this.Root.ZoneSystem.ZoneArray;
            var workerData = this.WorkerResource.AquireResource<SparseArray<SparseTriIndex<float>>>();
            var flatZones = zones.GetFlatData();
            var flatWorkerData = workerData.GetFlatData();
            using ( StreamWriter writer = new StreamWriter( this.OututFile.GetFilePath() ) )
            {
                writer.WriteLine("Zone,PD,Region,EmpStat,Mobility,AgeCat,Persons");
                for ( int i = 0; i < flatWorkerData.Length; i++ )
                {
                    foreach(var validI in flatWorkerData[i].ValidIndexes())
                    {
                        foreach( var validJ in flatWorkerData[i].ValidIndexes(validI))
                        {
                            foreach ( var validK in flatWorkerData[i].ValidIndexes( validI, validJ ) )
                            {
                                writer.Write( flatZones[i].ZoneNumber );
                                writer.Write( ',' );
                                writer.Write( flatZones[i].PlanningDistrict );
                                writer.Write( ',' );
                                writer.Write( flatZones[i].RegionNumber );
                                writer.Write( ',' );
                                writer.Write( validI );
                                writer.Write( ',' );
                                writer.Write( validJ );
                                writer.Write( ',' );
                                writer.Write( validK );
                                writer.Write( ',' );
                                writer.WriteLine( flatWorkerData[i][validI, validJ, validK] );
                            }
                        }
                    }
                }
            }
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
            if ( !this.WorkerResource.CheckResourceType<SparseArray<SparseTriIndex<float>>>() )
            {
                error = "In '" + this.Name + "' the Worker resource was not of type SparseArray<SparseTriIndex<float>>!";
            }
            return true;
        }
    }
}
