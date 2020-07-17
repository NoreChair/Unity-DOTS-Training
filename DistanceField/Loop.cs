using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class Loop : MonoBehaviour {
    World gameWorld = null;
    CreateOrbiterSystem spawnSystem = null;
    UpdateOrbiterSystem updateSystem = null;


    public int perBatchInstanceCount = 512;
    public int orbiterCount = 4000;
    public float jitter = 0.001f;
    public float attraction = 0.003f;
    public float speed = 5.0f;
    public DistanceFieldModel model = DistanceFieldModel.SpherePlane;
    public Mesh renderMesh = null;
    public Material material = null;

    void Start() {
        gameWorld = World.DefaultGameObjectInjectionWorld;
        spawnSystem = gameWorld.CreateSystem<CreateOrbiterSystem>();
        updateSystem = gameWorld.CreateSystem<UpdateOrbiterSystem>();

        updateSystem.perBatchInstanceCount = perBatchInstanceCount;
        updateSystem.renderMesh = renderMesh;
        updateSystem.material = material;
        spawnSystem.orbiterCount = orbiterCount;
        spawnSystem.Update();
    }

    void Update() {
        model = (DistanceFieldModel)(Time.time * 0.1f % (int)DistanceFieldModel.ModelCount);

        updateSystem.model = model;
        updateSystem.jitter = jitter;
        updateSystem.attraction = attraction;
        updateSystem.Update();
    }

    void OnDestroy() {
    }
}
