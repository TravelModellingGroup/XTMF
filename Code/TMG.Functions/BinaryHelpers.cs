/*
    Copyright 2014-2022 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO.Compression;
using TMG.Input;
using XTMF;

namespace TMG.Functions
{
    public static class BinaryHelpers
    {

        /// <summary>
        /// Executes the given method within the context of the writer.
        /// This will automatically compress the stream if the extension is .gz.
        /// </summary>
        /// <param name="module">The module that is creating the writer.</param>
        /// <param name="toRun">The method that will use the writer.</param>
        /// <param name="fileName">The path to the file to write to.</param>
        /// <exception cref="XTMFRuntimeException">Can throw a captured IOException.</exception>
        [Obsolete("Please use ExecuteWriter(IModule, Action<BinaryWriter>, string)")]
        public static void ExecuteWriter(Action<BinaryWriter> toRun, string fileName)
        {
            using (var writer = CreateWrite(null, fileName))
            {
                toRun(writer);
            }
        }

        /// <summary>
        /// Executes the given method within the context of the writer.
        /// This will automatically compress the stream if the extension is .gz.
        /// </summary>
        /// <param name="module">The module that is creating the writer.</param>
        /// <param name="toRun">The method that will use the writer.</param>
        /// <param name="fileName">The path to the file to write to.</param>
        /// <exception cref="XTMFRuntimeException">Can throw a captured IOException.</exception>
        public static void ExecuteWriter(IModule module, Action<BinaryWriter> toRun, string fileName)
        {
            using (var writer = CreateWrite(module, fileName))
            {
                toRun(writer);
            }
        }

        /// <summary>
        /// Creates a new binary writer to the given file path.
        /// This will automatically compress the stream if the extension is .gz
        /// </summary>
        /// <param name="module">The module that is creating the writer.</param>
        /// <param name="fileName">The path to the file to write to.</param>
        /// <returns>The binary writer stream.  Make sure to dispose this.</returns>
        /// <exception cref="XTMFRuntimeException">Can throw a captured IOException.</exception>
        public static BinaryWriter CreateWrite(IModule module, string fileName)
        {
            Stream file = null;
            try
            {
                file = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                if (Path.GetExtension(fileName)?.Equals(".gz", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var decompressionStream = new GZipStream(file, CompressionMode.Compress, false);
                    var writer = new BinaryWriter(decompressionStream, System.Text.Encoding.Default, false);
                    file = null;
                    return writer;
                }
                else
                {
                    var writer = new BinaryWriter(file, System.Text.Encoding.Default, false);
                    file = null;
                    return writer;
                }
            }
            catch (IOException e)
            {
                file?.Dispose();
                throw new XTMFRuntimeException(module, e, $"Unable to write the file located at '{fileName}'!");
            }
        }

        /// <summary>
        /// Executes the given function in an 
        /// </summary>
        /// <param name="module"></param>
        /// <param name="toRun"></param>
        /// <param name="fileName"></param>
        /// <exception cref="XTMFRuntimeException">Can throw a captured IOException.</exception>
        public static void ExecuteReader(IModule module, Action<BinaryReader> toRun, string fileName)
        {
            using (var reader = CreateReader(module, fileName))
            {
                toRun(reader);
            }
        }

        /// <summary>
        /// Creates a new reader for binary data.
        /// If the file has the extension .gz it will be automatically decompressed.
        /// </summary>
        /// <param name="module">The module that is requesting this operation.</param>
        /// <param name="fileName">The file path to read from.</param>
        /// <returns>A binary reader for the given file path.</returns>
        /// <exception cref="XTMFRuntimeException">Can throw a captured IOException.</exception>
        public static BinaryReader CreateReader(IModule module, string fileName)
        {
            Stream file = null;
            try
            {
                file = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                // Check to see if the binary file is compressed
                if (Path.GetExtension(fileName)?.Equals(".gz", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var decompressionStream = new GZipStream(file, CompressionMode.Decompress, false);
                    var reader = new BinaryReader(decompressionStream, System.Text.Encoding.Default, false);
                    file = null;
                    return reader;
                }
                else
                {
                    var reader = new BinaryReader(file);
                    file = null;
                    return reader;
                }
            }
            catch (IOException e)
            {
                file?.Dispose();
                if (!File.Exists(fileName))
                {
                    throw new XTMFRuntimeException(module, e, $"File not found at '{fileName}'!");
                }
                throw new XTMFRuntimeException(module, e, $"Unable to read the file located at '{fileName}'!");
            }
        }
    }
}
