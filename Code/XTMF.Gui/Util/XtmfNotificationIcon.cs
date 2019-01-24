using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using XTMF.Gui.Properties;
using Application = System.Windows.Forms.Application;

namespace XTMF.Gui.Util
{

    public class XtmfNotificationIcon
    {
        private static NotifyIcon _icon;

        private static Action _clickCallbackAction;

        /// <summary>
        /// 
        /// </summary>
        public static void InitializeNotificationIcon()
        {

            try
            {
                _icon = new NotifyIcon
                {
                    Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
                    Visible = true
                };
                _icon.BalloonTipClicked += _icon_BalloonTipClicked;
            }
            catch
            {
                Console.WriteLine("Tray icon not loaded for network share.");
            }


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void _icon_BalloonTipClicked(object sender, EventArgs e)
        {
            _clickCallbackAction?.Invoke();
            MainWindow.Us.WindowState = WindowState.Normal;
            SystemCommands.RestoreWindow(MainWindow.Us);
            _clickCallbackAction = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="callBack"></param>
        /// <param name="title"></param>
        public static void ShowNotificationBalloon(string message, Action callBack, string title = "XTMF")
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = message;
            _icon.ShowBalloonTip(5000);
            _clickCallbackAction = callBack;

        }

        /// <summary>
        /// 
        /// </summary>
        public static void ClearNotificationIcon()
        {
            _icon.Visible = false;
        }
    }

}