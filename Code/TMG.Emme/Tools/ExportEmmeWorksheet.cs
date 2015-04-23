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
using System.Text;
using XTMF;
using TMG.Input;
using System.IO;

namespace TMG.Emme.Tools
{
    public enum NamedPaperSizes
    {
        A0, A1, A3, A4, A5, A6, A7, A8, A9, B0,
        B1, B2, B3, B4, B5, B6, B7, B8, B9, B10,
        C5E, COMM10E, DLE, EXECUTIVE, FOLIO,
        LEDGER, LETTER, LEGAL, TABLOID, CUSTOM
    }

    public enum PaperOrientation
    {
        PORTRAIT, LANDSCAPE
    }

    public enum SizeUnit
    {
        INCHES, MILLIMETERS
    }

    [ModuleInformation(Description = "Exports an Emme worksheet to a file (e.g. PDF or PNG). The network view can be " +
                        "configured to use the current view (not recommended) or a pre-specified view. Worksheet " +
                        "parameters can also be configured to allow custom meta-data or other information to be set " +
                        "individually per worksheet export.")]
    public class ExportEmmeWorksheet : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The number of the Emme Scenario to export.")]
        public int ScenarioNumber;

        [SubModelInformation(Required = true, Description = "The Emme worksheet (*.emw) to load and export.")]
        public FileLocation WorksheetFile;

        [SubModelInformation(Required = true, Description = "The image, PDF, or SVG file to export the worksheet as.")]
        public FileLocation OutputFile;

        [SubModelInformation(Required = false, Description = "The geographic View to export. If left out, the default view " +
                                "shall be used (usually the extents of the network")]
        public EmmeView View;

        [SubModelInformation(Required = false, Description = "A list of parameters to configure in the worksheet.")]
        public List<EmmeWorksheetParameter> Parameters;

        [SubModelInformation(Required = true, Description = "The type of file format to export.")]
        public EmmeWorksheetExportType ExportType;

        private const string _ToolName = "tmg.XTMF_internal.export_worksheet";
        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(255, 255, 255);

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if(mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController.");
            }

            var configuration = new Dictionary<string, object>();
            if(View == null)
            {
                configuration["view"] = "";
            }
            else
            {
                configuration["view"] = View.Confgiruation;
            }
            configuration["export_type"] = ExportType.Configuration;
            var paramList = new List<Dictionary<string, object>>();
            foreach(var param in Parameters)
            {
                paramList.Add(param.Configuration);
            }
            configuration["parameters"] = paramList;

            var builder = new StringBuilder();
            _DictToJSON(configuration, ref builder);
            var args = string.Join(" ", ScenarioNumber,
                                   "\"" + Path.GetFullPath(WorksheetFile.GetFilePath()) + "\"",
                                    "\"" + Path.GetFullPath(OutputFile.GetFilePath()) + "\"",
                                    "\"" + builder.ToString() + "\""
                                    );

            return mc.Run(_ToolName, args);
        }

        private void _DictToJSON(Dictionary<string, object> dict, ref StringBuilder builder)
        {
            builder.Append('{');

            foreach(var entry in dict)
            {
                var key = entry.Key;
                var val = entry.Value;

                builder.AppendFormat("'{0}':", key);

                var valAsDict = val as Dictionary<string, object>;
                var valAsList = val as IEnumerable<object>;

                if(valAsDict != null)
                {
                    _DictToJSON(valAsDict, ref builder);
                }
                else if(valAsList != null)
                {
                    _ListToJSON(valAsList, ref builder);
                }
                else
                {
                    //Remove all single and double quotations from the value to allow proper passing of the string to Emme.
                    builder.AppendFormat("'{0}'", val.ToString().Replace("'", "").Replace("\"", ""));
                }
                builder.Append(',');
            }
            builder.Remove(builder.Length - 1, 1);
            builder.Append('}');
        }

        private void _ListToJSON(IEnumerable<object> list, ref StringBuilder builder)
        {
            builder.Append('[');

            int nItems = 0;
            foreach(var val in list)
            {
                var valAsDict = val as Dictionary<string, object>;
                if(valAsDict != null)
                {
                    _DictToJSON(valAsDict, ref builder);
                }
                else
                {
                    builder.AppendFormat("'{0}'", val);
                }

                nItems++;
                builder.Append(',');
            }

            if(nItems > 0)
            {
                builder.Remove(builder.Length - 1, 1); //Get rid of the last comma
            }

            builder.Append(']');
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0.0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    #region Emme View Classes

    public abstract class EmmeView : IModule
    {

        internal abstract Dictionary<string, object> Confgiruation { get; }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 1.0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { throw new NotImplementedException(); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    [ModuleInformation(Description = "Retrieves an Emme view from a name. This view must be present inside of the " +
                                    "Emme Project's Views directory, otherwise an error will occur. This is the " +
                                    "recommended approach for exporting worksheets, since the Emme Desktop application " +
                                    "facilitates the creation of views in this manner.")]
    public class ViewFromName : EmmeView
    {
        [RunParameter("Name", "", "The name of the view within the Emme Project directory.")]
        public string ViewName;

        [RunParameter("Parent Folders", "", "Enter a list of parent folders, separated by semicolons")]
        public string ParentFolders;

        internal override Dictionary<string, object> Confgiruation
        {
            get
            {
                var dict = new Dictionary<string, object>();
                dict["name"] = ViewName;
                dict["parent_folders"] = ParentFolders;
                dict["type"] = "EXPLORER";
                return dict;
            }
        }
    }

    [ModuleInformation(Description = "Retrieves an Emme view from a file location anywhere on the computer, or in the " +
                                    "XTMF Project file structure. This is useful for managing views within XTMF Projects " +
                                    "independent of what might exist inside of the Emme Project.")]
    public class ViewFromFile : EmmeView
    {
        [SubModelInformation(Description = "EMV File", Required = true)]
        public FileLocation ViewFile;

        internal override Dictionary<string, object> Confgiruation
        {
            get
            {
                var dict = new Dictionary<string, object>();
                dict["file_path"] = Path.GetFullPath(ViewFile.GetFilePath());
                dict["type"] = "FILE";
                return dict;
            }
        }
    }

    [ModuleInformation(Description = "Generates an Emme view from a set of coordinates (x0, y0, x1, y1). In order for " +
                                    "this to be used successfully, the coordinates should match the coordinate system " +
                                    "of the Emme Project. This module will <em>not</em> project your coordinates for you!")]
    public class ViewFromBox : EmmeView
    {
        [RunParameter("X Min", 0.0f, "Minimum x-coordinate.")]
        public float XMin;

        [RunParameter("X Max", 0.0f, "MAximum x-coordinate.")]
        public float XMax;

        [RunParameter("Y Min", 0.0f, "Minimum y-coordinate.")]
        public float YMin;

        [RunParameter("Y Max", 0.0f, "Maximum y-coordinate.")]
        public float YMax;


        internal override Dictionary<string, object> Confgiruation
        {
            get
            {
                var dict = new Dictionary<string, object>();
                dict["x0"] = XMin; dict["x1"] = XMax;
                dict["y0"] = YMin; dict["y1"] = YMax;
                dict["type"] = "BOX";
                return dict;
            }
        }
    }

    #endregion

    #region Parameters

    [ModuleInformation(Description = "Targets and sets the value of an Emme worksheet Parameter. This can be used to " +
                                    "customize variables visible during the export process. Parameter names are specific " +
                                    "to layer types used in Emme Desktop (an example layer is 'Link Base'), and can be " +
                                    "discovered by right-clicking on their name in the layer/worksheet editor positioned " +
                                    "to the right in the Desktop. For example, Link Base layers have an 'Offset' parameter " +
                                    "which controls the thickness of the link drawings.")]
    public class EmmeWorksheetParameter : IModule
    {
        [RunParameter("Layer Name", "", "The name of the layer (if the layer is visible in the worksheet configuration bar, " +
                        "its name will be visible. One or both of Layer Type and Layer name must be specified in order to " +
                        "select a layer.")]
        public string LayerName;

        [RunParameter("Layer Type", "", "The type of the layer (e.g. 'Link base'). One or both of Layer Type and Layer " +
                        "Name must be specified in order to select a layer.")]
        public string LayerType;

        [RunParameter("Parameter Name", "", "The name of the parameter whose value to set.")]
        public string ParameterName;

        [RunParameter("Value", "", "The value to assign to the specified layer's specified parameter. It must be " +
                        "parsable to the correct value type. Be sure that numeric parameters get numeric values!")]
        public string Value;

        [RunParameter("Index", 0, "For multi-value parameters, the index of the value to set.")]
        public int Index;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(255, 255, 255);

        internal Dictionary<string, object> Configuration
        {
            get
            {
                var dict = new Dictionary<string, object>();
                if(!string.IsNullOrWhiteSpace(LayerName))
                {
                    dict["layer_name"] = LayerName;
                }
                if(!string.IsNullOrWhiteSpace(LayerType))
                {
                    dict["layer_type"] = LayerType;
                }
                dict["par_name"] = ParameterName;
                dict["value"] = Value;
                dict["index"] = Index;

                return dict;
            }
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0.0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    #endregion

    #region Worksheet Export Types

    public abstract class EmmeWorksheetExportType : IModule
    {

        internal abstract Dictionary<string, object> SubConfiguration { get; }

        internal Dictionary<string, object> Configuration
        {
            get
            {
                var dict = SubConfiguration;
                dict["detail"] = Detail;
                dict["type"] = GetType().Name;
                return dict;
            }
        }

        [RunParameter("Details", 1.0f, "A detail factor, from 1.0 to 8.0. The higher this number, the more detailed " +
                "(zoomed in) the resulting image will be.")]
        public float Detail;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0.0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { throw new NotImplementedException(); }
        }

        protected virtual bool LocalRuntimeValidation(ref string error) { return true; }

        public bool RuntimeValidation(ref string error)
        {
            if(Detail > 8.0f || Detail < 1.0f)
            {
                error = "In '" + Name + "' Worksheet export detail must be between 1.0 and 8.0 (got " + Detail + ").";
                return false;
            }
            return LocalRuntimeValidation(ref error);
        }
    }

    #region Image Export Types

    [ModuleInformation(Description = "Saves the worksheet as an image file. Supported types are JPEG, PNG, and BMP.")]
    public abstract class ImageExportType : EmmeWorksheetExportType
    {

        internal abstract string ImageFormat { get; }

        [RunParameter("Width", 256, "The width of the image, in pixels.")]
        public int Width;

        [RunParameter("Height", 256, "The height of the image, in pixels.")]
        public int Height;

        [RunParameter("Margin Top", 0.0f, "The top margin, as a fraction of the image size (0.0 to 1.0)")]
        public float MarginTop;

        [RunParameter("Margin Left", 0.0f, "The left margin, as a fraction of the image size (0.0 to 1.0)")]
        public float MarginLeft;

        [RunParameter("Margin Right", 0.0f, "The right margin, as a fraction of the image size (0.0 to 1.0)")]
        public float MarginRight;

        [RunParameter("Margin Bottom", 0.0f, "The bottom margin, as a fraction of the image size (0.0 to 1.0)")]
        public float MarginBottom;

        internal override Dictionary<string, object> SubConfiguration
        {
            get
            {
                var dict = new Dictionary<string, object>();
                dict["width"] = Width; dict["height"] = Height;
                dict["margin_top"] = MarginTop; dict["margin_bottom"] = MarginBottom;
                dict["margin_left"] = MarginLeft; dict["margin_right"] = MarginRight;
                dict["format"] = ImageFormat;
                dict["type"] = "IMAGE";
                return dict;
            }
        }
    }

    public class JPEGType : ImageExportType
    {
        [RunParameter("Quality", 90, "The quality of the JPEG file, an integer from 0 to 100")]
        public int Quality;

        override protected bool LocalRuntimeValidation(ref string error)
        {
            if(Quality < 0 || Quality > 100)
            {
                error = "In '" + Name + "' The 'Quality' attribute must be between 0 and 100.";
                return false;
            }
            return true;
        }

        internal override string ImageFormat
        {
            get { return "JPG"; }
        }

        internal override Dictionary<string, object> SubConfiguration
        {
            get
            {
                var dict = base.SubConfiguration;
                dict["quality"] = Quality;
                return dict;
            }
        }
    }

    public class PNGType : ImageExportType
    {
        internal override string ImageFormat
        {
            get { return "PNG"; }
        }
    }

    public class BMPType : ImageExportType
    {
        internal override string ImageFormat
        {
            get { return "BMP"; }
        }
    }

    #endregion


    [ModuleInformation(Description = "Saves the worksheet as Scalable Vector Graphics (SVG) format.")]
    public class SVGExportType : EmmeWorksheetExportType
    {
        [RunParameter("Width", 8.5f, "The width of the SVG, specified in inches or millimeters (see Size Unit).")]
        public float Width;

        [RunParameter("Height", 8.5f, "The height of the SVG, specified in inches or millimeters (see Size Unit).")]
        public float Height;

        [RunParameter("Size Unit", SizeUnit.INCHES, "The unit of measurement for the specified size. One of INCHES or MILLIMETERS.")]
        public SizeUnit Unit;

        [RunParameter("Margin Top", 0.0f, "The top margin, in size units (see Size Unit).")]
        public float MarginTop;

        [RunParameter("Margin Left", 0.0f, "The left margin, in size units (see Size Unit).")]
        public float MarginLeft;

        [RunParameter("Margin Right", 0.0f, "The right margin, in size units (see Size Unit).")]
        public float MarginRight;

        [RunParameter("Margin Bottom", 0.0f, "The bottom margin, in size units (see Size Unit).")]
        public float MarginBottom;

        internal override Dictionary<string, object> SubConfiguration
        {
            get
            {
                var dict = new Dictionary<string, object>();
                dict["width"] = Width; dict["height"] = Height;
                dict["margin_top"] = MarginTop; dict["margin_bottom"] = MarginBottom;
                dict["margin_left"] = MarginLeft; dict["margin_right"] = MarginRight;
                dict["unit"] = Unit.ToString();
                dict["type"] = "SVG";
                return dict;
            }
        }
    }

    [ModuleInformation(Description = "Saves the worksheet as an Adobe Portable Document File (PDF) format.")]
    public class PDFExportType : EmmeWorksheetExportType
    {
        [RunParameter("Margin Top", 0, "The top margin, in millimeters.")]
        public int MarginTop;

        [RunParameter("Margin Left", 0, "The left margin, in millimeters.")]
        public int MarginLeft;

        [RunParameter("Margin Right", 0, "The right margin, in millimeters.")]
        public int MarginRight;

        [RunParameter("Margin Bottom", 0, "The bottom margin, in millimeters.")]
        public int MarginBottom;

        [RunParameter("Extend to Margins", true, "If true, the view is extended to the edge of the margins. If false, the " +
                        "current worksheet view aspect ratio is maintained.")]
        public bool ExtendToMargins;

        [RunParameter("Paper Size", NamedPaperSizes.LETTER, "Paper size. Can be set to CUSTOM or one of the following: " +
                        "A0, A1, A3, A4, A5, A6, A7, A8, A9, B0, B1, B2, B3, B4, B5, B6, B7, B8, B9, B10, C5E, COMM10E, " +
                        "DLE, EXECUTIVE, FOLIO, LEDGER, LETTER, LEGAL, or TABLOID.")]
        public NamedPaperSizes PaperSize;

        [RunParameter("Orientation", PaperOrientation.LANDSCAPE, "Paper orientation. Can be either PORTRAIT or LANDSCAPE.")]
        public PaperOrientation Orientation;

        [RunParameter("Custom Width", 11f, "If the Paper Size is set to CUSTOM, this will specify the paper width (in " +
                        "Custom Units")]
        public float CustomWidth;

        [RunParameter("Custom Height", 8.5f, "If the Paper Size is set to CUSTOM, this will specify the paper height (in " +
                        "Custom Units")]
        public float CustomHeight;

        [RunParameter("Custom Units", SizeUnit.INCHES, "If the paper size is set to CUSTOM, this will specify the units of " +
                        "width and height.")]
        public SizeUnit CustomUnit;

        internal override Dictionary<string, object> SubConfiguration
        {
            get
            {
                var dict = new Dictionary<string, object>();
                dict["margin_top"] = MarginTop; dict["margin_bottom"] = MarginBottom;
                dict["margin_left"] = MarginLeft; dict["margin_right"] = MarginRight;
                dict["extend_to_margins"] = ExtendToMargins.ToString();
                dict["orientation"] = Orientation.ToString();
                dict["paper_size"] = PaperSize.ToString();
                if(PaperSize == NamedPaperSizes.CUSTOM)
                {
                    var subduct = new Dictionary<string, object>();
                    subduct["height"] = CustomHeight;
                    subduct["width"] = CustomWidth;
                    subduct["unit"] = CustomUnit.ToString();
                    dict["custom_size"] = subduct;
                }
                return dict;
            }
        }
    }

    #endregion



}
