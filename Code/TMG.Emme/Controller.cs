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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using XTMF;

namespace TMG.Emme
{
    /// <summary>
    ///
    /// </summary>
    public class Controller : IDisposable
    {
        protected Process Emme;

        protected StreamReader FromEmme;

        protected StreamWriter ToEmme;

        /// <summary>
        ///
        /// </summary>
        /// <param name="projectFolder"></param>
        public Controller(string projectFolder, bool newWindow = false)
        {
            this.FailTimer = 30;
            this.ProjectFile = projectFolder;
            this.LaunchInNewWindow = newWindow;
            string args = "-ng ";
            string workingDirectory = this.ProjectFile;
            Emme = new Process();
            Emme.StartInfo.FileName = "emme";
            Emme.StartInfo.Arguments = args;
            Emme.StartInfo.CreateNoWindow = true;
            Emme.StartInfo.UseShellExecute = false;
            Emme.StartInfo.RedirectStandardInput = true;
            //Emme.StartInfo.RedirectStandardOutput = true;
            if(!LaunchInNewWindow)
            {
                Emme.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            Emme.StartInfo.WorkingDirectory = workingDirectory;
            try
            {
                Emme.Start();
            }
            catch
            {
                throw new XTMFRuntimeException("Unable to create a link to EMME!  Please make sure that it is on the system PATH!");
            }
            this.ToEmme = this.Emme.StandardInput;
            this.ToEmme.WriteLine("TMG");
            //this.EmmeOut = this.Emme.StandardOutput;
        }

        protected Controller()
        {
        }

        public double FailTimer { get; set; }

        public bool LaunchInNewWindow { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string ProjectFile { get; protected set; }

        public virtual void Close()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool all)
        {
            lock (this)
            {
                do
                {
                    if(this.ToEmme != null)
                    {
                        try
                        {
                            this.ToEmme.WriteLine();
                            this.ToEmme.WriteLine();
                            this.ToEmme.WriteLine();
                            this.ToEmme.WriteLine();
                            this.ToEmme.WriteLine("q");
                            this.ToEmme.WriteLine("q");
                            this.ToEmme.WriteLine("q");
                            this.ToEmme.WriteLine("q");
                        }
                        catch
                        { }
                    }
                    if(!this.Emme.HasExited)
                    {
                        Thread.Sleep(100);
                    }
                } while(!this.Emme.HasExited);
                try
                {
                    if(this.ToEmme != null)
                    {
                        this.ToEmme.Close();
                        this.ToEmme = null;
                    }
                }
                catch
                {
                }
                if(this.Emme != null)
                {
                    try
                    {
                        this.Emme.Kill();
                    }
                    catch
                    { }
                    this.Emme.Dispose();
                    this.Emme = null;
                }
            }
        }

        /// <summary>
        /// Process floats to work with emme
        /// </summary>
        /// <param name="number">The float you want to send</param>
        /// <returns>A limited precision non scientific number in a string</returns>
        public string ToEmmeFloat(float number)
        {
            /*StringBuilder builder = new StringBuilder();
            builder.Append((int)number);
            number = number - (int)number;
            if(number > 0)
            {
                var integerSize = builder.Length;
                builder.Append('.');
                for(int i = integerSize; i < 4; i++)
                {
                    number = number * 10;
                    builder.Append((int)number);
                    number = number - (int)number;
                    if(number == 0)
                    {
                        break;
                    }
                }
            }
            return builder.ToString();*/
            return Math.Round(number, 6).ToString();
        }

        /// <summary>
        /// Process floats to work with emme
        /// </summary>
        /// <param name="number">The float you want to send</param>
        /// <param name="builder">A string build to use to make the string</param>
        /// <returns>A limited precision non scientific number in a string</returns>
        public void ToEmmeFloat(float number, StringBuilder builder)
        {
            builder.Clear();
            builder.Append((int)number);
            number = number - (int)number;
            if(number > 0)
            {
                var integerSize = builder.Length;
                builder.Append('.');
                for(int i = integerSize; i < 4; i++)
                {
                    number = number * 10;
                    builder.Append((int)number);
                    number = number - (int)number;
                    if(number == 0)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Runs the Macro with the given arugments.
        /// Blocks until Emme exits
        /// </summary>
        /// <param name="macroName">The name of the macro that you want to run</param>
        /// <param name="arguments">The arguments to the macro that you want to run</param>
        public virtual bool Run(string macroName, string arguments)
        {
            if(this.ToEmme == null || this.Emme == null) return false;
            var externFile = Path.GetFullPath(Path.Combine(this.ProjectFile, "ExternalCommand.lock"));
            lock (this)
            {
                if(!File.Exists(externFile))
                {
                    var stream = File.Create(externFile);
                    if(stream != null)
                    {
                        stream.Close();
                    }
                }
                StringBuilder builder = new StringBuilder();
                builder.Append("~<" + macroName);
                if(arguments != null && arguments != String.Empty)
                {
                    builder.Append(' ');
                    builder.Append(arguments);
                }
                var timeToFail = this.FailTimer;
                var startTime = DateTime.Now;
                this.ToEmme.WriteLine(builder.ToString());
                while(File.Exists(externFile))
                {
                    if(this.Emme == null || this.Emme.HasExited)
                    {
                        return false;
                    }
                    var currentTime = DateTime.Now;
                    if((currentTime - startTime) > TimeSpan.FromMinutes(timeToFail))
                    {
                        return false;
                    }
                    System.Threading.Thread.Sleep(100);
                }
            }
            return true;
        }
    }
}