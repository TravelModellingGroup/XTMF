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
cbuffer Parameters : register ( b0 )
{
	uint NumberOfZones;
	uint NumberOfModes;
	uint Stride;
	// we still need this to be 16 byte aligned
	uint UNUSEDTwo;
};

// group size
#define thread_group_size_x 8
#define thread_group_size_y 8
#define max_modes 16

// Our variables
RWStructuredBuffer<float> vBuffer : register(u0);

[numthreads( thread_group_size_x, thread_group_size_y, 1 )]
void CSMain( uint3 threadIDInGroup : SV_GroupThreadID, uint3 groupID : SV_GroupID,
			uint groupIndex : SV_GroupIndex,
			uint3 dispatchThreadID : SV_DispatchThreadID )
{
	uint m;
	uint i = dispatchThreadID.x;
	uint j = dispatchThreadID.y;
	uint jOffset;
	float temp [max_modes];
	if( (i < NumberOfZones) & (j < NumberOfZones) )
	{
		float total = 0;
		jOffset = (i * NumberOfZones + j) * Stride;
		[unroll(max_modes)]
		for(m = 0 ; m < NumberOfModes; m += 1 )
		{
			temp[m] = exp(vBuffer[jOffset + m]);
			total += temp[m];
		}
		if(total != 0)
		{
			total = rcp(total);
			[unroll(max_modes)]
			for(m = 0 ; m < NumberOfModes; m += 1 )
			{
				// total is the inverse of the real total
				vBuffer[jOffset + m] = temp[m] * total;
			}
		}
		else
		{
			total = rcp(NumberOfModes);
			[unroll(max_modes)]
			for(m = 0 ; m < NumberOfModes; m += 1 )
			{
				// This isn't actually total, just reusing the variable
				vBuffer[jOffset + m] = total;
			}
		}
	}
}