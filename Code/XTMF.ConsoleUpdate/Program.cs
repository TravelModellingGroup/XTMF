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
using System.Threading.Tasks;

namespace XTMF.ConsoleUpdate
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine( "XTMF.ConsoleUpdate Version 1.0.1" );
            var controller = new UpdateController();
            controller.XTMFUpdateServerLocation = args[0];
            controller.UseWebservices = false;
            try
            {
                controller.UpdateAll( false, false, (progress) => Console.Write( "\r{0:P}%\t", progress ),
                ( s =>
                    {
                        Console.WriteLine();
                        Console.WriteLine( s );
                    } ) );
            }
            catch ( AggregateException error )
            {
                Console.WriteLine();
                Console.WriteLine( "XTMF Update Error:" );
                Console.WriteLine( error.InnerException.Message );
            }
            catch ( Exception error )
            {
                Console.WriteLine();
                Console.WriteLine( "XTMF Update Error:" );
                Console.WriteLine( error.Message + "\r\n" + error.StackTrace );
            }
        }
    }
}
