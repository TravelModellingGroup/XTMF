/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using XTMF;
using TMG.Input;
using Microsoft.Office.Interop.Excel;
using System.Runtime.InteropServices;
using System.IO;

namespace TMG.Frameworks.Office
{
    [ModuleInformation(Description =
        "This module converts a CSV file to an XLSX file using Microsoft Excel.  Excel 14+ is required for this to operate."
        )]
    public class ConvertCSVToXLSX : ISelfContainedModule
    {

        public class ToConvert : XTMF.IModule
        {
            [SubModelInformation(Required = true, Description = "The CSV file to read in.")]
            public FileLocation InputFile;

            [SubModelInformation(Required = true, Description = "The XLSX file to read write out.")]
            public FileLocation OutputFile;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = false, Description = "The files to convert from CSV to XLSX")]
        public List<ToConvert> FilesToConvert;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            var excel = new Application();
            Workbooks workbooks = null;
            // be very careful here to make sure that excel is actually going to close properly
            try
            {
                workbooks = excel.Workbooks;
                foreach(var toConvert in FilesToConvert)
                {

                    var inPath = toConvert.InputFile.GetFilePath();
                    if(Directory.Exists(inPath))
                    {
                        SaveDirectory(workbooks, new DirectoryInfo(inPath), new DirectoryInfo(toConvert.OutputFile.GetFilePath()));
                    }
                    else
                    {
                        SaveWorkbook(workbooks, inPath, toConvert.OutputFile.GetFilePath());
                    }

                }
            }
            finally
            {
                if(workbooks != null)
                {
                    workbooks.Close();
                    Marshal.FinalReleaseComObject(workbooks);
                }
                excel.Quit();
                Marshal.FinalReleaseComObject(excel);
            }
        }

        private void SaveDirectory(Workbooks workbooks, DirectoryInfo currentInputDirectory, DirectoryInfo currentOutputDirectory)
        {
            foreach(var subDir in currentInputDirectory.GetDirectories())
            {
                SaveDirectory(workbooks, subDir, currentOutputDirectory.CreateSubdirectory(subDir.Name));
            }
            foreach(var file in currentInputDirectory.GetFiles( "*.csv", SearchOption.TopDirectoryOnly))
            {
                if(!currentOutputDirectory.Exists)
                {
                    currentOutputDirectory.Create();
                }
                SaveWorkbook(workbooks, file.FullName, Path.Combine(currentOutputDirectory.FullName, Path.GetFileNameWithoutExtension(file.Name) + ".xlsx"));
            }
        }

        private static void SaveWorkbook(Workbooks workbooks, string inPath, string outPath)
        {
            Workbook workbook = null;
            try
            {
                workbook = workbooks.Open(inPath);
                workbook.SaveAs(outPath, XlFileFormat.xlWorkbookDefault);
            }
            finally
            {
                if(workbook != null)
                {
                    workbook.Close();
                    Marshal.FinalReleaseComObject(workbook);
                }
            }
        }
    }

}
