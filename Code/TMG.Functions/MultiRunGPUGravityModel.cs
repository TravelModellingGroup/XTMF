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
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datastructure;
using TMG.DirectCompute;
using XTMF;

namespace TMG.Functions
{
    public class MultiRunGPUGravityModel : IDisposable
    {
        private GPUBuffer attractionBuffer;
        private GPUBuffer attractionStarBuffer;
        private GPUBuffer balancedBuffer;
        private float Epsilon;
        private float[] flows;
        private GPUBuffer flowsBuffer;
        private GPUBuffer frictionBuffer;
        private GPU gpu;
        private ComputeShader gravityModelShader;
        private int length;
        private int MaxIterations;
        private GPUBuffer parameters;
        private GPUBuffer productionBuffer;
        private Action<float> ProgressCallback;

        public MultiRunGPUGravityModel(int length, Action<float> progressCallback = null, float epsilon = 0.8f, int maxIterations = 100)
        {
            var programPath = Assembly.GetEntryAssembly().CodeBase.Replace( "file:///", String.Empty );
            try
            {
                this.gpu = new GPU();
            }
            catch
            {
                throw new XTMFRuntimeException( "Unable to create a connection to the GPU, please make sure you are using a DirectX11+ card!" );
            }
            Task initialize = new Task( delegate()
                {
                    this.length = length;
                    this.ProgressCallback = progressCallback;
                    this.Epsilon = epsilon;
                    this.MaxIterations = maxIterations;
                    CreateBuffers();
                } );
            initialize.Start();
            // while everything else is being initialized, compile the shader
            this.gravityModelShader = gpu.CompileComputeShader( Path.Combine( Path.GetDirectoryName( programPath ), "Modules", "GravityModel.hlsl" ), "CSMain" );
            initialize.Wait();
            if ( this.gravityModelShader == null )
            {
                throw new XTMFRuntimeException( "Unable to compile GravityModel.hlsl!" );
            }
        }

        ~MultiRunGPUGravityModel()
        {
            this.Dispose( false );
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( true );
        }

        public SparseTwinIndex<float> ProcessFlow(float[] Friction, SparseArray<float> O, SparseArray<float> D)
        {
            float[] o = O.GetFlatData();
            float[] d = D.GetFlatData();
            var oLength = o.Length;
            var dLength = d.Length;
            var squareSize = oLength * dLength;
            Stopwatch watch = new Stopwatch();
            watch.Start();
            gravityModelShader.NumberOfXThreads = length;
            gravityModelShader.NumberOfYThreads = 1;
            gravityModelShader.ThreadGroupSizeX = 64;
            gravityModelShader.ThreadGroupSizeY = 1;
            float[] balanced = new float[] { 0, this.Epsilon };
            int iterations = 0;
            var step1 = new int[] { oLength, 0, this.MaxIterations };
            var step2 = new int[] { oLength, 1, this.MaxIterations };
            if ( flows == null || flows.Length != o.Length * d.Length )
            {
                flows = new float[squareSize];
            }
            SparseTwinIndex<float> ret = null;
            Task createReturn = new Task( delegate()
            {
                ret = O.CreateSquareTwinArray<float>();
            } );
            createReturn.Start();
            FillAndLoadBuffers( o, d, Friction, balanced );
            iterations = Balance( gpu, gravityModelShader, balancedBuffer, parameters, balanced, iterations, step1, step2 );
            gpu.Read( flowsBuffer, flows );
            gravityModelShader.RemoveAllBuffers();
            createReturn.Wait();
            BuildDistribution( ret, O, D, oLength, flows );
            watch.Stop();
            using ( StreamWriter writer = new StreamWriter( "GPUPerf.txt", true ) )
            {
                writer.Write( "Iterations:" );
                writer.WriteLine( iterations );
                writer.Write( "Time(ms):" );
                writer.WriteLine( watch.ElapsedMilliseconds );
            }
            return ret;
        }

        private static void BuildDistribution(SparseTwinIndex<float> ret, SparseArray<float> O, SparseArray<float> D, int oLength, float[] flows)
        {
            var retFlat = ret.GetFlatData();
            var ratio = O.GetFlatData().Sum() / flows.Sum();
            if ( float.IsNaN( ratio ) | float.IsInfinity( ratio ) )
            {
                Parallel.For( 0, retFlat.Length, delegate(int i)
                {
                    var iOffset = i * oLength;
                    var ith = retFlat[i];
                    for ( int j = 0; j < oLength; j++ )
                    {
                        ith[j] = ( float.IsNaN( ratio ) | float.IsInfinity( ratio ) ) ? 0f : flows[iOffset + j];
                    }
                } );
                return;
            }
            Parallel.For( 0, retFlat.Length, delegate(int i)
            {
                var iOffset = i * oLength;
                var ith = retFlat[i];
                for ( int j = 0; j < oLength; j++ )
                {
                    ith[j] = flows[iOffset + j] * ratio;
                }
            } );
        }

        private int Balance(GPU gpu, ComputeShader gravityModelShader, GPUBuffer balancedBuffer, GPUBuffer parameters, float[] balanced, int iterations, int[] step1, int[] step2)
        {
            do
            {
                if ( this.ProgressCallback != null )
                {
                    this.ProgressCallback( (float)iterations / this.MaxIterations );
                }
                gpu.Write( parameters, step1 );
                // Compute Flows
                gpu.ExecuteComputeShader( gravityModelShader );
                gpu.Write( parameters, step2 );
                // Compute Residues and check to see if we are all balanced
                gpu.ExecuteComputeShader( gravityModelShader );
                gpu.Read( balancedBuffer, balanced );
            } while ( ( ++iterations ) < this.MaxIterations && balanced[0] == 0 );
            if ( this.ProgressCallback != null )
            {
                this.ProgressCallback( 1f );
            }
            return iterations;
        }

        private void CreateBuffers()
        {
            var squareSize = length * length;
            flowsBuffer = gpu.CreateBuffer( squareSize, 4, true );
            attractionStarBuffer = gpu.CreateBuffer( length, 4, true );
            balancedBuffer = gpu.CreateBuffer( 2, 4, true );
            productionBuffer = gpu.CreateBuffer( length, 4, false );
            attractionBuffer = gpu.CreateBuffer( length, 4, false );
            frictionBuffer = gpu.CreateBuffer( squareSize, 4, false );
            parameters = gpu.CreateConstantBuffer( 16 );
        }

        protected virtual void Dispose(bool userCalled)
        {
            if ( this.gravityModelShader != null )
            {
                this.gravityModelShader.Dispose();
                this.gravityModelShader = null;
            }
            if ( this.gpu != null )
            {
                this.gpu.Dispose();
                this.gpu = null;
            }
        }

        private void FillAndLoadBuffers(float[] o, float[] d, float[] Friction, float[] balanced)
        {
            gpu.Write( balancedBuffer, balanced );
            gpu.Write( productionBuffer, o );
            gpu.Write( attractionBuffer, d );
            gpu.Write( attractionStarBuffer, d );
            gpu.Write( frictionBuffer, Friction );
            // The order matters, needs to be the same as in the shader code!!!
            gravityModelShader.AddBuffer( parameters );
            gravityModelShader.AddBuffer( flowsBuffer );
            gravityModelShader.AddBuffer( attractionStarBuffer );
            gravityModelShader.AddBuffer( balancedBuffer );
            gravityModelShader.AddBuffer( productionBuffer );
            gravityModelShader.AddBuffer( attractionBuffer );
            gravityModelShader.AddBuffer( frictionBuffer );
        }
    }
}