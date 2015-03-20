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
// This is the main DLL file.

#include "stdafx.h"

// We need to include this header last otherwise it will cause a naming conflict
#include "TMG.GPU.h"
#include <vcclr.h> // required header for PtrToStringChars

namespace TMG
{
	namespace DirectCompute
	{
		GPU::GPU()
		{
			// Initialize the order for this
			this->Buffers = gcnew List<GPUBuffer^>();
			this->Context = new GPUContext();
			HRESULT hr;

			D3D_FEATURE_LEVEL levelsWanted[] =
			{
				D3D_FEATURE_LEVEL_11_0,
				D3D_FEATURE_LEVEL_10_1
			};
			UINT numLevelsWanted = sizeof(levelsWanted) / sizeof(levelsWanted[0]);

			D3D_DRIVER_TYPE driverTypes[] =
			{
				D3D_DRIVER_TYPE_HARDWARE
			};
			UINT numDriverTypes = sizeof(driverTypes) / sizeof(driverTypes[0]);
			bool succeeded = false;

			for (UINT driverTypeIndex = 0; driverTypeIndex < numDriverTypes; driverTypeIndex++)
			{
				// Now we can move onto actually getting the device context
				D3D_DRIVER_TYPE g_driverType = driverTypes[driverTypeIndex];
				UINT createDeviceFlags = D3D11_CREATE_DEVICE_SINGLETHREADED;
				hr = D3D11CreateDevice(NULL, g_driverType, NULL, createDeviceFlags,
					levelsWanted, numLevelsWanted, D3D11_SDK_VERSION,
					&this->Context->g_pD3DDevice, &this->Context->g_D3DFeatureLevel, &this->Context->g_pD3DContext);
				if (SUCCEEDED(hr))
				{
					succeeded = true;
					break;
				}
			}
			if (!succeeded)
			{
				// Problem? Throw an exception back, letting everyone know that you need a better/DX11 GPU + Windows Vista / Windows 7+
				throw gcnew XTMF::XTMFRuntimeException(L"We were unable to initialize a connection to the GPU!\r\nPlease make sure you are on a DirectX11+ machine.");
			}
		}

		GPU::~GPU()
		{
			this->Release();
		}

		void GPU::Wait()
		{
			D3D11_QUERY_DESC queryDescriptor;
			queryDescriptor.Query = D3D11_QUERY_EVENT;
			queryDescriptor.MiscFlags = 0;
			ID3D11Query* query;
			// create an event query
			if (this->Context->g_pD3DDevice->CreateQuery(&queryDescriptor, &query) != S_OK)
			{
				return;
			}
			// Tell the GPU that we want to know when this is true
			this->Context->g_pD3DContext->Begin(query);
			this->Context->g_pD3DContext->End(query);
			bool complete;
			// loop until we have completed
			while (S_OK != this->Context->g_pD3DContext->GetData(query, NULL, 0, 0))
				;
			// now that it has finished release the query
			query->Release();
		}

		//--------------------------------------------------------------------------------------
		// Create Structured Buffer
		//--------------------------------------------------------------------------------------
		HRESULT CreateStructuredBuffer(ID3D11Device* pDevice, UINT uElementSize, UINT uCount, VOID* pInitData, ID3D11Buffer** ppBufOut)
		{
			*ppBufOut = NULL;

			D3D11_BUFFER_DESC desc;
			ZeroMemory(&desc, sizeof(desc));
			desc.BindFlags = D3D11_BIND_UNORDERED_ACCESS | D3D11_BIND_SHADER_RESOURCE;
			desc.ByteWidth = uElementSize * uCount;
			desc.MiscFlags = D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;
			desc.StructureByteStride = uElementSize;

			if (pInitData)
			{
				D3D11_SUBRESOURCE_DATA InitData;
				InitData.pSysMem = pInitData;
				return pDevice->CreateBuffer(&desc, &InitData, ppBufOut);
			}
			else
				return pDevice->CreateBuffer(&desc, NULL, ppBufOut);
		}

		//--------------------------------------------------------------------------------------
		// Create a staging buffer to copy data to and from the GPU
		//--------------------------------------------------------------------------------------
		HRESULT CreateStagingBuffer(ID3D11Device* pDevice, UINT uElementSize, UINT uCount, ID3D11Buffer** ppBufOut)
		{
			*ppBufOut = NULL;
			D3D11_BUFFER_DESC stagingBufferDesc;
			stagingBufferDesc.ByteWidth = uElementSize * uCount;
			stagingBufferDesc.Usage = D3D11_USAGE_STAGING;
			stagingBufferDesc.BindFlags = 0;
			stagingBufferDesc.CPUAccessFlags = (D3D11_CPU_ACCESS_READ | D3D11_CPU_ACCESS_WRITE);
			stagingBufferDesc.MiscFlags = D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;
			stagingBufferDesc.StructureByteStride = uElementSize;
			return pDevice->CreateBuffer(&stagingBufferDesc, NULL, ppBufOut);
		}

		HRESULT CreateBufferUAV(ID3D11Device* pDevice, ID3D11Buffer* pBuffer, ID3D11UnorderedAccessView** ppUAVOut)
		{
			D3D11_BUFFER_DESC descBuf;
			ZeroMemory(&descBuf, sizeof(descBuf));
			pBuffer->GetDesc(&descBuf);

			D3D11_UNORDERED_ACCESS_VIEW_DESC desc;
			ZeroMemory(&desc, sizeof(desc));
			desc.ViewDimension = D3D11_UAV_DIMENSION_BUFFER;
			desc.Buffer.FirstElement = 0;
			descBuf.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE | D3D11_CPU_ACCESS_READ;
			if (descBuf.MiscFlags & D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS)
			{
				// This is a Raw Buffer

				desc.Format = DXGI_FORMAT_R32_TYPELESS; // Format must be DXGI_FORMAT_R32_TYPELESS, when creating Raw Unordered Access View
				desc.Buffer.Flags = D3D11_BUFFER_UAV_FLAG_RAW;
				desc.Buffer.NumElements = descBuf.ByteWidth / 4;
			}
			else if (descBuf.MiscFlags & D3D11_RESOURCE_MISC_BUFFER_STRUCTURED)
			{
				// This is a Structured Buffer

				desc.Format = DXGI_FORMAT_UNKNOWN;      // Format must be must be DXGI_FORMAT_UNKNOWN, when creating a View of a Structured Buffer
				desc.Buffer.NumElements = descBuf.ByteWidth / descBuf.StructureByteStride;
			}
			else
			{
				return E_INVALIDARG;
			}

			return pDevice->CreateUnorderedAccessView(pBuffer, &desc, ppUAVOut);
		}

		//--------------------------------------------------------------------------------------
		// Create Shader Resource View for Structured or Raw Buffers
		//--------------------------------------------------------------------------------------
		HRESULT CreateBufferSRV(ID3D11Device* pDevice, ID3D11Buffer* pBuffer, ID3D11ShaderResourceView** ppSRVOut)
		{
			D3D11_BUFFER_DESC descBuf;
			ZeroMemory(&descBuf, sizeof(descBuf));
			pBuffer->GetDesc(&descBuf);

			D3D11_SHADER_RESOURCE_VIEW_DESC desc;
			ZeroMemory(&desc, sizeof(desc));
			desc.ViewDimension = D3D11_SRV_DIMENSION_BUFFEREX;
			desc.BufferEx.FirstElement = 0;
			descBuf.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE | D3D11_CPU_ACCESS_READ;
			if (descBuf.MiscFlags & D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS)
			{
				// This is a Raw Buffer

				desc.Format = DXGI_FORMAT_R32_TYPELESS;
				desc.BufferEx.Flags = D3D11_BUFFEREX_SRV_FLAG_RAW;
				desc.BufferEx.NumElements = descBuf.ByteWidth / 4;
			}
			else
				if (descBuf.MiscFlags & D3D11_RESOURCE_MISC_BUFFER_STRUCTURED)
				{
					// This is a Structured Buffer

					desc.Format = DXGI_FORMAT_UNKNOWN;
					desc.BufferEx.NumElements = descBuf.ByteWidth / descBuf.StructureByteStride;
				}
				else
				{
					return E_INVALIDARG;
				}

			return pDevice->CreateShaderResourceView(pBuffer, &desc, ppSRVOut);
		}

		GPUBuffer^ GPU::CreateBuffer(int Length, int elementSize, bool readWrite)
		{
			HRESULT hr = S_OK;
			ID3D11Buffer *structBuff;
			hr = CreateStructuredBuffer(this->Context->g_pD3DDevice, elementSize,
				Length, NULL, &structBuff);
			// If we can't get the buffer, return null
			if (!SUCCEEDED(hr))
			{
				return nullptr;
			}
			ID3D11Buffer *stageBuff;
			hr = CreateStagingBuffer(this->Context->g_pD3DDevice, elementSize,
				Length, &stageBuff);
			if (!SUCCEEDED(hr))
			{
				SAFE_RELEASE(structBuff);
				return nullptr;
			}
			int* view = 0;
			if (readWrite)
			{
				ID3D11UnorderedAccessView *UAV;
				hr = CreateBufferUAV(this->Context->g_pD3DDevice, structBuff, &UAV);
				view = (int*)UAV;
			}
			else
			{
				ID3D11ShaderResourceView* SRV;
				hr = CreateBufferSRV(this->Context->g_pD3DDevice, structBuff, &SRV);
				view = (int*)SRV;
			}

			if (!SUCCEEDED(hr))
			{
				SAFE_RELEASE(structBuff);
				return nullptr;
			}

			GPUBuffer^ newbuff = gcnew GPUBuffer();
			newbuff->NativeBufferLocation = (int*)structBuff;
			newbuff->NativeViewerLocation = (int*)view;
			newbuff->NativeStagingBuffer = (int*)stageBuff;
			newbuff->Length = Length;
			newbuff->ElementSize = elementSize;
			newbuff->ReadWrite = readWrite;
			newbuff->Constant = false;
			this->Buffers->Add(newbuff);
			return newbuff;
		}

		//--------------------------------------------------------------------------------------
		// Create a staging buffer to copy data to and from the GPU
		//--------------------------------------------------------------------------------------
		HRESULT CreateCBuffer(ID3D11Device* pDevice, UINT uElementSize, ID3D11Buffer** ppBufOut)
		{
			*ppBufOut = NULL;
			// make sure it is a multiple of 16
			int residule = uElementSize % 16;
			if (residule != 0)
			{
				uElementSize = uElementSize + (16 - residule);
			}
			D3D11_BUFFER_DESC stagingBufferDesc;
			stagingBufferDesc.ByteWidth = uElementSize;
			stagingBufferDesc.Usage = D3D11_USAGE_DYNAMIC;
			stagingBufferDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
			stagingBufferDesc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
			stagingBufferDesc.MiscFlags = 0;
			stagingBufferDesc.StructureByteStride = uElementSize;
			return pDevice->CreateBuffer(&stagingBufferDesc, NULL, ppBufOut);
		}

		GPUBuffer^ GPU::CreateConstantBuffer(int Length)
		{
			HRESULT hr = S_OK;

			ID3D11Buffer *structBuff;
			hr = CreateCBuffer(this->Context->g_pD3DDevice, Length, &structBuff);

			// If we can't get the buffer, return null
			if (FAILED(hr))
			{
				throw gcnew XTMF::XTMFRuntimeException("Failed to create a Constant Buffer (ERROR CODE: "
					+ (gcnew System::UInt32((unsigned int)hr))->ToString() + " )!");
				return nullptr;
			}

			GPUBuffer^ newbuff = gcnew GPUBuffer();
			newbuff->NativeBufferLocation = (int*)structBuff;
			newbuff->NativeViewerLocation = 0;
			newbuff->NativeStagingBuffer = 0;
			newbuff->Length = 1;
			newbuff->ElementSize = Length;
			newbuff->ReadWrite = false;
			newbuff->Constant = true;
			this->Buffers->Add(newbuff);
			return newbuff;
		}

		void GPU::ReleaseBuffer(GPUBuffer^ buffer)
		{
			if (buffer->NativeBufferLocation)
			{
				((ID3D11Buffer*)buffer->NativeBufferLocation)->Release();
				buffer->NativeBufferLocation = 0;
			}
			if (buffer->NativeViewerLocation)
			{
				((ID3D11UnorderedAccessView*)buffer->NativeViewerLocation)->Release();
				buffer->NativeViewerLocation = 0;
			}
			if (buffer->NativeStagingBuffer)
			{
				((ID3D11Buffer*)(buffer->NativeStagingBuffer))->Release();
				buffer->NativeStagingBuffer = 0;
			}
			this->Buffers->Remove(buffer);
		}

		ComputeShader^ GPU::CompileComputeShader(String^ ComputeShaderFile, String^ ComputeShaderMain)
		{
			return this->CompileComputeShader(ComputeShaderFile, ComputeShaderMain, nullptr, nullptr);
		}

		template <class T>
		inline void CreateString(String^ string, T** destination)
		{
			int i;
			auto length = string->Length;
			T *row = new T[length + 1];
			*destination = row;
			for (i = 0; i < length; i++)
			{
				auto unConstRow = const_cast<T&>(row[i]);
				unConstRow = string[i];
			}
			auto unConstRow2 = const_cast<T&>(row[i]);
			unConstRow2 = NULL;
		}

		inline void CreateDefines(D3D_SHADER_MACRO* &defines, array<String^>^ DefineNames, array<String^>^ DefineValues)
		{
			int i;
			auto length = DefineNames->Length;
			defines = new D3D_SHADER_MACRO[length];
			for (i = 0; i < length; i++)
			{
				CreateString(DefineNames[i], &(defines[i].Name));
				CreateString(DefineValues[i], &(defines[i].Definition));
			}
		}

		void ReleaseDefines(D3D_SHADER_MACRO** defines, array<String^>^ DefineNames, array<String^>^ DefineValues)
		{
			// Guard against null
			if (*defines == NULL) 
				return;
			int i;
			auto length = DefineNames->Length;
			for (i = 0; i < length; i++)
			{
				delete[] (*defines[i]).Name;
				delete[] (*defines[i]).Definition;
			}
			delete[] *defines;
			*defines = NULL;
		}

		ComputeShader^ GPU::CompileComputeShader(String^ ComputeShaderFile, String^ ComputeShaderMain, array<String^>^ DefineNames, array<String^>^ DefineValues)
		{
			HRESULT hr = S_OK;
			int i;
			int csfLength = ComputeShaderFile->Length;
			int csmLength = ComputeShaderMain->Length;
			WCHAR* csf = new WCHAR[csfLength + 1];
			char* csm = new char[csmLength + 1];;

			for (i = 0; i < csfLength; i++)
			{
				csf[i] = ComputeShaderFile[i];
			}
			csf[i] = 0;

			for (i = 0; i < csmLength; i++)
			{
				csm[i] = ComputeShaderMain[i];
			}
			csm[i] = 0;
			D3D_SHADER_MACRO* defines = NULL;
			if (DefineNames != nullptr & DefineValues != nullptr)
			{
				CreateDefines(defines, DefineNames, DefineValues);
			}
			ID3DBlob* pBlobOut = NULL;
			ID3DBlob* pErrorBlob = NULL;
			ID3D11ComputeShader* compiledShader = NULL;
			// (1 << 13) == Force IEEE floating point strictness + O3 == (1 << 15)|(1 << 13)
			hr = D3DCompileFromFile(csf, defines, NULL, csm, "cs_5_0",
				(1 << 1) | (1 << 13) | (1 << 11) | (1 << 15), 0, &pBlobOut, &pErrorBlob);

			ReleaseDefines(&defines, DefineNames, DefineValues);

			if (!SUCCEEDED(hr))
			{
				delete[] csf;
				delete[] csm;
				return nullptr;
			}
			hr = this->Context->g_pD3DDevice->CreateComputeShader(pBlobOut->GetBufferPointer(),
				pBlobOut->GetBufferSize(), NULL, &compiledShader);
			ComputeShader^ shader = nullptr;
			ComputeShader^ tempShader = nullptr;
			try
			{
				if (SUCCEEDED(hr))
				{
					tempShader = gcnew ComputeShader();
					tempShader->ShaderCode = (int*)compiledShader;
					tempShader->Buffers = gcnew List<GPUBuffer^>();
					tempShader->RandomAccessBuffer = gcnew List<int>();
					tempShader->ResourceBuffer = gcnew List<int>();
					tempShader->ConstantBuffer = gcnew List<int>();
					shader = tempShader;
				}
				// Release the buffer that we were using for dealing with strings
				delete[] csf;
				delete[] csm;
				tempShader = nullptr;
			}
			finally
			{
				if (tempShader != nullptr)
				{
					delete tempShader;
				}
			}
			return shader;
		}

		void CreateTempArray(List<GPUBuffer^>^ shaderBuffers, List<int>^ indexes, bool view, int** saveTo)
		{
			for (int i = 0; i < indexes->Count; i++)
			{
				if (view)
				{
					saveTo[i] = shaderBuffers[indexes[i]]->NativeViewerLocation;
				}
				else
				{
					saveTo[i] = shaderBuffers[indexes[i]]->NativeBufferLocation;
				}
			}
		}

		void GPU::ExecuteComputeShader(ComputeShader^ shader)
		{
			int* tempArray[32];
			UINT initCounts = 0;
			this->Context->g_pD3DContext->CSSetShader((ID3D11ComputeShader*)shader->ShaderCode, NULL, 0);
			// Initialize the constant buffers
			if (shader->ConstantBuffer->Count > 0)
			{
				CreateTempArray(shader->Buffers, shader->ConstantBuffer, false, tempArray);
				this->Context->g_pD3DContext->CSSetConstantBuffers(0, shader->ConstantBuffer->Count, (ID3D11Buffer**)tempArray);
			}
			// Initialize the Resource Buffer
			if (shader->ResourceBuffer->Count > 0)
			{
				CreateTempArray(shader->Buffers, shader->ResourceBuffer, true, tempArray);
				this->Context->g_pD3DContext->CSSetShaderResources(0, shader->ResourceBuffer->Count, (ID3D11ShaderResourceView**)tempArray);
			}
			// Initialize the random access views
			if (shader->RandomAccessBuffer->Count > 0)
			{
				CreateTempArray(shader->Buffers, shader->RandomAccessBuffer, true, tempArray);
#pragma warning(suppress: 6387)
				this->Context->g_pD3DContext->CSSetUnorderedAccessViews(0, shader->RandomAccessBuffer->Count, (ID3D11UnorderedAccessView**)tempArray, NULL);
			}
			// Actually run the code
			if (shader->ThreadGroupSizeX == 0)
			{
				if (shader->NumberOfXThreads > 1)
				{
					shader->ThreadGroupSizeX = 64;
				}
				else
				{
					shader->ThreadGroupSizeX = 1;
				}
			}
			if (shader->ThreadGroupSizeY == 0)
			{
				if (shader->NumberOfYThreads > 1)
				{
					shader->ThreadGroupSizeY = 64;
				}
				else
				{
					shader->ThreadGroupSizeY = 1;
				}
			}
			int remainderX = shader->NumberOfXThreads % shader->ThreadGroupSizeX;
			int remainderY = shader->NumberOfYThreads % shader->ThreadGroupSizeY;
			int x = shader->NumberOfXThreads / shader->ThreadGroupSizeX + (remainderX > 0 ? 1 : 0);
			int y = shader->NumberOfYThreads / shader->ThreadGroupSizeY + (remainderY > 0 ? 1 : 0);
			this->Context->g_pD3DContext->Dispatch(x, y, 1);
		}

		void GPU::Release()
		{
			if (this->Buffers != nullptr)
			{
				while (this->Buffers->Count > 0)
				{
					this->ReleaseBuffer(this->Buffers[0]);
				}
				this->Buffers = nullptr;
			}
			if (this->Context)
			{
				SAFE_RELEASE(this->Context->g_pD3DDevice)
					SAFE_RELEASE(this->Context->g_pD3DContext);
				delete this->Context;
				this->Context = 0;
			}
		}
	}
}