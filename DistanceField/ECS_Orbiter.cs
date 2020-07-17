using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public struct ECS_Orbiter : IComponentData {
    public float3 position;
    public float3 velocity;
    public float4 color;
}

public struct ECS_LocalToWorld : IComponentData {
    public float4x4 matrix;
}