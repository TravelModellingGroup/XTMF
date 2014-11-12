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
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for RunModelSystemPage.xaml
    /// </summary>
    public partial class RunModelSystemPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.RunModelSystemPage };
        private XTMFRun CurrentRun = null;
        private Tuple<byte, byte, byte> ErrorColour = new Tuple<byte, byte, byte>( 255, 0, 0 );
        private bool IsActive = false;
        private BindingListWithRemoving<IProgressReport> ProgressReports;
        private string ProjectDirectory;
        private string RunDirectory;
        private bool RunFinished = false;
        private bool WasCanceled = false;
        private string RunName;
        private DateTime StartTime;
        private BindingListWithRemoving<SubProgress> SubProgressBars = new BindingListWithRemoving<SubProgress>();

        /// <summary>
        /// Requires Windows 7
        /// </summary>
        private TaskbarItemInfo TaskbarInformation;

        private DispatcherTimer Timer;
        private bool Windows7OrAbove = false;
        private SingleWindowGUI XTMF;

        public RunModelSystemPage(SingleWindowGUI xtmf)
        {
            this.XTMF = xtmf;
            this.ProgressReports = this.XTMF.XTMF.Configuration.ProgressReports;
            InitializeComponent();
            this.ProgressReports.ListChanged += new ListChangedEventHandler( ProgressReports_ListChanged );
            this.ProgressReports.BeforeRemove += new EventHandler<ListChangedEventArgs>( ProgressReports_BeforeRemove );
            this.SubProgressBars.ListChanged += new ListChangedEventHandler( SubProgressBars_ListChanged );
            this.SubProgressBars.BeforeRemove += new EventHandler<ListChangedEventArgs>( SubProgressBars_BeforeRemove );
            this.Timer = new DispatcherTimer();
            this.Timer.Interval = TimeSpan.FromMilliseconds( 1000 / 30 );
            this.Timer.Tick += new EventHandler( Timer_Tick );
            this.Loaded += new RoutedEventHandler( RunModelSystemPage_Loaded );
            this.Unloaded += new RoutedEventHandler( RunModelSystemPage_Unloaded );
            this.ProjectDirectory = System.IO.Path.GetFullPath( this.XTMF.XTMF.Configuration.ProjectDirectory );
            if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
            {
                var major = Environment.OSVersion.Version.Major;
                if ( major > 6 || ( major >= 6 && Environment.OSVersion.Version.Minor >= 1 ) )
                {
                    Windows7OrAbove = true;
                    this.TaskbarInformation = this.XTMF.TaskbarItemInfo = new TaskbarItemInfo();
                }
            }
        }

        public sealed class ConsoleOutputController : INotifyPropertyChanged, IDisposable
        {
            public string ConsoleOutput { get; set; }

            public Visibility Show { get; set; }
            
            MemoryStream memoryStream = new MemoryStream();
            StreamWriter writer;
            internal volatile bool Done = false;
            public ConsoleOutputController(RunModelSystemPage page)
            {
                var previousConsole = Console.Out;
                writer = new StreamWriter( memoryStream, System.Text.Encoding.Unicode );
                page.OldCaret = 0;
                Console.SetOut( this.writer );
                new Task( () =>
                    {
                        try
                        {
                            var lastPosition = 0L;
                            if ( lastPosition == 0 )
                            {
                                this.Show = Visibility.Hidden;
                                var e = PropertyChanged;
                                if ( e != null )
                                {
                                    e( this, new PropertyChangedEventArgs( "Show" ) );
                                }
                            }
                            StreamReader reader = new StreamReader( memoryStream, System.Text.Encoding.Unicode );
                            while ( true )
                            {
                                Thread.Sleep( 60 );
                                writer.Flush();
                                var currentPosition = writer.BaseStream.Position;
                                if ( currentPosition > lastPosition )
                                {
                                    var buff = new char[( currentPosition - lastPosition ) / sizeof( char )];
                                    memoryStream.Position = lastPosition;
                                    int length = reader.ReadBlock( buff, 0, buff.Length );
                                    if ( lastPosition == 0 )
                                    {
                                        this.Show = Visibility.Visible;
                                        var e = PropertyChanged;
                                        if ( e != null )
                                        {
                                            e( this, new PropertyChangedEventArgs( "Show" ) );
                                        }
                                    }
                                    lastPosition = currentPosition;
                                    if ( length > 0 )
                                    {
                                        page.Dispatcher.Invoke( new Action( () =>
                                            {
                                                page.OldCaret = page.ConsoleOutput.CaretIndex;
                                            } ) );
                                        this.ConsoleOutput = this.ConsoleOutput + new string( buff, 0, length );
                                        var e = PropertyChanged;
                                        if ( e != null )
                                        {
                                            e( this, new PropertyChangedEventArgs( "ConsoleOutput" ) );
                                        }
                                    }
                                }
                                if ( this.Done )
                                {
                                    writer.Dispose();
                                    writer = null;
                                    return;
                                }
                            }
                        }
                        catch
                        {
                            Console.SetOut( previousConsole );
                        }
                    } ).Start();
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public void Dispose()
            {
                if ( this.writer != null )
                {
                    this.writer.Dispose();
                    this.writer = null;
                }
                if ( this.memoryStream != null )
                {
                    this.memoryStream.Dispose();
                    this.memoryStream = null;
                }
                this.ConsoleOutput = null;
            }
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
            if ( this.CurrentRun == null )
            {
                if ( data is QuestionResult )
                {
                    this.WasCanceled = false;
                    this.RunDirectory = System.IO.Path.GetTempPath();
                    this.RunFinished = false;
                    this.StartTime = DateTime.Now;
                    this.ContinueButton.IsEnabled = false;
                    this.CancelButton.IsEnabled = true;
                    this.StartTimeLabel.Content = String.Format( "Start Time: {0:g}", this.StartTime );
                    this.ProgressBar.Finished = false;
                    this.ProgressReports.Clear();
                    this.SubProgressBars.Clear();
                    this.XTMF.XTMF.Configuration.ProgressReports.Clear();
                    this.ConsoleOutput.DataContext = new ConsoleOutputController( this );
                    this.ConsoleBorder.DataContext = this.ConsoleOutput.DataContext;

                    var root = ( data as QuestionResult ).Data as IModelSystemStructure;
                    string error = null;
                    int modelSystemIndex = this.XTMF.CurrentProject.ModelSystemStructure.IndexOf( root );
                    this.RunDirectory = System.IO.Path.Combine( this.ProjectDirectory, this.XTMF.CurrentProject.Name, ( this.RunName = ( data as QuestionResult ).Result ) );
                    this.RunNameLabel.Text = this.RunName;
                    this.CurrentRun = this.XTMF.XTMF.RunController.CreateRun( this.RunName, this.XTMF.CurrentProject, modelSystemIndex, ref error );

                    HookupCurrentRun();
                    this.CurrentRun.Start();
                    if ( CurrentRun == null )
                    {
                        MessageBox.Show( error, "Unable to Create Model System!", MessageBoxButton.OK, MessageBoxImage.Error );
                        this.ProgressBar.Value = 0;
                        this.RunFinished = true;
                        SetButtonsToFinished();
                        return;
                    }
                    if ( this.Windows7OrAbove )
                    {
                        this.TaskbarInformation.ProgressState = TaskbarItemProgressState.Normal;
                        this.TaskbarInformation.ProgressValue = 0;
                    }
                    this.CreateDirectoryStructure( RunDirectory );
                }
            }
        }

        private void CancelButton_Clicked(object obj)
        {
            SetStatusToCancelling( ( this.WasCanceled = this.CurrentRun.ExitRequest() ) );
        }

        private void ContinueButton_Clicked(object obj)
        {
            this.XTMF.Navigate( XTMFPage.ProjectSettingsPage );
        }

        private void CreateDirectoryStructure(string dir)
        {
            if ( !System.IO.Directory.Exists( dir ) )
            {
                System.IO.Directory.CreateDirectory( dir );
            }
        }

        private void CurrentRun_RunComplete()
        {
            this.SetButtonsToFinished();
            this.RunFinished = true;
            this.CurrentRun = null;
        }

        private void CurrentRun_RuntimeError(Exception message)
        {
            ShowError( message, "Runtime Error:\r\n" );
        }

        private void CurrentRun_RuntimeValidationError(string message)
        {
            ShowError( message, "Runtime Validation Error:\r\n" );
        }

        private void CurrentRun_ValidationError(string message)
        {
            ShowError( message, "Validation Error:\r\n" );
        }

        private void CurrentRun_ValidationStarting()
        {
            this.SetStatusToValidation();
        }

        private void HookupCurrentRun()
        {
            this.CurrentRun.RunComplete += new Action( CurrentRun_RunComplete );
            this.CurrentRun.RuntimeError += new Action<Exception>( CurrentRun_RuntimeError );
            this.CurrentRun.RuntimeValidationError += new Action<string>( CurrentRun_RuntimeValidationError );
            this.CurrentRun.ValidationStarting += new Action( CurrentRun_ValidationStarting );
            this.CurrentRun.ValidationError += new Action<string>( CurrentRun_ValidationError );
        }

        private void OpenDirectoryButton_Clicked(object obj)
        {
            var path = System.IO.Path.Combine( this.ProjectDirectory, this.XTMF.CurrentProject.Name, this.RunName );
            if ( this.XTMF.CurrentProject != null && System.IO.Directory.Exists( path ) )
            {
                System.Diagnostics.Process.Start( path );
            }
            else
            {
                MessageBox.Show( path + " does not exist!" );
            }
        }

        private void ProgressReports_BeforeRemove(object sender, ListChangedEventArgs e)
        {
            this.SubProgressBars.RemoveAt( e.NewIndex );
        }

        private void ProgressReports_ListChanged(object sender, ListChangedEventArgs e)
        {
            if ( e.ListChangedType == ListChangedType.ItemAdded )
            {
                var toAdd = this.ProgressReports[e.NewIndex];
                this.Dispatcher.Invoke( new Action( delegate()
                {
                    this.AdditionDetailsPanelBorder.Visibility = System.Windows.Visibility.Visible;
                    this.AdditionDetailsPanelBorder.Height = double.NaN;
                    var progressBar = new TMGProgressBar()
                    {
                        Background = new SolidColorBrush( Color.FromArgb( (byte)0x22, (byte)0x22, (byte)0x22, (byte)0x22 ) ),
                        Maximum = 10000,
                        Minimum = 0,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                        Height = 15
                    };
                    if ( toAdd.Colour != null )
                    {
                        progressBar.SetForgroundColor( Color.FromRgb( toAdd.Colour.Item1, toAdd.Colour.Item2, toAdd.Colour.Item3 ) );
                    }
                    this.SubProgressBars.Add( new SubProgress()
                    {
                        Name = new Label() { Content = toAdd.Name, Foreground = Brushes.White },
                        ProgressBar = progressBar
                    } );
                } ) );
            }
            else if ( e.ListChangedType == ListChangedType.ItemDeleted )
            {
                if ( this.ProgressReports.Count == 0 )
                {
                    this.Dispatcher.Invoke( new Action( delegate()
                    {
                        this.AdditionDetailsPanelBorder.Visibility = System.Windows.Visibility.Collapsed;
                        this.AdditionDetailsPanelBorder.Height = 0;
                    } ) );
                }
            }
        }

        private void RunModelSystemPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.IsActive = true;
            this.Timer.Start();
        }

        private void RunModelSystemPage_Unloaded(object sender, RoutedEventArgs e)
        {
            this.IsActive = false;
            this.Timer.Stop();
        }

        private void SetButtonsToFinished()
        {
            this.Dispatcher.BeginInvoke( new Action(
                delegate()
                {
                    ( this.ConsoleOutput.DataContext as RunModelSystemPage.ConsoleOutputController ).Done = true;
                    this.ContinueButton.IsEnabled = true;
                    this.CancelButton.IsEnabled = false;
                    if ( this.WasCanceled )
                    {
                        this.StatusLabel.Text = "Run Canceled";
                    }
                    else
                    {
                        this.StatusLabel.Text = "Run Complete";
                    }
                    this.ProgressBar.Finished = true;
                    this.ContinueButton.FlashAnimation( 5 );
                    this.OpenDirectoryButton.FlashAnimation( 5 );
                } ), null );
        }

        private void SetStatusToCancelling(bool success)
        {
            this.Dispatcher.BeginInvoke( new Action(
                delegate()
                {
                    if ( success )
                    {
                        this.StatusLabel.Text = "Canceling Run";
                    }
                    else
                    {
                        this.StatusLabel.Text = "Unable to Cancel Run";
                    }
                } ), null );
        }

        private void SetStatusToRunning()
        {
            this.Dispatcher.BeginInvoke( new Action(
                delegate()
                {
                    this.StatusLabel.Text = "Running Model System";
                } ), null );
        }

        private void SetStatusToValidation()
        {
            this.Dispatcher.BeginInvoke( new Action(
                delegate()
                {
                    this.StatusLabel.Text = "Validating Model System";
                } ), null );
        }

        private void ShowError(string message, string header)
        {
            this.Dispatcher.Invoke( new Action(
                delegate()
                {
                    this.RunFinished = true;
                    this.CurrentRun = null;
                    this.IsActive = false;
                    ( new XTMF.Gui.UserControls.ErrorWindow() { ErrorMessage = header + message } ).ShowDialog();
                } ) );
            this.SetButtonsToFinished();
        }

        private void ShowError(Exception e, string header)
        {
            this.Dispatcher.Invoke( new Action(
                delegate()
                {
                    this.RunFinished = true;
                    this.CurrentRun = null;
                    this.IsActive = false;
                    ( new XTMF.Gui.UserControls.ErrorWindow() { Exception = e, Owner = this.XTMF } ).ShowDialog();
                } ) );
            this.SetButtonsToFinished();
        }

        private void SubProgressBars_AddingNew(object sender, AddingNewEventArgs e)
        {
        }

        private void SubProgressBars_BeforeRemove(object sender, ListChangedEventArgs e)
        {
            lock ( this )
            {
                this.Dispatcher.Invoke( new Action( delegate()
                {
                    var toRemove = this.SubProgressBars[e.NewIndex];
                    this.AdditionDetailsPanel.Remove( toRemove.Name );
                    this.AdditionDetailsPanel.Remove( toRemove.ProgressBar );
                } ) );
            }
        }

        private void SubProgressBars_ListChanged(object sender, ListChangedEventArgs e)
        {
            if ( e.ListChangedType == ListChangedType.ItemAdded )
            {
                var toAdd = this.SubProgressBars[e.NewIndex];
                this.AdditionDetailsPanel.Add( toAdd.Name );
                this.AdditionDetailsPanel.Add( toAdd.ProgressBar );
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if ( this.IsActive )
                {
                    if ( this.CurrentRun != null )
                    {
                        float progress = 1;
                        Tuple<byte, byte, byte> colour = ErrorColour;
                        try
                        {
                            this.StatusLabel.Text = this.CurrentRun.PollStatusMessage();
                            progress = this.CurrentRun.PollProgress();
                            colour = this.CurrentRun.PollColour();
                        }
                        catch { }
                        progress = progress * 10000;

                        if ( progress > 10000 ) progress = 10000;
                        if ( progress < 0 ) progress = 0;
                        if ( colour != null )
                        {
                            this.ProgressBar.SetForgroundColor( Color.FromRgb( colour.Item1, colour.Item2, colour.Item3 ) );
                        }
                        this.ProgressBar.Value = progress;
                        if ( this.Windows7OrAbove )
                        {
                            this.TaskbarInformation.ProgressValue = ( ( progress / 10000 ) );
                        }
                        int subProgressBarLength = this.SubProgressBars.Count;
                        for ( int i = 0; i < subProgressBarLength; i++ )
                        {
                            try
                            {
                                progress = this.ProgressReports[i].GetProgress();
                                progress = progress * 10000;
                                if ( progress > 10000 ) progress = 10000;
                                if ( progress < 0 ) progress = 0;
                                this.SubProgressBars[i].ProgressBar.Value = progress;
                            }
                            catch
                            {
                            }
                        }
                        if ( !( this.RunFinished ) )
                        {
                            var elapsedTime = ( DateTime.Now - this.StartTime );
                            int days = elapsedTime.Days;
                            elapsedTime = new TimeSpan( elapsedTime.Hours, elapsedTime.Minutes, elapsedTime.Seconds );
                            if ( days < 1 )
                            {
                                this.ElapsedTimeLabel.Content = String.Format( "Elapsed Time: {0:g}", elapsedTime );
                            }
                            else
                            {
                                this.ElapsedTimeLabel.Content = String.Format( "Elapsed Time: {1} Day(s), {0:g}", elapsedTime, days );
                            }
                        }
                    }
                    else
                    {
                        this.ProgressBar.Value = this.RunFinished ? 10000 : 0;
                    }
                }
            }
            catch
            {
            }
        }

        private struct SubProgress
        {
            internal Label Name;
            internal TMGProgressBar ProgressBar;
        }

        int ConsoleLength = 0;
        int OldCaret = 0;
        private void ConsoleOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newTextLength = this.ConsoleOutput.Text.Length;
            if ( this.OldCaret >= this.ConsoleLength )
            {
                this.ConsoleScrollViewer.ScrollToEnd();
                this.ConsoleOutput.CaretIndex = newTextLength;
            }
            else
            {
                this.ConsoleOutput.CaretIndex = this.OldCaret;
            }
            this.ConsoleLength = newTextLength;
        }
    }
}