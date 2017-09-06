/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Run
{
    sealed class XTMFRunRemoteClient : XTMFRun
    {
        private IModelSystemTemplate MST;

        public XTMFRunRemoteClient(Configuration configuration, string runName, string runDirectory, string modelSystemString)
            : base(runName, runDirectory, configuration)
        {
            using (var memStream = new MemoryStream())
            {
                BinaryWriter wr = new BinaryWriter(memStream);
                wr.Write(modelSystemString);
                wr.Seek(0, SeekOrigin.Begin);
                var mss = ModelSystemStructure.Load(memStream, configuration);
                MST = mss.Module as IModelSystemTemplate;
            }
            DirectoryInfo runDir = new DirectoryInfo(runDirectory);
            if (runDir.Exists)
            {
                runDir.Delete(true);
            }
            runDir.Create();
            Environment.CurrentDirectory = runDirectory;
        }

        public override bool RunsRemotely => true;

        public override bool DeepExitRequest()
        {
            throw new NotImplementedException();
        }

        public override bool ExitRequest()
        {
            throw new NotImplementedException();
        }

        public override Tuple<byte, byte, byte> PollColour()
        {
            throw new NotImplementedException();
        }

        public override float PollProgress()
        {
            throw new NotImplementedException();
        }

        public override string PollStatusMessage()
        {
            throw new NotImplementedException();
        }

        public override void Start()
        {
            throw new NotImplementedException();
        }

        public override void TerminateRun()
        {
            throw new NotImplementedException();
        }

        public override void Wait()
        {
            throw new NotImplementedException();
        }
    }
}
