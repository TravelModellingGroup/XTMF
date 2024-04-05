﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MahApps.Metro.Controls;

namespace XTMF.Gui.UserControls;

/// <summary>
/// Interaction logic for TabHostWindow.xaml
/// </summary>
public partial class TabHostWindow : MetroWindow
{
    public TabHostWindow()
    {
        InitializeComponent();

        TabablzControl.InterTabController.InterTabClient = new InterTabClient();
        
    }
}
