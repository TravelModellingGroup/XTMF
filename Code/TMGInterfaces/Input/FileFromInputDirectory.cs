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
using System.IO;

namespace TMG.Input;

public class FileFromInputDirectory
{
    private string FileName;

    public static bool TryParse(ref string error, string input, out FileFromInputDirectory output)
    {
        var length = input.Length;
        var invalidCharacters = Path.GetInvalidPathChars();
        for ( int i = 0; i < length; i++ )
        {
            var c = input[i];
            for ( int j = 0; j < invalidCharacters.Length; j++ )
            {
                if ( c == invalidCharacters[j] )
                {
                    error = "At position " + i + ", we found an invalid character '" + invalidCharacters[j] + "'!";
                    output = null;
                    return false;
                }
            }
        }
        output = new FileFromInputDirectory() { FileName = input };

        return true;
    }

    public bool ContainsFileName()
    {
        return !String.IsNullOrWhiteSpace( FileName );
    }

    public string GetFileName(string inputFileDirectory)
    {
        return Path.IsPathRooted( FileName ) ? FileName :
            Path.Combine( inputFileDirectory, FileName );
    }

    public override string ToString()
    {
        return FileName;
    }
}