using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using TMG.DirectCompute;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace James.UTDM
{

    public class TestHighPerformanceAdding : XTMF.IModelSystemTemplate
    {
        public string InputBaseDirectory { get; set; }

        public string Name { get; set; }

        public string OutputBaseDirectory { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); } }

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        class HighPerformanceAdding
        {
            private float[] DataToAdd;
            private GPU Gpu;
            private ComputeShader Adder;
            public HighPerformanceAdding (float[] dataToAdd, GPU gpu, ComputeShader adder)
	        {
                DataToAdd = dataToAdd;
                Gpu = gpu;
                Adder = adder;
	        }

            private struct AddStream
            {
                private GPU Gpu;
                private int BufferSize;
                private float[] Data;

                public AddStream(GPU gpu, int bufferSize, float[] data) : this()
                {
                    Gpu = gpu;
                    BufferSize = bufferSize;
                    Data = data;
                    Buffer = gpu.CreateBuffer(BufferSize, sizeof(float), true);
                }

                private GPUBuffer Buffer;

                private int PassLength;

                internal void LoadData(int index)
                {
                    var length = Math.Min( this.BufferSize, Data.Length - ( index + this.BufferSize ) );
                    if ( length > 0 )
                    {
                        Gpu.Write(this.Buffer, Data, index, 0, length);
                    }
                    else
                    {
                        //we are in a remainder case
                        length = Data.Length - index;
                        // if there is any data left to work with
                        if ( length > 0 )
                        {
                            this.Gpu.Write(this.Buffer, Data, index, 0, length);
                            this.Gpu.Clear( this.Buffer, length, this.BufferSize );
                        }
                    }
                    PassLength = length;
                }

                internal void ApplyPass(ComputeShader adder, GPUBuffer constant, int index)
                {
                    if ( PassLength > 0 )
                    {
                        adder.NumberOfXThreads = this.BufferSize / 512;
                        Gpu.ExecuteComputeShader( adder );
                    }
                }

                internal float GetAnswer(float[] tempBuffer)
                {
                    float total = 0.0f;
                    // if we have any data to read
                    if ( PassLength > 0 )
                    {
                        var elementsToRead = this.BufferSize / 512;
                        Gpu.Read( this.Buffer, tempBuffer, 0, 0, elementsToRead );
                        if ( elementsToRead == tempBuffer.Length )
                        {
                            for ( int i = 0; i < tempBuffer.Length; i++ )
                            {
                                total += tempBuffer[i];
                            }
                        }
                        else
                        {
                            for ( int i = 0; i < elementsToRead; i++ )
                            {
                                total += tempBuffer[i];
                            }
                        }
                    }
                    return total;
                }

                internal void AttachBuffer(ComputeShader adder)
                {
                    adder.AddBuffer( Buffer );
                }
            }

            public float Add(int bufferSize)
            {
                Adder.ThreadGroupSizeX = 64;
                Adder.RemoveAllBuffers();
                var stream = new AddStream( Gpu, bufferSize, DataToAdd );
                int index = 0;
                var total = 0.0f;
                var constBuffer = Gpu.CreateConstantBuffer( 16 );                
                var tempBuffer = new float[bufferSize / 512];
                Adder.AddBuffer(constBuffer);
                stream.AttachBuffer(Adder);
                while ( index < DataToAdd.Length )
                {
                    // Load up the next set of data
                    stream.LoadData( index );
                    stream.ApplyPass(this.Adder, constBuffer, index);
                    total += stream.GetAnswer( tempBuffer );
                    index += bufferSize;
                }
                return total;
            }
        }

        public void Start()
        {
            using (var gpu = new GPU())
            {
                var data = new float[32000000];
                InitializeData( data );
                ComputeShader shader = CompileShader( gpu );
                var stopwatch = new Stopwatch();
                for ( int i = 14; i < 20; i++ )
                {
                    var bufferSize = 2 << i;
                    for ( int iteration = 0; iteration < 100; iteration++ )
                    {
                        gpu.Wait();
                        stopwatch.Start();
                        var adder = new HighPerformanceAdding( data, gpu, shader );
                        var result = adder.Add( bufferSize );
                        gpu.Wait();
                        stopwatch.Stop();
                    }
                    Console.WriteLine( "A buffer size of " + ( bufferSize ) + " took " + stopwatch.ElapsedMilliseconds + " ms for 100 runs " );
                    stopwatch.Reset();
                }
                stopwatch.Start();
                for ( int i = 0; i < 100; i++ )
                {
                    data.Sum();
                }
                stopwatch.Stop();
                Console.WriteLine( "The cpu took " + stopwatch.ElapsedMilliseconds + "ms for 100 runs." );
            }
        }

        private static void InitializeData(float[] data)
        {
            for ( int i = 0; i < data.Length; i++ )
            {
                data[i] = 1;
            }
        }

        private ComputeShader CompileShader(GPU gpu)
        {
            var codeBase = Assembly.GetEntryAssembly().CodeBase.Replace( "file:///", "" );
            var fileName = Path.Combine( Path.GetDirectoryName( codeBase ), "Modules", "James.UTDM.TestGPU.hlsl" );
            var shader = gpu.CompileComputeShader( fileName, "CSMain" );
            if ( shader == null )
            {
                throw new XTMFRuntimeException( "We were unable to compile the compute shader!" );
            }
            shader.NumberOfXThreads = 1024;
            shader.NumberOfYThreads = 1;
            shader.ThreadGroupSizeX = 512;
            shader.ThreadGroupSizeY = 1;
            return shader;
        }
    }

}
