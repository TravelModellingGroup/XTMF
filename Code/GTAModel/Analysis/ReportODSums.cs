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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Analysis
{
    public class ReportODSums : ISelfContainedModule
    {
        [RunParameter( "Data Tags", "", "A string seperated by commas that we will use as labels for the data being read in.  There should be one for each data source." )]
        public string DataNames;

        [SubModelInformation( Description = "The different OD Data sources to read in.  They should be zonal data points.", Required = false )]
        public List<IDataSource<SparseTwinIndex<float>>> DataSources;

        [RunParameter( "Report File Name", "Report.csv", typeof( FileFromOutputDirectory ), "The location/name of the file to store this report." )]
        public FileFromOutputDirectory ReportFileName;

        [RootModule]
        public ITravelDemandModel Root;

        private string[] SplitNames;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( this.DataSources.Count == 0 )
            {
                return true;
            }
            this.SplitNames = this.DataNames.Split( ',' );
            if ( this.SplitNames.Length != this.DataSources.Count )
            {
                error = "In '" + this.Name + "' the number of data sources does not match the number of tags to label the data sources!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            if ( DataSources.Count == 0 ) return;
            using ( StreamWriter writer = new StreamWriter( ReportFileName.GetFileName() ) )
            {
                double globalIntrazonals = 0.0;
                double globalSum = 0.0;
                double globalIE = 0.0;
                double globalEI = 0.0;
                writer.WriteLine( "DataTag,Total,Intrazonals,Interzonals,InnerToExternal,ExternalToInner" );
                for ( int i = 0; i < this.DataSources.Count; i++ )
                {
                    ProcessDataSource( SplitNames[i], this.DataSources[i], writer, ref globalIntrazonals, ref globalSum, ref globalIE, ref globalEI );
                }
                writer.Write( "Totals:," );
                writer.Write( globalSum );
                writer.Write( ',' );
                writer.Write( globalIntrazonals );
                writer.Write( ',' );
                writer.Write( globalSum - globalIntrazonals );
                writer.Write( ',' );
                writer.Write( globalIE );
                writer.Write( ',' );
                writer.WriteLine( globalEI );
            }
        }

        private void ProcessDataSource(string dataTag, IDataSource<SparseTwinIndex<float>> dataSource, StreamWriter writer,
            ref double globalIntrazonals, ref double globalSum, ref double globalIE, ref double globalEI)
        {
            dataSource.LoadData();
            float intrazonals;
            float total;
            float ie;
            float ei;
            Sum( dataSource.GiveData().GetFlatData(), out total, out intrazonals, out ie, out ei );
            dataSource.UnloadData();
            writer.Write( dataTag );
            writer.Write( ',' );
            writer.Write( total );
            writer.Write( ',' );
            writer.Write( intrazonals );
            writer.Write( ',' );
            writer.Write( total - intrazonals );
            writer.Write( ',' );
            writer.Write( ie );
            writer.Write( ',' );
            writer.WriteLine( ei );
            globalIntrazonals += intrazonals;
            globalSum += total;
            globalIE += ie;
            globalEI += ei;
        }

        private void Sum(float[][] p, out float total, out float intrazonals, out float ie, out float ei)
        {
            var sum = 0.0;
            var sumie = 0.0;
            var sumei = 0.0;
            var localIntrazonals = 0.0;
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            for ( int i = 0; i < p.Length; i++ )
            {
                var local = 0.0;
                if ( zones[i].RegionNumber == 0 )
                {
                    for ( int j = 0; j < p[i].Length; j++ )
                    {
                        var addMe = p[i][j];
                        local += addMe;
                        sumei += addMe;
                    }
                }
                else
                {
                    for ( int j = 0; j < p[i].Length; j++ )
                    {
                        if ( zones[j].RegionNumber == 0 )
                        {
                            sumie += p[i][j];
                        }
                        local += p[i][j];
                    }
                }
                sum += local;
                localIntrazonals += p[i][i];
            }
            ie = (float)sumie;
            ei = (float)sumei;
            total = (float)sum;
            intrazonals = (float)localIntrazonals;
        }
    }
}