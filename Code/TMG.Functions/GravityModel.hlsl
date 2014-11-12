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
// Constants need to be 64 byte aligned
cbuffer Parameters : register ( b0 )
{
	uint Length;
	uint Stage;
	uint MaxIteration;
	uint UNUSED;
};

// group size
#define thread_group_size_x 64
#define thread_group_size_y 1

#define unrollSize 8

// Our variables
RWStructuredBuffer<float> Flows : register(u0);
RWStructuredBuffer<float> AttractionsStar  : register(u1);
RWStructuredBuffer<float> BalancedBuffer : register(u2);
StructuredBuffer<float> Productions : register(t0);
StructuredBuffer<float> Attractions : register(t1);
StructuredBuffer<float> Friction  : register(t2);

void ComputeFlow(int origin)
{
	uint k,j;
	float sumAF = 0;
	uint originOffset = origin * Length;
	uint remainder = (Length % unrollSize);
	for (k = 0; k < remainder; k++)
	{
		sumAF += (Friction[originOffset + k] * AttractionsStar[k]);
	}

	for (k = remainder; k < Length; k += unrollSize)
	{
		float temp[unrollSize];
		[unroll(unrollSize)]
		for(j = 0; j < unrollSize; j++)
		{
			temp[j] = (Friction[originOffset + k + j] * AttractionsStar[k + j]);
		}
		[unroll(unrollSize)]
		for(j = 0; j < unrollSize; j++)
		{
			sumAF += temp[j];
		}
	}
	// rcp => 1/x
	sumAF = Productions[origin] * rcp(sumAF);
	if(!isfinite(sumAF))
	{
		sumAF = 0;
	}
	[allow_uav_condition]
	for (k = 0; k < remainder; k++)
	{
		float temp = ((Friction[originOffset + k] * AttractionsStar[k]) * sumAF);
		Flows[originOffset + k] = isfinite(temp) ? temp : 0;
	}

	// Now we can go and compute the Flows now that we have the sums complete
	[allow_uav_condition]
	for (k = remainder; k < Length; k += unrollSize)
	{
		[unroll(unrollSize)]
		for(j = 0; j < unrollSize; j++)
		{
			float temp = Friction[originOffset + k + j] * sumAF * AttractionsStar[k + j];
			Flows[originOffset + k + j] = isfinite(temp) ? temp : 0;
		}
	}
}

void Balance(uint destination)
{
	uint i;
	float total = 0;
	float residule = 1;
	uint remainder = Length % unrollSize;
	for(i = 0; i < remainder ; i++)
	{
		total += Flows[Length * i + destination];
	}
	for (i = remainder; i < Length; i += 8)
	{
		total += Flows[Length * i + destination];
		total += Flows[Length * (i + 1) + destination];
		total += Flows[Length * (i + 2) + destination];
		total += Flows[Length * (i + 3) + destination];
		total += Flows[Length * (i + 4) + destination];
		total += Flows[Length * (i + 5) + destination];
		total += Flows[Length * (i + 6) + destination];
		total += Flows[Length * (i + 7) + destination];
	}
	residule = Attractions[destination] / total;
	if(isfinite(residule))
	{
		float ep = BalancedBuffer[1];
		if (abs(1 - residule) > ep)
		{
			BalancedBuffer[0] = 0;
		}
		AttractionsStar[destination] *= residule;
	}
	else
	{
		AttractionsStar[destination] = 0;
	}
}

[numthreads( thread_group_size_x, thread_group_size_y, 1 )]
void CSMain( uint3 threadIDInGroup : SV_GroupThreadID, uint3 groupID : SV_GroupID,
			uint groupIndex : SV_GroupIndex,
			uint3 dispatchThreadID : SV_DispatchThreadID )
{
	uint idx = dispatchThreadID.x;
	if(Stage == 0)
	{
		if(idx <= thread_group_size_x)
		{
			// We don't need to synchronize after this because the ComputeFlow won't use this value
			BalancedBuffer[0] = 1;
		}
		// Compute 1 iteration of the gravity model
		if(idx < Length)
		{
			ComputeFlow(idx);
		}
	}
	else
	{
		// Now that all of the flows have been computed lets go and balance them
		if(idx < Length)
		{
			Balance(idx);
		}
	}
}