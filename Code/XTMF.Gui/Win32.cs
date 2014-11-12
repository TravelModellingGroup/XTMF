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
using System.Runtime.InteropServices;
using System.Windows;

namespace XTMF.Gui
{
    internal static class NativeMethods
    {
        //Flash both the window caption and taskbar button.
        //This is equivalent to setting the FLASHW_CAPTION | FLASHW_TRAY flags.
        private const UInt32 FLASHW_ALL = 3;

        //Flash the window caption.
        private const UInt32 FLASHW_CAPTION = 1;

        //Stop flashing. The system restores the window to its original state.
        private const UInt32 FLASHW_STOP = 0;

        //Flash continuously, until the FLASHW_STOP flag is set.
        private const UInt32 FLASHW_TIMER = 4;

        //Flash continuously until the window comes to the foreground.
        private const UInt32 FLASHW_TIMERNOFG = 12;

        //Flash the taskbar button.
        private const UInt32 FLASHW_TRAY = 2;

        [DllImport( "DwmApi.dll" )]
        public static extern int DwmExtendFrameIntoClientArea(
            IntPtr hwnd,
            ref MARGINS pMarInset);

        internal static void FlashWindow(Window window, int times = -1)
        {
            if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
            {
                FLASHWINFO fInfo = new FLASHWINFO();
                var ptr = new System.Windows.Interop.WindowInteropHelper( window );
                fInfo.cbSize = Convert.ToUInt32( Marshal.SizeOf( fInfo ) );
                fInfo.hwnd = ptr.Handle;
                //fInfo.dwFlags = FLASHW_TIMERNOFG;
                fInfo.dwFlags = FLASHW_TRAY | 8;
                fInfo.uCount = (uint)( times <= 0 ? 3 : times );
                fInfo.dwTimeout = 0;
                FlashWindowEx( ref fInfo );
            }
        }

        [DllImport( "user32.dll" )]
        [return: MarshalAs( UnmanagedType.Bool )]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout( LayoutKind.Sequential )]
        public struct MARGINS
        {
            public int cxLeftWidth;      // width of left border that retains its size
            public int cxRightWidth;     // width of right border that retains its size
            public int cyTopHeight;      // height of top border that retains its size
            public int cyBottomHeight;   // height of bottom border that retains its size
        };

        [StructLayout( LayoutKind.Sequential )]
        private struct FLASHWINFO
        {
            public UInt32 cbSize;
            public IntPtr hwnd;
            public UInt32 dwFlags;
            public UInt32 uCount;
            public UInt32 dwTimeout;
        }
    }
}