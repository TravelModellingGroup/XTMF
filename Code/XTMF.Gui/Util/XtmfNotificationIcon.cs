using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Forms.Application;

namespace XTMF.Gui.Util;


public class XtmfNotificationIcon
{
    private static NotifyIcon _icon;

    private static Action _clickCallbackAction;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="hInst"></param>
    /// <param name="lpIconPath"></param>
    /// <param name="lpiIcon"></param>
    /// <returns></returns>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, StringBuilder lpIconPath, out ushort lpiIcon);

    /// <summary>
    /// Initializes the notification icon in the system tray area.
    /// </summary>
    public static void InitializeNotificationIcon()
    {
        try
        {
            StringBuilder strB = new(260);
            strB.Append(Application.ExecutablePath);
            IntPtr handle = ExtractAssociatedIcon(IntPtr.Zero, strB, out ushort uicon);
            Icon ico = Icon.FromHandle(handle);
            _icon = new NotifyIcon
            {
                Icon = ico,
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