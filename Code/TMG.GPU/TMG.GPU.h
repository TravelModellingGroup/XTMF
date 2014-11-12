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
// TMG.GPU.h

#pragma once
#pragma warning( disable : 4005 )

#include "D3DX11.h"

#if defined(DEBUG) || defined(_DEBUG)
#ifndef V
#define V(x)           { hr = (x); if( FAILED(hr) ) { DXUTTrace( __FILE__, (DWORD)__LINE__, hr, L#x, true ); } }
#endif
#ifndef V_RETURN
#define V_RETURN(x)    { hr = (x); if( FAILED(hr) ) { return DXUTTrace( __FILE__, (DWORD)__LINE__, hr, L#x, true ); } }
#endif
#else
#ifndef V
#define V(x)           { hr = (x); }
#endif
#ifndef V_RETURN
#define V_RETURN(x)    { hr = (x); if( FAILED(hr) ) { return hr; } }
#endif
#endif

#ifndef SAFE_RELEASE
#define SAFE_RELEASE(p)      { if (p) { (p)->Release(); (p)=NULL; } }
#endif

#ifndef SAFE_DELETE
#define SAFE_DELETE(bob)     { if (bob) { delete bob; bob = NULL; } }
#endif

using namespace System;
using namespace System::Diagnostics;
using namespace System::Collections::Generic;

namespace TMG
{
	namespace DirectCompute
	{
		public ref class GPUBuffer
		{
		internal:
			int* NativeBufferLocation;
			int* NativeViewerLocation;
			int* NativeStagingBuffer;
		public:
			bool ReadWrite;
			bool Constant;
			int ElementSize;
			int Length;
		};

		public ref class ComputeShader
		{
		internal:
			List<int>^ RandomAccessBuffer;
			List<int>^ ResourceBuffer;
			List<int>^ ConstantBuffer;
			List<GPUBuffer^>^ Buffers;
			int* ShaderCode;
			~ComputeShader();
		public:
			int NumberOfXThreads;
			int NumberOfYThreads;
			int ThreadGroupSizeX;
			int ThreadGroupSizeY;
			void AddBuffer(GPUBuffer^ buffer);
			void RemoveAllBuffers();
		};

		struct GPUContext
		{
			ID3D11Device *g_pD3DDevice;
			ID3D11DeviceContext *g_pD3DContext;
			D3D_FEATURE_LEVEL g_D3DFeatureLevel;
		};

		public ref class GPU
		{
		private:
			GPUContext* Context;
		public:
			// Get access to the GPU, this throws an exception is a GPU is unavailable
			GPU();
			// Allocate a new buffer on the GPU
			GPUBuffer^ CreateBuffer(int Length, int elementSize, bool readWrite);
			// Create a constant buffer
			GPUBuffer^ CreateConstantBuffer(int Length);
			// Deallocate the buffer on the GPU
			void ReleaseBuffer(GPUBuffer^ buffer);
			// Compile a compute shader from file
			ComputeShader^ CompileComputeShader(String^ ComputeShaderFile, String^ ComputeShaderMain);
			// Compile a compute shader from file with defines
			ComputeShader^ CompileComputeShader(String^ ComputeShaderFile, String^ ComputeShaderMain, array<String^>^ DefineNames, array<String^>^ DefineValues);
			//Read from the buffer to the local buffer
			void Read(GPUBuffer^ buffer, array<Byte>^ localBuffer);
			//Read from the buffer to the local buffer
			void Read(GPUBuffer^ buffer, array<float>^ localBuffer);
			//Read from the buffer to the local buffer
			void Read(GPUBuffer^ buffer, array<double>^ localBuffer);
			//Read from the buffer to the local buffer
			void Read(GPUBuffer^ buffer, array<int>^ localBuffer);
			//Write to buffer from the local buffer
			void Write(GPUBuffer^ buffer, array<Byte>^ localBuffer);
			//Write to buffer from the local buffer
			void Write(GPUBuffer^ buffer, array<float>^ localBuffer);
			//Write to buffer from the local buffer
			void Write(GPUBuffer^ buffer, array<double>^ localBuffer);
			//Write to buffer from the local buffer
			void Write(GPUBuffer^ buffer, array<int>^ localBuffer);
			//Read from the buffer starting at the source index to the local buffer's destination index
			void Read(GPUBuffer^ buffer, array<Byte>^ localBuffer, int srcIndex, int destIndex, int length);
			//Read from the buffer starting at the source index to the local buffer's destination index
			void Read(GPUBuffer^ buffer, array<float>^ localBuffer, int srcIndex,int destIndex, int length);
			//Read from the buffer starting at the source index to the local buffer's destination index
			void Read(GPUBuffer^ buffer, array<double>^ localBuffer, int srcIndex, int destIndex, int length);
			//Read from the buffer starting at the source index to the local buffer's destination index
			void Read(GPUBuffer^ buffer, array<int>^ localBuffer, int srcIndex, int destIndex, int length);
			//Write to the buffer starting at the destination index from the local buffer's source index
			void Write(GPUBuffer^ buffer, array<Byte>^ localBuffer, int srcIndex, int destIndex, int length);
			//Write to the buffer starting at the destination index from the local buffer's source index
			void Write(GPUBuffer^ buffer, array<float>^ localBuffer, int srcIndex, int destIndex, int length);
			//Write to the buffer starting at the destination index from the local buffer's source index
			void Write(GPUBuffer^ buffer, array<double>^ localBuffer, int srcIndex, int destIndex, int length);
			//Write to the buffer starting at the destination index from the local buffer's source index
			void Write(GPUBuffer^ buffer, array<int>^ localBuffer, int srcIndex, int destIndex, int length);

			//Clear out the data in the buffer between the starting index to (but not including) the ending index
			void Clear(GPUBuffer^ buffer, int startingIndex, int endingIndexExclusive);

			void ExecuteComputeShader(ComputeShader^ shader);
			// Wait for the GPU to complete all of its tasks before continuing
			void Wait();
			// Dispose of the GPU's connection, this will automatically release all memory buffers
			void Release();
			// Do not access this
			List<GPUBuffer^>^ Buffers;
			~GPU();
		};
	}
}
