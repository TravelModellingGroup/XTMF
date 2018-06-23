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
using Microsoft.Win32;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace XTMF.Gui
{
    internal static class Win32Helper
    {
        internal struct ShowDialogResult
        {
            public bool Result { get; set; }
            public string FileName { get; set; }
        }

        internal static class VistaDialog
        {
            private const string c_foldersFilter = "Folders|\n";
            private const BindingFlags c_flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            private readonly static Assembly s_windowsFormsAssembly;
            private readonly static Type s_iFileDialogType;
            private readonly static MethodInfo s_createVistaDialogMethodInfo;
            private readonly static MethodInfo s_onBeforeVistaDialogMethodInfo;
            private readonly static MethodInfo s_getOptionsMethodInfo;
            private readonly static MethodInfo s_setOptionsMethodInfo;
            private readonly static uint s_fosPickFoldersBitFlag;
            private readonly static ConstructorInfo s_vistaDialogEventsConstructorInfo;
            private readonly static MethodInfo s_adviseMethodInfo;
            private readonly static MethodInfo s_unAdviseMethodInfo;
            private readonly static MethodInfo s_showMethodInfo;

            static VistaDialog()
            {
                s_createVistaDialogMethodInfo = typeof(System.Windows.Forms.OpenFileDialog).GetMethod("CreateVistaDialog", c_flags);
                s_onBeforeVistaDialogMethodInfo = typeof(System.Windows.Forms.OpenFileDialog).GetMethod("OnBeforeVistaDialog", c_flags);
                s_getOptionsMethodInfo = typeof(System.Windows.Forms.FileDialog).GetMethod("GetOptions", c_flags);
                s_windowsFormsAssembly = typeof(System.Windows.Forms.FileDialog).Assembly;
                s_iFileDialogType = s_windowsFormsAssembly.GetType("System.Windows.Forms.FileDialogNative+IFileDialog");
                s_setOptionsMethodInfo = s_iFileDialogType.GetMethod("SetOptions", c_flags);
                s_fosPickFoldersBitFlag = (uint)s_windowsFormsAssembly
                    .GetType("System.Windows.Forms.FileDialogNative+FOS")
                    .GetField("FOS_PICKFOLDERS")
                    .GetValue(null);
                s_vistaDialogEventsConstructorInfo = s_windowsFormsAssembly
                    .GetType("System.Windows.Forms.FileDialog+VistaDialogEvents")
                    .GetConstructor(c_flags, null, new[] { typeof(System.Windows.Forms.FileDialog) }, null);
                s_adviseMethodInfo = s_iFileDialogType.GetMethod("Advise");
                s_unAdviseMethodInfo = s_iFileDialogType.GetMethod("Unadvise");
                s_showMethodInfo = s_iFileDialogType.GetMethod("Show");
            }

            public static ShowDialogResult Show(IntPtr ownerHandle, string initialDirectory, string title)
            {
                var openFileDialog = new System.Windows.Forms.OpenFileDialog()
                {
                    AddExtension = false,
                    CheckFileExists = false,
                    DereferenceLinks = true,
                    Filter = c_foldersFilter,
                    InitialDirectory = initialDirectory,
                    Multiselect = false,
                    Title = title
                };

                var iFileDialog = s_createVistaDialogMethodInfo.Invoke(openFileDialog, new object[] { });
                s_onBeforeVistaDialogMethodInfo.Invoke(openFileDialog, new[] { iFileDialog });
                s_setOptionsMethodInfo.Invoke(iFileDialog, new object[] { (uint)s_getOptionsMethodInfo.Invoke(openFileDialog, new object[] { }) | s_fosPickFoldersBitFlag });
                var adviseParametersWithOutputConnectionToken = new[] { s_vistaDialogEventsConstructorInfo.Invoke(new object[] { openFileDialog }), 0U };
                s_adviseMethodInfo.Invoke(iFileDialog, adviseParametersWithOutputConnectionToken);

                try
                {
                    int retVal = (int)s_showMethodInfo.Invoke(iFileDialog, new object[] { ownerHandle });
                    return new ShowDialogResult
                    {
                        Result = retVal == 0,
                        FileName = openFileDialog.FileName
                    };
                }
                finally
                {
                    s_unAdviseMethodInfo.Invoke(iFileDialog, new[] { adviseParametersWithOutputConnectionToken[1] });
                }
            }
        }
    }
}
