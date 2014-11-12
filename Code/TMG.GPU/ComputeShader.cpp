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
		void ComputeShader::AddBuffer(GPUBuffer^ buffer)
		{
			int pos = this->Buffers->Count;
			this->Buffers->Add(buffer);
			if(buffer->Constant)
			{
				this->ConstantBuffer->Add(pos);
			}
			else if(buffer->ReadWrite)
			{
				this->RandomAccessBuffer->Add(pos);
			}
			else
			{
				this->ResourceBuffer->Add(pos);
			}
		}

		void ComputeShader::RemoveAllBuffers()
		{
			this->RandomAccessBuffer->Clear();
			this->ResourceBuffer->Clear();
			this->ConstantBuffer->Clear();
			this->Buffers->Clear();
		}

		ComputeShader::~ComputeShader()
		{
			if(this->ShaderCode)
			{
				((ID3D11ComputeShader*)this->ShaderCode)->Release();
				this->ShaderCode = NULL;
			}
			this->RemoveAllBuffers();
			this->RandomAccessBuffer = nullptr;
			this->ResourceBuffer = nullptr;
			this->ConstantBuffer = nullptr;
			this->Buffers = nullptr;
		}
	}
}