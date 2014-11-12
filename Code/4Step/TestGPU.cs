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
using TMG.DirectCompute;
using XTMF;
using TMG;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Diagnostics;
namespace James.UTDM
{
    public sealed class TestGPU : IModelSystemTemplate, IDisposable
    {
        private GPU Gpu;
        const int DATA_SIZE = 640000;
        public string InputBaseDirectory
        {
            get;
            set;
        }

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        public bool ExitRequest()
        {
            return false;
        }

        public void Start()
        {
            this.Gpu = new GPU();
            if ( this.Gpu == null )
            {
                throw new XTMFRuntimeException( "We were unable to initialize the connection to the GPU!" );
            }
            TestWriting();
            RunAddTest();
        }

        private void TestWriting()
        {
            GPUBuffer testBuffer = null;
            try
            {
                float[] data = new float[1024];
                testBuffer = Gpu.CreateBuffer( data.Length, sizeof( float ), true );
                // initialize the buffer to 0's
                this.Gpu.Write( testBuffer, data );
                float[] op = new float[2];
                for ( int i = 0; i < data.Length; i += 2 )
                {
                    op[0] = i;
                    op[1] = i * 2;
                    this.Gpu.Write( testBuffer, op, 0, i, op.Length );
                }
                // read the results back in
                this.Gpu.Read( testBuffer, data );
                this.Gpu.Wait();
            }
            finally
            {
                if ( testBuffer != null )
                {
                    Gpu.ReleaseBuffer( testBuffer );
                    testBuffer = null;
                }
            }
        }

        private void RunAddTest()
        {
            int dataLength = 1024;
            for ( int i = 0; i < 15; i++ )
            {
                RunGPU( dataLength );
                RunCPU( dataLength );
                dataLength *= 2;
                this.Progress = i / 15f;
            }
        }

        private void RunCPU(int dataLength)
        {
            float[] origin = new float[dataLength];
            float[] destination = new float[dataLength / 4];
            for ( int i = 0; i < origin.Length; i++ )
            {
                origin[i] = i;
            }
            var watch = new Stopwatch();
            watch.Start();
            var destinationLoops = destination.Length;
            for ( int j = (int)( Math.Log( origin.Length, 4 ) ); j > 0; j-- )
            {
                for ( int i = 0; i < destinationLoops; i++ )
                {
                    var temp = origin[i * 4 + 0];
                    temp += origin[i * 4 + 1];
                    temp += origin[i * 4 + 2];
                    temp += origin[i * 4 + 3];
                    destination[i] = temp;
                }
                var swap = origin;
                origin = destination;
                destination = swap;
                destinationLoops /= 4;
            }
            watch.Stop();
            using ( var writer = new StreamWriter( "cpu.txt", true ) )
            {
                writer.WriteLine( "{0}", watch.ElapsedTicks / 10000f );
            }
        }

        private void RunGPU(int dataLength)
        {
            float[] data = new float[dataLength];
            for ( int i = 0; i < data.Length; i++ )
            {
                data[i] = 2;
            }
            ComputeShader shader = null;
            int reductionPerCall = 64;
            var codeBase = Assembly.GetEntryAssembly().CodeBase.Replace( "file:///", "" );
            var compile = Task.Factory.StartNew( () =>
            {
                var fileName = Path.Combine( Path.GetDirectoryName( codeBase ), "Modules", "James.UTDM.TestGPU.hlsl" );
                shader = Gpu.CompileComputeShader( fileName, "CSMain" );
                if ( shader == null )
                {
                    throw new XTMFRuntimeException( "We were unable to compile the compute shader!" );
                }
                shader.NumberOfXThreads = data.Length;
                shader.NumberOfYThreads = 1;
                shader.ThreadGroupSizeX = 64;
                shader.ThreadGroupSizeY = 1;
            } );
            var constBuffer = Gpu.CreateConstantBuffer( 16 );
            var originalBuffer = Gpu.CreateBuffer( data.Length, sizeof( float ), true );
            var destinationBuffer = Gpu.CreateBuffer( ( data.Length / reductionPerCall ) + 1, sizeof( float ), true );
            try
            {
                this.Gpu.Write( originalBuffer, data );
                // wait now until the gpu has finished compiling the data
                compile.Wait();
                // make sure the write has completed
                this.Gpu.Wait();
                var watch = new Stopwatch();
                watch.Start();
                int size = data.Length;
                int remainder = 0;
                while ( size > 0 )
                {
                    remainder = size % reductionPerCall;
                    shader.NumberOfXThreads = size / reductionPerCall;
                    shader.RemoveAllBuffers();
                    shader.AddBuffer( constBuffer );
                    shader.AddBuffer( originalBuffer );
                    shader.AddBuffer( destinationBuffer );
                    // execute then flip the buffers
                    this.Gpu.ExecuteComputeShader( shader );
                    // compute the remainder
                    var temp = destinationBuffer;
                    destinationBuffer = originalBuffer;
                    originalBuffer = temp;
                    size = 0;
                }
                if ( remainder > 0 )
                {
                }
                Gpu.Wait();
                watch.Stop();
                using ( var writer = new StreamWriter( "gpu.txt", true ) )
                {
                    writer.WriteLine( "{0}", ( watch.ElapsedTicks / 10000f ) );
                }
            }
            finally
            {
                this.Gpu.ReleaseBuffer( constBuffer );
                this.Gpu.ReleaseBuffer( originalBuffer );
                this.Gpu.ReleaseBuffer( destinationBuffer );
                if ( shader != null )
                {
                    shader.Dispose();
                    shader = null;
                }
            }
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Dispose()
        {
            if ( this.Gpu != null )
            {
                this.Gpu.Dispose();
                this.Gpu = null;
            }
        }

        ~TestGPU()
        {
            this.Dispose();
        }
    }
}
