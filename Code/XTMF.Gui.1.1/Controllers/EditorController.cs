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

namespace XTMF.Gui.Controllers
{
    internal static class EditorController
    {

        static SingleAccess<List<MainWindow>> OpenWindows = new SingleAccess<List<MainWindow>>(new List<MainWindow>());
        public static XTMFRuntime Runtime { get; private set; }

        internal static void Register(MainWindow window, Action OnComplete)
        {
            Task.Factory.StartNew(
                () =>
            {
                OpenWindows.Run((list) =>
               {
                   if (Runtime == null)
                   {
                       Runtime = new XTMFRuntime();
                       var loadError = ((Configuration)Runtime.Configuration).LoadError;
                       if (loadError != null)
                       {
                           window.Dispatcher.BeginInvoke( new Action(() =>
                          {
                              MessageBox.Show(window, loadError + "\r\nA copy of this error has been saved to your clipboard.", "Error Loading XTMF", MessageBoxButton.OK, MessageBoxImage.Error);
                              Clipboard.SetText(loadError);
                          }));
                       }
                   }
                   if (!list.Contains(window))
                   {
                       list.Add(window);
                   }
                   if (OnComplete != null)
                   {
                       OnComplete();
                   }
               });
            });
        }

        internal static void Unregister(MainWindow window)
        {
            OpenWindows.Run((list) => list.Remove(window));
        }

        internal static bool IsControlDown()
        {
            return (Keyboard.IsKeyDown(Key.LeftCtrl) | Keyboard.IsKeyDown(Key.RightCtrl));
        }

        internal static bool IsShiftDown()
        {
            return (Keyboard.IsKeyDown(Key.LeftShift) | Keyboard.IsKeyDown(Key.RightShift));
        }
    }
}
