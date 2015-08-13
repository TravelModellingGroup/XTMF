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
#include "Stdafx.h"
#include "TMG.GPU.h"

using namespace XTMF;

namespace TMG
{
	namespace DirectCompute
	{
		/*
		This is the general solution for writing to the GPU.  The class functions will just inline specific versions of this logic
		to implement themselves.
		*/
		template <class T>
		inline void GPUWrite(GPUContext* gpuContext, GPUBuffer^buffer, array<T>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			D3D11_MAPPED_SUBRESOURCE mappedResource;
			if (((length + destIndex) * sizeof(T) > buffer->Length * buffer->ElementSize) | (length + srcIndex > localBuffer->Length))
			{
				if ((length + destIndex) * sizeof(T) > buffer->Length * buffer->ElementSize)
				{
					throw gcnew XTMFRuntimeException("The gpu buffer was not large enough for this operation!");
				}
				else
				{
					throw gcnew XTMFRuntimeException("The data source was not large enough for this operation!");
				}
			}
			pin_ptr<T> pin = &localBuffer[0];
			if (buffer->Constant)
			{
				if (SUCCEEDED(gpuContext->g_pD3DContext->Map((ID3D11Buffer*)buffer->NativeBufferLocation, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource)))
				{
					T *data = (T*)mappedResource.pData;
					memcpy(data + destIndex, pin + srcIndex, length * sizeof(T));
					gpuContext->g_pD3DContext->Unmap((ID3D11Buffer*)buffer->NativeBufferLocation, 0);
				}
			}
			else
			{
				if (SUCCEEDED(gpuContext->g_pD3DContext->Map((ID3D11Buffer*)buffer->NativeStagingBuffer, 0, D3D11_MAP_WRITE, 0, &mappedResource)))
				{
					T *data = (T*)mappedResource.pData;
					memcpy(data + destIndex, pin + srcIndex, length * sizeof(T));
					gpuContext->g_pD3DContext->Unmap((ID3D11Buffer*)buffer->NativeStagingBuffer, 0);
					gpuContext->g_pD3DContext->CopyResource((ID3D11Buffer*)buffer->NativeBufferLocation, (ID3D11Buffer*)buffer->NativeStagingBuffer);
				}
			}
		}

		/*
		This is the general solution for reading from the GPU.  The class functions will just inline specific versions of this logic
		to implement themselves.
		*/
		template <class T>
		inline void GPURead(GPUContext* gpuContext, GPUBuffer^buffer, array<T>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			D3D11_MAPPED_SUBRESOURCE mappedResource;
			if (((length + srcIndex) * sizeof(T) > buffer->Length * buffer->ElementSize) | (length + destIndex > localBuffer->Length))
			{
				if ((length + srcIndex) * sizeof(T) > buffer->Length * buffer->ElementSize)
				{
					throw gcnew XTMFRuntimeException("The gpu buffer was not large enough for this operation!");
				}
				else
				{
					throw gcnew XTMFRuntimeException("The data destination was not large enough for this operation!");
				}
			}
			pin_ptr<T> pin = &localBuffer[0];
			gpuContext->g_pD3DContext->CopyResource((ID3D11Buffer*)buffer->NativeStagingBuffer, (ID3D11Buffer*)buffer->NativeBufferLocation);
			if (SUCCEEDED(gpuContext->g_pD3DContext->Map((ID3D11Buffer*)buffer->NativeStagingBuffer, 0, D3D11_MAP_READ, 0, &mappedResource)))
			{
				T *data = (T*)mappedResource.pData;
				memcpy(pin + destIndex, data + srcIndex, length * sizeof(T));
				gpuContext->g_pD3DContext->Unmap((ID3D11Buffer*)buffer->NativeStagingBuffer, 0);
			}
		}

		void GPU::Read(GPUBuffer^ buffer, array<Byte>^ localBuffer)
		{
			GPURead(this->Context, buffer, localBuffer, 0, 0, localBuffer->Length);
		}

		void GPU::Read(GPUBuffer^ buffer, array<Byte>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			GPURead(this->Context, buffer, localBuffer, srcIndex, destIndex, length);
		}

		void GPU::Write(GPUBuffer^ buffer, array<Byte>^ localBuffer)
		{
			GPUWrite(this->Context, buffer, localBuffer, 0, 0, localBuffer->Length);
		}

		void GPU::Write(GPUBuffer^ buffer, array<Byte>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			GPUWrite(this->Context, buffer, localBuffer, srcIndex, destIndex, length);
		}

		void GPU::Read(GPUBuffer^ buffer, array<float>^ localBuffer)
		{
			GPURead(this->Context, buffer, localBuffer, 0, 0, localBuffer->Length);
		}

		void GPU::Read(GPUBuffer^ buffer, array<float>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			GPURead(this->Context, buffer, localBuffer, srcIndex, destIndex, length);
		}

		void GPU::Write(GPUBuffer^ buffer, array<float>^ localBuffer)
		{
			GPUWrite(this->Context, buffer, localBuffer, 0, 0, localBuffer->Length);
		}

		void GPU::Write(GPUBuffer^ buffer, array<float>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			GPUWrite(this->Context, buffer, localBuffer, srcIndex, destIndex, length);
		}

		void GPU::Read(GPUBuffer^ buffer, array<double>^ localBuffer)
		{
			GPURead(this->Context, buffer, localBuffer, 0, 0, localBuffer->Length);
		}

		void GPU::Read(GPUBuffer^ buffer, array<double>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			GPURead(this->Context, buffer, localBuffer, srcIndex, destIndex, length);
		}

		void GPU::Write(GPUBuffer^ buffer, array<double>^ localBuffer)
		{
			GPUWrite(this->Context, buffer, localBuffer, 0, 0, localBuffer->Length);
		}

		void GPU::Write(GPUBuffer^ buffer, array<double>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			GPUWrite(this->Context, buffer, localBuffer, srcIndex, destIndex, length);
		}

		void GPU::Read(GPUBuffer^ buffer, array<int>^ localBuffer)
		{
			GPURead(this->Context, buffer, localBuffer, 0, 0, localBuffer->Length);
		}

		void GPU::Read(GPUBuffer^ buffer, array<int>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			GPURead(this->Context, buffer, localBuffer, srcIndex, destIndex, length);
		}

		void GPU::Write(GPUBuffer^ buffer, array<int>^ localBuffer)
		{
			GPUWrite(this->Context, buffer, localBuffer, 0, 0, localBuffer->Length);
		}

		void GPU::Write(GPUBuffer^ buffer, array<int>^ localBuffer, int srcIndex, int destIndex, int length)
		{
			GPUWrite(this->Context, buffer, localBuffer, srcIndex, destIndex, length);
		}

		void GPU::Clear(GPUBuffer^ buffer, int startingIndex, int endingIndex)
		{
			D3D11_MAPPED_SUBRESOURCE mappedResource;
			auto gpuContext = this->Context;
			auto startingMemoryLocation = startingIndex * buffer->ElementSize;
			auto totalMemoryToWipe = (endingIndex - startingIndex) * buffer->ElementSize;
			if (buffer->Constant)
			{
				if (SUCCEEDED(gpuContext->g_pD3DContext->Map((ID3D11Buffer*)buffer->NativeBufferLocation, 0, D3D11_MAP_WRITE_DISCARD, 0, &mappedResource)))
				{
					byte *data = (byte*)mappedResource.pData;
					memset(data + startingMemoryLocation, 0, totalMemoryToWipe);
					gpuContext->g_pD3DContext->Unmap((ID3D11Buffer*)buffer->NativeBufferLocation, 0);
				}
			}
			else
			{
				if (SUCCEEDED(gpuContext->g_pD3DContext->Map((ID3D11Buffer*)buffer->NativeStagingBuffer, 0, D3D11_MAP_WRITE, 0, &mappedResource)))
				{
					byte *data = (byte*)mappedResource.pData;
					memset(data + startingMemoryLocation, 0, totalMemoryToWipe);
					gpuContext->g_pD3DContext->Unmap((ID3D11Buffer*)buffer->NativeStagingBuffer, 0);
					gpuContext->g_pD3DContext->CopyResource((ID3D11Buffer*)buffer->NativeBufferLocation, (ID3D11Buffer*)buffer->NativeStagingBuffer);
				}
			}
		}
	}
}