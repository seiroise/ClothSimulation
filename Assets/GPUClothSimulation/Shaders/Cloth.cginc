#ifndef __INCLUDE_CLOTH__
#define __INCLUDE_CLOTH__

struct ClothPoint
{
	float3 position;
	float3 prevPosition;
	float weight;
	// int constraintCount;
};

struct ClothConstraint
{
	int aIdx;
	int bIdx;
	float len;
	float3 typeWeight;
};

#endif