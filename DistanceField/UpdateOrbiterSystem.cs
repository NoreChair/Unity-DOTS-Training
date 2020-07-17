// #define _PARALLEL_JOB

using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

[DisableAutoCreation]
public class UpdateOrbiterSystem : SystemBase {
    int lastBatchCount = 0;
    EntityQuery query = default;
    Matrix4x4[][] matrix4s = null;

    public int perBatchInstanceCount = 512;
    public float jitter = 0.001f;
    public float attraction = 0.003f;
    public DistanceFieldModel model = DistanceFieldModel.SpherePlane;
    public Mesh renderMesh = null;
    public Material material = null;

    protected override void OnCreate() {
        EntityQueryDesc desc = new EntityQueryDesc() {
            All = new ComponentType[]{
                typeof(ECS_Orbiter),
                typeof(ECS_LocalToWorld)
            }
        };
        query = GetEntityQuery(desc);
    }

    protected override void OnUpdate() {
        var updateOrbJob = new UpdateOrbiterJob() {
            time = UnityEngine.Time.time * 0.1f,
            jitter = jitter,
            attraction = attraction,
            model = model,
            orbiterType = GetArchetypeChunkComponentType<ECS_Orbiter>(false)
        };
        Dependency = updateOrbJob.Schedule(query, Dependency);

#if _PARALLEL_JOB
        var orbiters = query.ToComponentDataArray<ECS_Orbiter>(Allocator.TempJob);
        var matrices = query.ToComponentDataArray<ECS_LocalToWorld>(Allocator.TempJob);
        var asyncTransform = new AsyncOrbiterTransformJob() {
            orbiters = orbiters,
            matrices = matrices
        };
        Dependency = asyncTransform.Schedule(orbiters.Length, 32, Dependency);
#else
        var asyncTransform = new AsyncOrbiterTransformJob() {
            orbiterType = GetArchetypeChunkComponentType<ECS_Orbiter>(true),
            matrixType = GetArchetypeChunkComponentType<ECS_LocalToWorld>(false)
        };
        Dependency = asyncTransform.Schedule(query, Dependency);

        var orbiters = query.ToComponentDataArray<ECS_Orbiter>(Allocator.TempJob);
        var matrices = query.ToComponentDataArray<ECS_LocalToWorld>(Allocator.TempJob);
#endif

        if (matrix4s == null) {
            matrix4s = new Matrix4x4[matrices.Length / perBatchInstanceCount + 1][];
            for (int i = 0; i < matrix4s.Length; ++i) {
                matrix4s[i] = new Matrix4x4[perBatchInstanceCount];
            }
            lastBatchCount = matrices.Length - (matrix4s.Length - 1) * perBatchInstanceCount;
        }

        Dependency.Complete();

        unsafe {
            void* src = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(matrices);
            for (int i = 0; i < matrix4s.Length; ++i) {
                fixed (void* dest = &matrix4s[i][0]) {
                    UnsafeUtility.MemCpy(dest, src, sizeof(float4x4) * perBatchInstanceCount);
                    src = (float*)src + 16 * perBatchInstanceCount;
                    var ptr = new System.IntPtr(src);
                }
            }
        }

        matrices.Dispose();
        orbiters.Dispose();

        for (int i = 0; i < matrix4s.Length; ++i) {
            if (i == matrix4s.Length - 1) {
                Graphics.DrawMeshInstanced(renderMesh, 0, material, matrix4s[i], lastBatchCount);
            } else {
                Graphics.DrawMeshInstanced(renderMesh, 0, material, matrix4s[i], perBatchInstanceCount);
            }
        }
    }

    protected override void OnDestroy() {

    }
}

[Unity.Burst.BurstCompile]
public struct UpdateOrbiterJob : IJobChunk {
    public float time;
    public float attraction;
    public float jitter;
    public DistanceFieldModel model;
    public ArchetypeChunkComponentType<ECS_Orbiter> orbiterType;

    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        Unity.Mathematics.Random random = new Unity.Mathematics.Random();
        random.InitState();

        var orbiters = chunk.GetNativeArray(orbiterType);
        for (int i = 0; i < orbiters.Length; i++) {
            float3 randomSphere = random.NextFloat3(0, 1);
            randomSphere = math.normalizesafe(randomSphere);

            var orbiter = orbiters[i];
            float3 normal;
            float dist = GetDistance(time, orbiter.position, out normal);
            orbiter.velocity -= normal * attraction * Mathf.Clamp(dist, -1f, 1f);
            orbiter.velocity += randomSphere * jitter;
            orbiter.velocity *= .99f;
            orbiter.position += orbiter.velocity;
            orbiters[i] = orbiter;
        }
    }

    static float SmoothMin(float a, float b, float radius) {
        float e = Mathf.Max(radius - Mathf.Abs(a - b), 0);
        return Mathf.Min(a, b) - e * e * 0.25f / radius;
    }

    static float Sphere(float x, float y, float z, float radius) {
        return Mathf.Sqrt(x * x + y * y + z * z) - radius;
    }

    public float GetDistance(float time, float3 position, out float3 normal) {
        float x = position.x;
        float y = position.y;
        float z = position.z;
        float distance = float.MaxValue;

        normal = float3.zero;
        if (model == DistanceFieldModel.Metaballs) {
            for (int i = 0; i < 5; i++) {
                float orbitRadius = i * .5f + 2f;
                float angle1 = time * 4f * (1f + i * .1f);
                float angle2 = time * 4f * (1.2f + i * .117f);
                float angle3 = time * 4f * (1.3f + i * .1618f);
                float cx = math.cos(angle1) * orbitRadius;
                float cy = math.sin(angle2) * orbitRadius;
                float cz = math.sin(angle3) * orbitRadius;

                float newDist = SmoothMin(distance, Sphere(x - cx, y - cy, z - cz, 2f), 2f);
                if (newDist < distance) {
                    normal = new float3(x - cx, y - cy, z - cz);
                    distance = newDist;
                }
            }
        } else if (model == DistanceFieldModel.SpinMixer) {
            for (int i = 0; i < 6; i++) {
                float orbitRadius = (i / 2 + 2) * 2;
                float angle = time * 20f * (1f + i * .1f);
                float cx = math.cos(angle) * orbitRadius;
                float cy = math.sin(angle);
                float cz = math.sin(angle) * orbitRadius;

                float newDist = Sphere(x - cx, y - cy, z - cz, 2f);
                if (newDist < distance) {
                    normal = new float3(x - cx, y - cy, z - cz);
                    distance = newDist;
                }
            }
        } else if (model == DistanceFieldModel.SpherePlane) {
            float sphereDist = Sphere(x, y, z, 5f);
            float3 sphereNormal = math.normalize(new float3(x, y, z));

            float planeDist = y;
            float3 planeNormal = new float3(0f, 1f, 0f);

            float t = math.sin(time * 8f) * .4f + .4f;
            distance = math.lerp(sphereDist, planeDist, t);
            normal = math.lerp(sphereNormal, planeNormal, t);
        } else if (model == DistanceFieldModel.SphereField) {
            float spacing = 5f + math.sin(time * 5f) * 2f;
            x += spacing * .5f;
            y += spacing * .5f;
            z += spacing * .5f;
            x -= math.floor(x / spacing) * spacing;
            y -= math.floor(y / spacing) * spacing;
            z -= math.floor(z / spacing) * spacing;
            x -= spacing * .5f;
            y -= spacing * .5f;
            z -= spacing * .5f;
            distance = Sphere(x, y, z, 5f);
            normal = new float3(x, y, z);
        } else if (model == DistanceFieldModel.FigureEight) {
            float ringRadius = 4f;
            float flipper = 1f;
            if (z < 0f) {
                z = -z;
                flipper = -1f;
            }
            float3 point = math.normalize(new float3(x, 0f, z - ringRadius)) * ringRadius;
            float angle = math.atan2(point.z, point.x) + time * 8f;
            point += (float3)Vector3.forward * ringRadius;
            normal = new float3(x - point.x, y - point.y, (z - point.z) * flipper);
            float wave = math.cos(angle * flipper * 3f) * .5f + .5f;
            wave *= wave * .5f;
            distance = math.sqrt(normal.x * normal.x + normal.y * normal.y + normal.z * normal.z) - (.5f + wave);
        } else if (model == DistanceFieldModel.PerlinNoise) {
            float perlin = Mathf.PerlinNoise(x * .2f, z * .2f);
            distance = y - perlin * 6f;
            normal = Vector3.up;
        }

        normal = math.normalize(normal);
        return distance;
    }
}

#if _PARALLEL_JOB
[Unity.Burst.BurstCompile]
public struct AsyncOrbiterTransformJob : IJobParallelFor {
    [ReadOnly] public NativeArray<ECS_Orbiter> orbiters;
    public NativeArray<ECS_LocalToWorld> matrices;

    public void Execute(int index) {
        var orbiter = orbiters[index];
        var trs = matrices[index];
        var forward = math.normalizesafe(orbiter.velocity);
        var scale = new float3(.1f, .01f, Mathf.Max(.2f, math.length(orbiter.velocity) * 5));
        trs.matrix = float4x4.TRS(orbiter.position, quaternion.LookRotationSafe(forward, float3.up), scale);
        matrices[index] = trs;
    }
}
#else
[Unity.Burst.BurstCompile]
public struct AsyncOrbiterTransformJob : IJobChunk {
    [ReadOnly] public ArchetypeChunkComponentType<ECS_Orbiter> orbiterType;
    public ArchetypeChunkComponentType<ECS_LocalToWorld> matrixType;

    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        var orbiters = chunk.GetNativeArray(orbiterType);
        var matrices = chunk.GetNativeArray(matrixType);
        for (int i = 0; i < orbiters.Length; ++i) {
            var orbiter = orbiters[i];
            var trs = matrices[i];
            var forward = math.normalizesafe(orbiter.velocity);
            var scale = new float3(.1f, .01f, Mathf.Max(.2f, math.length(orbiter.velocity) * 5));
            trs.matrix = float4x4.TRS(orbiter.position, quaternion.LookRotationSafe(forward, Vector3.up), scale);
            matrices[i] = trs;
        }
    }
}
#endif