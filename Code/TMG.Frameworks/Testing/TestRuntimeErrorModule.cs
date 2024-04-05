/*
    Copyright 2015-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Frameworks.Testing;

[ModuleInformation(Description =
    @"A test module that will generate a runtime exception at the start of this module's execution. A divide by 0 error is generated in the module's Start() method.",
    IconURI = "TestTube")]
public class TestRuntimeErrorModule : ISelfContainedModule
{
    private float _progress = 0;
    public string Name { get; set; }
    public float Progress { get => _progress; }
    public Tuple<byte, byte, byte> ProgressColour { get; }

    [RunParameter("XTMF Runtime exception", false, "Should the runtime exception be an XTMF Runtime exception?")]
    public bool ThrowXTMFRuntimeException;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool RuntimeValidation(ref string error)
    {
        //nothing to validate
        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    public void Start()
    {
        //throw new dummy exception
        if (ThrowXTMFRuntimeException)
        {
            throw new XTMFRuntimeException(this, "The requested runtime exception has been generated!");
        }
        else
        {
            var p = 0;
            var s = 10 / p;
        }
    }
}

public class GenericRuntimeError : Exception
{
    /// <summary>
    /// Creates a generic runtime error with message s.
    /// </summary>
    /// <param name="s"></param>
    public GenericRuntimeError(string s) : base(s)
    {

    }
}
