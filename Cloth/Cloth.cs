using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public struct ClothVertex : IComponentData
{
    public NativeArray<int> pins;
    public NativeArray<float3> vertices;
    public NativeArray<float3> oldVertices;
}

public struct ClothBar : IComponentData
{
    public NativeArray<float> barLengths;
    public NativeArray<int2> bars;
}

public struct ClothMesh : ISharedComponentData
{
    public Mesh mesh;
}