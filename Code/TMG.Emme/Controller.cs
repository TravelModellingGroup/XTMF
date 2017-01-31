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

        ///  <summary>
        ///  </summary>
        ///  <param name="projectFolder"></param>
        /// <param name="newWindow"></param>
        public Controller(string projectFolder, bool newWindow = false)
        {
            FailTimer = 30;
            ProjectFile = projectFolder;
            LaunchInNewWindow = newWindow;
            string args = "-ng ";
            string workingDirectory = ProjectFile;
            Emme = new Process();
            Emme.StartInfo.FileName = "emme";
            Emme.StartInfo.Arguments = args;
            Emme.StartInfo.CreateNoWindow = true;
            Emme.StartInfo.UseShellExecute = false;
            Emme.StartInfo.RedirectStandardInput = true;
            //Emme.StartInfo.RedirectStandardOutput = true;
            if (!LaunchInNewWindow)
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
            ToEmme = Emme.StandardInput;
            ToEmme.WriteLine("TMG");
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
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool all)
        {
            lock (this)
            {
                do
                {
                    if (ToEmme != null)
                    {
                        try
                        {
                            ToEmme.WriteLine();
                            ToEmme.WriteLine();
                            ToEmme.WriteLine();
                            ToEmme.WriteLine();
                            ToEmme.WriteLine("q");
                            ToEmme.WriteLine("q");
                            ToEmme.WriteLine("q");
                            ToEmme.WriteLine("q");
                        }
                        catch (IOException)
                        { }
                    }
                    if (!Emme.HasExited)
                    {
                        Thread.Sleep(100);
                    }
                } while (!Emme.HasExited);
                try
                {
                    if (ToEmme != null)
                    {
                        ToEmme.Close();
                        ToEmme = null;
                    }
                }
                catch (IOException)
                {
                }
                try
                {
                    Emme?.Kill();
                }
                catch (IOException)
                { }
                Emme?.Dispose();
                Emme = null;
            }
        }

        /// <summary>
        /// Process floats to work with emme
        /// </summary>
        /// <param name="number">The float you want to send</param>
        /// <returns>A limited precision non scientific number in a string</returns>
        public string ToEmmeFloat(float number)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append((int)number);
            number = (float)Math.Round(number, 6);
            number = number - (int)number;
            if (number > 0)
            {
                var integerSize = builder.Length;
                builder.Append('.');
                for (int i = integerSize; i < 4; i++)
                {
                    number = number * 10;
                    builder.Append((int)number);
                    number = number - (int)number;
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if (number == 0)
                    {
                        break;
                    }
                }
            }
            return builder.ToString();
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
            if (number > 0)
            {
                var integerSize = builder.Length;
                builder.Append('.');
                for (int i = integerSize; i < 4; i++)
                {
                    number = number * 10;
                    builder.Append((int)number);
                    number = number - (int)number;
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if (number == 0)
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
            if (ToEmme == null || Emme == null) return false;
            var externFile = Path.GetFullPath(Path.Combine(ProjectFile, "ExternalCommand.lock"));
            lock (this)
            {
                if (!File.Exists(externFile))
                {
                    File.Create(externFile).Close();
                }
                StringBuilder builder = new StringBuilder();
                builder.Append("~<" + macroName);
                if (!string.IsNullOrEmpty(arguments))
                {
                    builder.Append(' ');
                    builder.Append(arguments);
                }
                var timeToFail = FailTimer;
                var startTime = DateTime.Now;
                ToEmme.WriteLine(builder.ToString());
                while (File.Exists(externFile))
                {
                    if (Emme == null || Emme.HasExited)
                    {
                        return false;
                    }
                    var currentTime = DateTime.Now;
                    if ((currentTime - startTime) > TimeSpan.FromMinutes(timeToFail))
                    {
                        return false;
                    }
                    Thread.Sleep(100);
                }
            }
            return true;
        }
    }
}