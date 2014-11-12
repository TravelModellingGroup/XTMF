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
cbuffer Parameters : register (b0)
{
	uint DataPoints;
	uint Unused;
	uint UnusedThree;
	// we still need this to be 16 byte aligned
	uint UNUSEDTwo;
};
// group size
#define groupSize 64
// Our variables
RWStructuredBuffer<float> g_data : register(u0);

groupshared float sdata[groupSize];

[numthreads(groupSize, 1, 1)]
void CSMain(uint3 threadIdx : SV_GroupThreadID,
	uint3 groupIdx : SV_GroupID)
{
	unsigned int tid = threadIdx.x;
	unsigned int i = groupIdx.x * (groupSize * 2) + threadIdx.x;
	sdata[threadIdx.x] = g_data[i] + g_data[i + groupSize] + g_data[i + groupSize * 2] + g_data[i + groupSize * 3]
		+ g_data[i + groupSize] + g_data[i + groupSize * 2] + g_data[i + groupSize * 7];
	GroupMemoryBarrierWithGroupSync();
	if (tid < 32){
		sdata[tid] += sdata[tid + 32];
		sdata[tid] += sdata[tid + 16];
		sdata[tid] += sdata[tid + 8];
		sdata[tid] += sdata[tid + 4];
		sdata[tid] += sdata[tid + 2];
		sdata[tid] += sdata[tid + 1];
	}
	if (tid == 0) g_data[groupIdx.x] = sdata[0];
}