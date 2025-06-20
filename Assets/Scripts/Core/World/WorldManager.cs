using UnityEngine;
using System.Collections;
using System;

public class WorldManager : MonoBehaviour
{
    bool isGeneratingWorld = false;
    public event Action<World> WorldReady;
    private World currentWorld;
    private WorldData worldData;

    public World GetCurrentWorld() => currentWorld;
       public void InitializeWorld(WorldData data)
    {
        if (data == null)
        {
            Debug.LogError("WorldManager.InitializeWorld: WorldData is null!");
            return;
        }

        worldData   = data;               // keep for later inspections/saves
        currentWorld = new World(data);   // new 1-arg ctor
        currentWorld.CreateAllChunks();
      /*  TerrainService = new TerrainEditService(
                             currentWorld,
                             GameManager.Instance.WorldTilemapViewer);*/

   
        Debug.Log($"World initialized ({data.widthInChunks}×{data.heightInChunks} chunks, seed {data.seed})");
    }
    public void GenerateBiomeMap( Action onMapGenerated = null)
    {
        if (!isGeneratingWorld)
        {
            StartCoroutine(GenerateBiomeMapRoutine( onMapGenerated));
        }
        else
        {
            Debug.LogWarning("World generation already in progress. Please wait.");
        }
    }

    IEnumerator GenerateBiomeMapRoutine( Action onMapGenerated)
    {
        isGeneratingWorld = true;
        float startTime = Time.realtimeSinceStartup;

 
        yield return BiomeMapGenerator.GenerateFullMapAsync(currentWorld,worldData, map =>
        {
            currentWorld = map;
        });



        float duration = Time.realtimeSinceStartup - startTime;
        Debug.Log($"Async BIOME map generation took {duration:F2} seconds.");

        isGeneratingWorld = false;
        onMapGenerated?.Invoke();
    }

    public void GenerateWorldMap( Action onWorldCreated = null)
    {
        if (isGeneratingWorld)
        {
            Debug.LogWarning("World generation already in progress. Please wait.");
            return;
        }


        StartCoroutine(GenerateWorldMapRoutine(onWorldCreated));
    }
IEnumerator GenerateWorldMapRoutine(Action onWorldCreated)
{
    isGeneratingWorld = true;
    float startTime = Time.realtimeSinceStartup;

    // Use the existing currentWorld
    WorldMapGenerator.GenerateWorldTiles(currentWorld, worldData);

    float duration = Time.realtimeSinceStartup - startTime;
    Debug.Log($"Async WORLD generation took {duration:F2} seconds.");

    isGeneratingWorld = false;
    onWorldCreated?.Invoke();
    yield break;
}

    /// <summary>
    /// Generates (places) all Points of Interest in the current world, 
    /// by calling WorldMapPOIGenerator.PlaceAllPois.
    /// Calls onWorldCreated when done.
    /// </summary>
    public void GenerateWorldPOI(Action onWorldCreated = null)
    {
        if (currentWorld == null)
        {
            Debug.LogWarning("No current world to place POIs. Generate/Initialize the world first.");
            onWorldCreated?.Invoke();
            return;
        }

        if (isGeneratingWorld)
        {
            Debug.LogWarning("World generation or placement is already in progress. Please wait.");
            onWorldCreated?.Invoke();
            return;
        }

        StartCoroutine(GenerateWorldPOIRoutine(onWorldCreated));
    }

    private IEnumerator GenerateWorldPOIRoutine(Action onWorldCreated)
    {
        isGeneratingWorld = true;
        float startTime = Time.realtimeSinceStartup;

        // Actually place the POIs (both surface and normal)
        WorldMapPOIGenerator.PlaceAllPois(currentWorld, worldData);

        float duration = Time.realtimeSinceStartup - startTime;
        Debug.Log($"Async POI generation took {duration:F2} seconds.");

        isGeneratingWorld = false;
        onWorldCreated?.Invoke();
        yield break;
    }
    public void GenerateWorldPost(Action onWorldProcessed = null)
    {
        if (currentWorld == null)
        {
            Debug.LogWarning("No current world to post-process. Generate the world first.");
            onWorldProcessed?.Invoke();
            return;
        }

        if (isGeneratingWorld)
        {
            Debug.LogWarning("World generation or post-processing already in progress. Please wait.");
            onWorldProcessed?.Invoke();
            return;
        }

        StartCoroutine(GenerateWorldPostRoutine(onWorldProcessed));
    }

    IEnumerator GenerateWorldPostRoutine(Action onWorldProcessed)
    {
        isGeneratingWorld = true;
        float startTime = Time.realtimeSinceStartup;

        WorldPostProcessor.PostProcessWorld(currentWorld, worldData);

        float duration = Time.realtimeSinceStartup - startTime;
        Debug.Log($"Async WORLD post-process took {duration:F2} seconds.");

        isGeneratingWorld = false;
        onWorldProcessed?.Invoke();
        WorldReady?.Invoke(currentWorld);   // <── fire!
        yield break;
    }




}
