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

namespace Datastructure
{
    public class ODMatrixWriter<T> : ODCCreator2<T>
    {
        public ODMatrixWriter(SparseArray<T> reference, int types, int times)
            : base( reference, types, times )
        {
        }

        public string AdditionalDescription { get; set; }

        public string EndTimesHeader { get; set; }

        public string Modes { get; set; }

        public string StartTimesHeader { get; set; }

        public string TypeHeader { get; set; }

        public int Year { get; set; }

        protected override Dictionary<string, string> GetMetaData()
        {
            var ret = new Dictionary<string, string>( 6 );
            ret["Year"] = Year.ToString();
            ODMatrixWriter<T>.AddIfNotNull( "StartTime", this.StartTimesHeader, ret );
            ODMatrixWriter<T>.AddIfNotNull( "EndTime", this.EndTimesHeader, ret );
            ODMatrixWriter<T>.AddIfNotNull( "Types", this.TypeHeader, ret );
            ODMatrixWriter<T>.AddIfNotNull( "Modes", this.Modes, ret );
            ODMatrixWriter<T>.AddIfNotNull( "Description", this.AdditionalDescription, ret );
            return ret;
        }

        private static void AddIfNotNull(string key, string value, Dictionary<string, string> store)
        {
            if ( !String.IsNullOrWhiteSpace( value ) )
            {
                store[key] = value;
            }
        }
    }
}