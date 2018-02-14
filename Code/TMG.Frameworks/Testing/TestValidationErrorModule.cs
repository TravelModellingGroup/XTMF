﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;

namespace TMG.Frameworks.Testing
{
    public class TestValidationErrorModule : ISelfContainedModule
    {
        public string Name { get; set; } = "TestValidationErrorModule";
        public float Progress { get; }
        public Tuple<byte, byte, byte> ProgressColour { get; }

        public bool RuntimeValidation(ref string error)
        {
            error =  "Generic Validation Error";
            return false;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }
    }

}
