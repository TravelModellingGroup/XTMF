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
using System.Reflection;
using System.Threading.Tasks;
using Datastructure;
using TMG.DirectCompute;

namespace TMG.Functions
{
    public class GPUGravityModel
    {
        private float Epsilon;
        private float[] Friction;
        private int MaxIterations;
        private Action<float> ProgressCallback;

        public GPUGravityModel(float[] frictionValues, Action<float> progressCallback = null, float epsilon = 0.8f, int maxIterations = 100)
        {
            this.Friction = frictionValues;
            this.ProgressCallback = progressCallback;
            this.Epsilon = epsilon;
            this.MaxIterations = maxIterations;
        }

        public SparseTwinIndex<float> ProcessFlow(SparseArray<float> O, SparseArray<float> D)
        {
            float[] o = O.GetFlatData();
            float[] d = D.GetFlatData();
            var oLength = o.Length;
            var dLength = d.Length;
            var squareSize = oLength * dLength;
            float[] flows = new float[squareSize];
            float[] residules = new float[dLength];
            GPU gpu = new GPU();
            string programPath;
            var codeBase = Assembly.GetEntryAssembly().CodeBase;
            try
            {
                programPath = Path.GetFullPath( codeBase );
            }
            catch
            {
                programPath = codeBase.Replace( "file:///", String.Empty );
            }
            // Since the modules are always located in the ~/Modules subdirectory for XTMF,
            // we can just go in there to find the script
            ComputeShader gravityModelShader = null;
            Task compile = new Task( delegate()
            {
                gravityModelShader = gpu.CompileComputeShader( Path.Combine( Path.GetDirectoryName( programPath ), "Modules", "GravityModel.hlsl" ), "CSMain" );
                gravityModelShader.NumberOfXThreads = oLength;
                gravityModelShader.NumberOfYThreads = 1;
                gravityModelShader.ThreadGroupSizeX = 64;
                gravityModelShader.ThreadGroupSizeY = 1;
            } );
            compile.Start();
            GPUBuffer flowsBuffer = gpu.CreateBuffer( squareSize, 4, true );
            GPUBuffer attractionStarBuffer = gpu.CreateBuffer( oLength, 4, true );
            GPUBuffer balancedBuffer = gpu.CreateBuffer( 2, 4, true );
            GPUBuffer productionBuffer = gpu.CreateBuffer( dLength, 4, false );
            GPUBuffer attractionBuffer = gpu.CreateBuffer( oLength, 4, false );
            GPUBuffer frictionBuffer = gpu.CreateBuffer( squareSize, 4, false );
            GPUBuffer parameters = gpu.CreateConstantBuffer( 16 );
            float[] balanced = new float[] { 0, this.Epsilon };
            int iterations = 0;
            var step1 = new int[] { oLength, 0, this.MaxIterations };
            var step2 = new int[] { oLength, 1, this.MaxIterations };
            compile.Wait();
            Stopwatch watch = new Stopwatch();
            watch.Start();
            FillAndLoadBuffers( o, d, Friction, gpu, gravityModelShader, flowsBuffer,
                attractionStarBuffer, balancedBuffer, productionBuffer, attractionBuffer, frictionBuffer, parameters, balanced );
            if ( gravityModelShader == null )
            {
                throw new XTMF.XTMFRuntimeException( "Unable to compile the GravityModel GPU Kernel!" );
            }
            iterations = Balance( gpu, gravityModelShader, balancedBuffer, parameters, balanced, iterations, step1, step2 );
            gpu.Read( flowsBuffer, flows );
            gravityModelShader.RemoveAllBuffers();
            watch.Stop();
            using ( StreamWriter writer = new StreamWriter( "GPUPerf.txt", true ) )
            {
                writer.Write( "Iteraions:" );
                writer.WriteLine( iterations );
                writer.Write( "Time(ms):" );
                writer.WriteLine( watch.ElapsedMilliseconds );
            }
            gravityModelShader.Dispose();
            gpu.Release();
            return BuildDistribution( O, D, oLength, flows );
        }

        private static SparseTwinIndex<float> BuildDistribution(SparseArray<float> O, SparseArray<float> D, int oLength, float[] flows)
        {
            var ret = SparseTwinIndex<float>.CreateSimilarArray( O, D );
            var retFlat = ret.GetFlatData();
            System.Threading.Tasks.Parallel.For( 0, oLength,
                delegate(int i)
                {
                    var iOffset = i * oLength;
                    for ( int j = 0; j < oLength; j++ )
                    {
                        retFlat[i][j] = flows[iOffset + j];
                    }
                } );
            return ret;
        }

        private static void FillAndLoadBuffers(float[] o, float[] d, float[] Friction, GPU gpu, ComputeShader gravityModelShader, GPUBuffer flowsBuffer, GPUBuffer attractionStarBuffer, GPUBuffer balancedBuffer, GPUBuffer productionBuffer, GPUBuffer attractionBuffer, GPUBuffer frictionBuffer, GPUBuffer parameters, float[] balanced)
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

        private int Balance(GPU gpu, ComputeShader gravityModelShader, GPUBuffer balancedBuffer, GPUBuffer parameters, float[] balanced, int iterations, int[] step1, int[] step2)
        {
            do
            {
                this.ProgressCallback( (float)iterations / this.MaxIterations );
                gpu.Write( parameters, step1 );
                // Compute Flows
                gpu.ExecuteComputeShader( gravityModelShader );
                gpu.Write( parameters, step2 );
                // Compute Residues and check to see if we are all balanced
                gpu.ExecuteComputeShader( gravityModelShader );
                gpu.Read( balancedBuffer, balanced );
            } while ( ( ++iterations ) < this.MaxIterations && balanced[0] == 0 );
            return iterations;
        }
    }
}