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
using System.IO.Compression;
using XTMF;

namespace TMG.Functions
{
    /// <summary>
    /// Provides several methods for compressing files and streams.
    /// </summary>
    public static class Compression
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="outputFileName"></param>
        /// <returns></returns>
        public static bool CompressFile(string fileName, string outputFileName = null)
        {
            FileStream inputStream;
            try
            {
                inputStream = File.OpenRead( fileName );
            }
            catch
            {
                return false;
            }
            using ( inputStream )
            {
                var directoryName = Path.GetDirectoryName(fileName);
                if (directoryName == null)
                {
                    throw new XTMFRuntimeException(null, $"Unable to get the directory name for the file path '{fileName}'");
                }
                return CompressStream( inputStream, outputFileName ?? Path.Combine(directoryName,
                    fileName + ".gz"));
            }
        }

        /// <summary>
        /// Compress a set of files, the output files will have the additional extension .gz
        /// </summary>
        /// <param name="files">The files to compress</param>
        /// <returns>If the compression is successful and completes, true.</returns>
        public static bool CompressFiles(ICollection<string> files)
        {
            foreach ( var file in files )
            {
                if ( !CompressFile( file ) )
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="outputFileName"></param>
        /// <returns></returns>
        public static bool CompressStream(Stream inputStream, string outputFileName)
        {
            FileStream writer = null;
            try
            {
                writer = new FileStream( outputFileName, FileMode.Create );
                using GZipStream stream = new GZipStream(writer, CompressionMode.Compress);
                writer = null;
                inputStream.CopyTo(stream);
            }
            catch ( Exception e )
            {
                throw new XTMFRuntimeException( null,e.Message );
            }
            finally
            {
                writer?.Dispose();
            }
            return true;
        }
    }
}