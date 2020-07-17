using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;

[DisableAutoCreation]
public class CreateOrbiterSystem : SystemBase {
    public int orbiterCount = 4000;
    EntityArchetype archetype;
    NativeArray<Entity> entities;

    protected override void OnCreate() {
        archetype = EntityManager.CreateArchetype(new ComponentType[]{
            typeof(ECS_Orbiter),
            typeof(ECS_LocalToWorld)
        });
    }

    protected override void OnUpdate() {
        if (entities.IsCreated) {
            EntityManager.DestroyEntity(entities);
            entities.Dispose();
        }

        entities = EntityManager.CreateEntity(archetype, orbiterCount, Allocator.Persistent);
        var cfe = GetComponentDataFromEntity<ECS_Orbiter>();
        for (int i = 0; i < entities.Length; ++i) {
            var orbiter = cfe[entities[i]];
            orbiter.position = UnityEngine.Random.insideUnitSphere * 50.0f;
            orbiter.velocity = float3.zero;
            orbiter.color = UnityEngine.Vector4.one;
            EntityManager.SetComponentData(entities[i], orbiter);
        }
    }

    protected override void OnDestroy() {
        EntityManager.DestroyEntity(entities);
        entities.Dispose();
    }
}


