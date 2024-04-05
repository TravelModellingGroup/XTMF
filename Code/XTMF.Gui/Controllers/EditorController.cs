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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace XTMF.Gui.Controllers;

internal static class EditorController
{
    static readonly SingleAccess<List<MainWindow>> OpenWindows = new([]);

    public static XTMFRuntime Runtime { get; private set; }

    public static bool UseRemoteHost { get; set; } = false;

    internal static void Register(MainWindow window, Action OnComplete, bool loadModules = true)
    {
        Task.Factory.StartNew(
            () =>
        {
            OpenWindows.Run((list) =>
           {
               if (Runtime == null)
               {
                   Runtime = new XTMFRuntime();
                   Runtime.RunController.ErrorLaunchingModel += (errorMessage) =>
                   {
                       window.Dispatcher.Invoke(() =>
                       {
                           MessageBox.Show(window, errorMessage, "Error running Model System", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                       });
                   };
                   var loadError = ((Configuration)Runtime.Configuration).LoadError;
                   window.Dispatcher.BeginInvoke(new Action(() =>
                 {
                     window.Title = "XTMF Version " + Runtime.Configuration.XTMFVersion.Major + "." +Runtime.Configuration.XTMFVersion.Minor;
                     if (loadError != null)
                     {
                         MessageBox.Show(window, loadError + "\r\nA copy of this error has been saved to your clipboard.", "Error Loading XTMF", MessageBoxButton.OK, MessageBoxImage.Error);
                         Clipboard.SetText(loadError);
                     }
                 }));
               }
               if (!list.Contains(window))
               {
                   list.Add(window);
               }
               OnComplete?.Invoke();
           });
        });
    }

    public static void FreeRuntime()
    {
        Runtime?.Dispose();
        Runtime = null;
    }

    internal static void Unregister(MainWindow window)
    {
        OpenWindows.Run((list) =>
            {
                list.Remove(window);
            });
    }

    internal static bool IsControlDown() => (Keyboard.IsKeyDown(Key.LeftCtrl) | Keyboard.IsKeyDown(Key.RightCtrl));

    internal static bool IsShiftDown() => (Keyboard.IsKeyDown(Key.LeftShift) | Keyboard.IsKeyDown(Key.RightShift));

    internal static bool IsAltDown() => (Keyboard.IsKeyDown(Key.LeftAlt) | Keyboard.IsKeyDown(Key.RightAlt));
}
