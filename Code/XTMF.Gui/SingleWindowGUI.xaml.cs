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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using XTMF.Gui.Pages;

namespace XTMF.Gui
{
    public enum XTMFPage
    {
        // home
        StartPage,

        // Importing data
        ImportPage,

        //Settings
        SettingsPage,

        // Running/Using Projects
        ProjectSelectPage,

        NewProjectPage,
        DeleteProjectPage,
        ProjectSettingsPage,
        ModelSystemSettingsPage,
        RunModelSystemPage,
        SelectModelSystemPage,
        SaveProjectPage,
        RemoveModelSystem,

        // Viewing Past Runs
        ViewProjectRunsPage,

        ViewProjectRunPage,
        DeleteRunPage,

        // Editing Model Systems
        EditModelSystem,

        DeleteModelSystemPage,

        //Utilities
        QuestionPage,

        BooleanQuestionPage,
        FileNamePage,

        // About TMG
        AboutPage,

        // The number of pages / default value
        NumberOfPages
    }

    /// <summary>
    /// Interaction logic for SingleWindowGUI.xaml
    /// </summary>
    public partial class SingleWindowGUI : Window
    {
        public bool CheckToSave = false;
        public XTMFPage[] CurrentPath;
        public IProject CurrentProject;
        public BindingList<Type> Modules = new BindingList<Type>();
        public string[] PageNames;
        public IBindingList Projects;
        public bool Reload = false;
        public XTMFRuntime XTMF;
        private XTMFPage CurrentPage = XTMFPage.NumberOfPages;
        private DoubleAnimation fadeInAnimation = new DoubleAnimation( 0, 1, new Duration( new TimeSpan( 0, 0, 0, 0, 250 ) ) );
        private bool FirstLoad = true;
        private object InterruptedNavigationData = null;
        private XTMFPage InterruptedNavigationTo = XTMFPage.NumberOfPages;
        private IXTMFPage[] Pages;

        public SingleWindowGUI()
        {
            InitializeComponent();
            this.CurrentMode.Visibility = System.Windows.Visibility.Hidden;
            this.LoadingPane.Visibility = System.Windows.Visibility.Visible;
        }

        public void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke( DispatcherPriority.Background,
               new DispatcherOperationCallback( ExitFrame ), frame );
            Dispatcher.PushFrame( frame );
        }

        public object ExitFrame(object f)
        {
            ( (DispatcherFrame)f ).Continue = false;
            return null;
        }

        public void Navigate(XTMFPage gotoPage, object extraData = null)
        {
            try
            {
                do
                {
                    this.Reload = false;
                    if ( this.CheckToSave )
                    {
                        this.InterruptedNavigationTo = gotoPage;
                        this.InterruptedNavigationData = extraData;
                        gotoPage = XTMFPage.SaveProjectPage;
                        this.CheckToSave = false;
                    }
                    if ( this.CurrentPage == XTMFPage.SaveProjectPage )
                    {
                        gotoPage = this.CurrentPage = this.InterruptedNavigationTo;
                        extraData = this.InterruptedNavigationData;
                    }
                    else
                    {
                        this.CurrentPage = gotoPage;
                    }
                    var isTerminal = System.Windows.Forms.SystemInformation.TerminalServerSession;
                    if ( !isTerminal )
                    {
                        this.DoFadeInAnimation( typeof( Grid ) );
                    }
                    this.CurrentMode.Children.Clear();
                    this.CurrentMode.Visibility = System.Windows.Visibility.Hidden;
                    this.LoadingPane.Visibility = System.Windows.Visibility.Visible;
                    /*if(!isTerminal)
                    {
                        DoubleAnimation fadeIn = new DoubleAnimation( 0, 1, new Duration( TimeSpan.FromMilliseconds( 300 ) ) );
                        this.LoadingPane.BeginAnimation( Grid.OpacityProperty, fadeIn );
                    }*/
                    this.DoEvents();
                    if ( this.Pages[(int)this.CurrentPage] == null )
                    {
                        this.CreatePage( this.CurrentPage );
                    }
                    this.Pages[(int)this.CurrentPage].SetActive( extraData );
                    this.CurrentMode.Visibility = System.Windows.Visibility.Visible;
                    this.LoadingPane.Visibility = System.Windows.Visibility.Hidden;
                    extraData = null;
                } while ( this.Reload );
                this.CurrentPath = this.Pages[(int)this.CurrentPage].Path;
                this.HeaderControl.NewPathSet();
                this.CurrentMode.Children.Add( (UserControl)this.Pages[(int)this.CurrentPage] );
                this.HeaderControl.CurrentPageIsStart = ( this.CurrentPage == XTMFPage.StartPage );
                var itemToFocus = ( this.Pages[(int)this.CurrentPage] as IInputElement );
                Keyboard.Focus( itemToFocus );
            }
            catch ( Exception e )
            {
                MessageBox.Show(this, "XTMF.GUI had an internal error:\r\n" + e.Message + "\r\nStack Trace:\r\n" + e.StackTrace, "Internal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit( 1 );
            }
        }

        public void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bool glass = false;
            string value;
            if ( this.FirstLoad )
            {
                this.Background = new SolidColorBrush( Color.FromRgb( 16, 25, 39 ) );
                this.Dispatcher.BeginInvoke( new Action( delegate()
                {
                    this.XTMF = new XTMFRuntime();
                    BindToControllers();
                    LoadData();
                    Initialize();
                    App.Current.Exit += new ExitEventHandler( Current_Exit );
                    this.Navigate( XTMFPage.StartPage );
                    this.FirstLoad = false;
                    if ( this.XTMF.Configuration.AdditionalSettings.TryGetValue( "UseGlass", out value ) )
                    {
                        bool.TryParse( value, out glass );
                    }
                    if ( glass && !ApplyGlass() || !glass )
                    {
                        this.Background = new SolidColorBrush( Color.FromRgb( 16, 25, 39 ) );
                        this.XTMF.Configuration.AdditionalSettings["UseGlass"] = "false";
                    }
                } ) );
                this.FirstLoad = true;
            }
            else
            {
                if ( this.XTMF.Configuration.AdditionalSettings.TryGetValue( "UseGlass", out value ) )
                {
                    bool.TryParse( value, out glass );
                }
                if ( glass && !ApplyGlass() || !glass )
                {
                    this.Background = new SolidColorBrush( Color.FromRgb( 16, 25, 39 ) );
                    this.XTMF.Configuration.AdditionalSettings["UseGlass"] = "false";
                }
            }
        }

        internal IModelSystem CreateCopy(IModelSystemStructure structure, List<ILinkedParameter> linkedParameters, string name, string description)
        {
            string error = null;
            var newMS = CreateModelSystem( name, description, structure, linkedParameters );
            if ( newMS != null )
            {
                newMS.Save( ref error );
            }
            return newMS;
        }

        internal IModelSystem CreateModelSystem(string name, string description, IModelSystemStructure strucuture, List<ILinkedParameter> linkedParameters)
        {
            string error = null;
            IModelSystem ms;
            if ( ( ms = this.XTMF.ModelSystemController.CreateModelSystem( name, description, strucuture, linkedParameters, ref error ) ) == null )
            {
                MessageBox.Show( "Unable to create model system!\r\n" + error, "Unable to Create Modelsystem!",
                    MessageBoxButton.OK, MessageBoxImage.Error );
                return null;
            }
            return ms;
        }

        internal IProject CreateProject(string name, string description)
        {
            string error = null;
            IProject project;
            if ( ( project = this.XTMF.ProjectController.CreateProject( name, description, ref error ) ) == null )
            {
                MessageBox.Show( "Unable to create project!\r\n" + error, "Unable to save!",
                    MessageBoxButton.OK, MessageBoxImage.Error );
                return null;
            }
            this.CurrentProject = project;
            return project;
        }

        internal void Delete(IProject project)
        {
            string error = null;
            this.XTMF.ProjectController.DeleteProject( project, ref error );
        }

        internal void Delete(IModelSystem modelSystem)
        {
            string error = null;
            this.XTMF.ModelSystemController.DeleteModelSystem( modelSystem, ref error );
        }

        internal void Flash()
        {
            NativeMethods.FlashWindow( this, 3 );
        }

        internal void ImportProject(string newMSFileName, bool replace)
        {
            string error = null;
            var name = System.IO.Path.GetFileName( System.IO.Path.GetDirectoryName( newMSFileName ) );
            this.XTMF.ProjectController.ImportProject( newMSFileName, name, ref error, replace );
        }

        internal void LoadModelSystem(string fileName, bool replace = false)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension( fileName );
            string error = null;
            this.XTMF.ModelSystemController.ImportModelSystem( fileName, name, ref error, replace );
        }

        internal bool Rename(IModelSystem ms, string newName)
        {
            string error = null;
            return this.XTMF.ModelSystemController.Rename( ms, newName, ref error );
        }

        internal string UniqueModelSystemName(string name)
        {
            var modelSystems = this.XTMF.ModelSystemController.ModelSystems;
            foreach ( var obj in modelSystems )
            {
                var ms = (IModelSystem)obj;
                if ( ms.Name.Equals( name, StringComparison.InvariantCultureIgnoreCase ) )
                {
                    return name + " has already been taken.";
                }
            }
            return null;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing( e );
            if ( !e.Cancel )
            {
                this.Visibility = System.Windows.Visibility.Hidden;
                this.DoEvents();
                Application.Current.Shutdown();
                Task.Factory.StartNew( () =>
                    {
                        var host = this.XTMF.ActiveHost;
                        if ( host != null )
                        {
                            host.Shutdown();
                        }
                        Environment.Exit( 0 );
                    } );
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Don't try to do anything if it has already been handled.
            if ( e.Handled == false )
            {
                // If the user wants to go back, let them if we are not at home
                if ( e.Key == Key.Escape )
                {
                    var path = this.CurrentPath;
                    if ( path != null )
                    {
                        var length = path.Length;
                        if ( length > 1 )
                        {
                            // Go back to the previous page (0 indexed so length - 2)
                            this.Navigate( this.CurrentPath[length - 2] );
                        }
                    }
                }
            }
            base.OnKeyDown( e );
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged( sizeInfo );
            bool glass = false;
            string value;
            if ( this.XTMF != null )
            {
                if ( this.XTMF.Configuration.AdditionalSettings.TryGetValue( "UseGlass", out value ) )
                {
                    bool.TryParse( value, out glass );
                }
                if ( glass )
                {
                    ApplyGlass();
                }
            }
        }

        private bool ApplyGlass()
        {
            try
            {
                this.Background = new SolidColorBrush( Color.FromArgb( 0, 0, 0, 0 ) );
                // Obtain the window handle for WPF application
                IntPtr mainWindowPtr = new WindowInteropHelper( this ).Handle;
                HwndSource mainWindowSrc = HwndSource.FromHwnd( mainWindowPtr );
                mainWindowSrc.CompositionTarget.BackgroundColor = Color.FromArgb( 50, 16, 25, 39 );

                // Get System Dpi
                System.Drawing.Graphics desktop = System.Drawing.Graphics.FromHwnd( mainWindowPtr );
                float DesktopDpiX = desktop.DpiX;
                float DesktopDpiY = desktop.DpiY;

                // Set Margins
                NativeMethods.MARGINS margins = new NativeMethods.MARGINS();

                // Extend glass frame into client area
                // Note that the default desktop Dpi is 96dpi. The  margins are
                // adjusted for the system Dpi.
                margins.cxLeftWidth = Convert.ToInt32( 5 * ( DesktopDpiX / 96 ) );
                margins.cxRightWidth = Convert.ToInt32( 5 * ( DesktopDpiX / 96 ) );
                margins.cyTopHeight = Convert.ToInt32( (int)this.ActualHeight );
                margins.cyBottomHeight = Convert.ToInt32( 5 * ( DesktopDpiX / 96 ) );

                int hr = NativeMethods.DwmExtendFrameIntoClientArea( mainWindowSrc.Handle, ref margins );
                //
                if ( hr < 0 )
                {
                    //DwmExtendFrameIntoClientArea Failed
                    return false;
                }
                return true;
            }
            // If not Vista, paint background white.
            catch ( DllNotFoundException )
            {
                Application.Current.MainWindow.Background = Brushes.White;
            }
            return false;
        }

        private void BindToControllers()
        {
            this.Projects = this.XTMF.ProjectController.Projects;
        }

        private void Current_Exit(object sender, ExitEventArgs e)
        {
            var host = this.XTMF.ActiveHost;
            if ( host != null )
            {
                host.Shutdown();
            }
            this.Visibility = System.Windows.Visibility.Hidden;
        }

        private void DoFadeInAnimation(Type objectType)
        {
            this.CurrentMode.BeginAnimation( Grid.OpacityProperty, fadeInAnimation );
        }

        private void Initialize()
        {
            Directory.SetCurrentDirectory( System.AppDomain.CurrentDomain.BaseDirectory );

            InitializePages();
            this.HeaderControl.MainWindow = this;
            this.HeaderControl.CurrentPageIsStart = true;
        }

        private void CreatePage(XTMFPage page)
        {
            switch ( page )
            {
                case XTMFPage.StartPage:
                    this.Pages[(int)XTMFPage.StartPage] = new StartingPage( this );
                    break;
                case XTMFPage.SettingsPage:
                    this.Pages[(int)XTMFPage.SettingsPage] = new SettingsPage( this );
                    break;
                case XTMFPage.ImportPage:
                    this.Pages[(int)XTMFPage.ImportPage] = new ImportTypeSelectPage( this );
                    break;
                case XTMFPage.ProjectSelectPage:
                    this.Pages[(int)XTMFPage.ProjectSelectPage] = new ProjectPage( this );
                    break;
                case XTMFPage.NewProjectPage:
                    this.Pages[(int)XTMFPage.NewProjectPage] = new NewProjectPage( this );
                    break;
                case XTMFPage.DeleteProjectPage:
                    this.Pages[(int)XTMFPage.DeleteProjectPage] = new DeleteProjectPage( this );
                    break;
                case XTMFPage.SaveProjectPage:
                    this.Pages[(int)XTMFPage.SaveProjectPage] = new SaveProjectPage( this );
                    break;
                case XTMFPage.ProjectSettingsPage:
                    this.Pages[(int)XTMFPage.ProjectSettingsPage] = new ProjectSettingsPage( this );
                    break;
                case XTMFPage.SelectModelSystemPage:
                    this.Pages[(int)XTMFPage.SelectModelSystemPage] = new SelectModelSystemPage( this );
                    break;
                case XTMFPage.ModelSystemSettingsPage:
                    this.Pages[(int)XTMFPage.ModelSystemSettingsPage] = new ModelSystemPage( this );
                    break;
                case XTMFPage.RemoveModelSystem:
                    this.Pages[(int)XTMFPage.RemoveModelSystem] = new RemoveModelSystemPage( this );
                    break;
                case XTMFPage.RunModelSystemPage:
                    this.Pages[(int)XTMFPage.RunModelSystemPage] = new RunModelSystemPage( this );
                    break;
                case XTMFPage.ViewProjectRunsPage:
                    this.Pages[(int)XTMFPage.ViewProjectRunsPage] = new ViewRunsPage( this );
                    break;
                case XTMFPage.ViewProjectRunPage:
                    this.Pages[(int)XTMFPage.ViewProjectRunPage] = new ViewRunPage( this );
                    break;
                case XTMFPage.DeleteRunPage:
                    this.Pages[(int)XTMFPage.DeleteRunPage] = new DeleteRunPage( this );
                    break;
                case XTMFPage.EditModelSystem:
                    this.Pages[(int)XTMFPage.EditModelSystem] = new EditModelSystemPage( this );
                    break;
                case XTMFPage.DeleteModelSystemPage:
                    this.Pages[(int)XTMFPage.DeleteModelSystemPage] = new DeleteModelSystemPage( this );
                    break;
                case XTMFPage.QuestionPage:
                    this.Pages[(int)XTMFPage.QuestionPage] = new QuestionPage( this );
                    break;
                case XTMFPage.BooleanQuestionPage:
                    this.Pages[(int)XTMFPage.BooleanQuestionPage] = new BooleanQuestionPage( this );
                    break;
                case XTMFPage.FileNamePage:
                    this.Pages[(int)XTMFPage.FileNamePage] = new CopyFileConflictPage( this );
                    break;
                case XTMFPage.AboutPage:
                    this.Pages[(int)XTMFPage.AboutPage] = new AboutPage( this );
                    break;
            }
        }

        private void InitializePages()
        {
            this.Pages = new IXTMFPage[(int)XTMFPage.NumberOfPages];
            this.PageNames = new string[(int)XTMFPage.NumberOfPages];
            this.PageNames[(int)XTMFPage.StartPage] = "Home";
            // Settings
            this.PageNames[(int)XTMFPage.SettingsPage] = "XTMF Settings";
            // Importing
            this.PageNames[(int)XTMFPage.ImportPage] = "Import";
            // Project Pages
            this.PageNames[(int)XTMFPage.ProjectSelectPage] = "Select Project";
            this.PageNames[(int)XTMFPage.NewProjectPage] = "New";
            this.PageNames[(int)XTMFPage.DeleteProjectPage] = "Delete";
            this.PageNames[(int)XTMFPage.SaveProjectPage] = "Save";
            this.PageNames[(int)XTMFPage.ProjectSettingsPage] = "Project";
            this.PageNames[(int)XTMFPage.SelectModelSystemPage] = "Select Model System";
            this.PageNames[(int)XTMFPage.ModelSystemSettingsPage] = "Model System Settings";
            this.PageNames[(int)XTMFPage.RemoveModelSystem] = "Remove";
            this.PageNames[(int)XTMFPage.RunModelSystemPage] = "Model System Run";
            // Viewing Past Runs
            this.PageNames[(int)XTMFPage.ViewProjectRunsPage] = "Past Runs";
            this.PageNames[(int)XTMFPage.ViewProjectRunPage] = "Run";
            this.PageNames[(int)XTMFPage.DeleteRunPage] = "Delete";
            // Editing Model Systems
            this.PageNames[(int)XTMFPage.EditModelSystem] = "Edit Model System";
            this.PageNames[(int)XTMFPage.DeleteModelSystemPage] = "Delete";
            // Utility Pages
            this.PageNames[(int)XTMFPage.QuestionPage] = "Question";
            this.PageNames[(int)XTMFPage.FileNamePage] = "Rename File";
            this.PageNames[(int)XTMFPage.AboutPage] = "About";
        }

        private void LoadData()
        {
            foreach ( var module in this.XTMF.Configuration.ModelRepository )
            {
                this.Modules.Add( module );
            }
        }
    }
}