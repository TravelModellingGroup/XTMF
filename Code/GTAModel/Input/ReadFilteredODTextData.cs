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

using System.IO;
using TMG.GTAModel.DataUtility;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input;

[ModuleInformation( Description =
    @"This module is designed to read in .txt formats or .csv formats.  
It works be reading in the first four ‘columns’, where they could be separated by spaces, 
tabs, or commas.  It also tries to read in the standard header (an example is given in the ReadODTextData module).  
What makes this module different from the ReadODTextData is that it will filter out records based 
on the third column instead of returning all of the records.  This is useful when implementing the 
Non-Work/School trips to be able to filter out by occupation type."
    )]
public sealed class ReadFilteredODTextData : ReadODTextData
{
    [RunParameter( "Allowed Indexes", "1", typeof( NumberList ), "A list of category indexes that will be selected." )]
    public NumberList AllowedIndexes;

    protected override bool ReadDataLine(BinaryReader reader, out ODData<float> data)
    {
        char c = '\0';
        // Read in the origin
        data.O = 0;
        data.D = 0;
        data.Data = 0f;
        if (!ReadInteger(reader, ref c, out data.O)) return false;
        if ( !ReadInteger( reader, ref c, out data.D ) ) return false;
        // This line is what makes this special since we are now reading in a type to filter by
        if ( !ReadInteger( reader, ref c, out int type ) ) return false;
        if ( !ReadFloat( reader, ref c, out data.Data ) ) return false;
        // burn the remainder of the line
        if ( c != '\n' )
        {
            BurnLine( reader );
        }
        return ContainsType( type );
    }

    private bool ContainsType(int type)
    {
        return ( AllowedIndexes.Contains( type ) );
    }
}