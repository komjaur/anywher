using UnityEngine;
using System.Diagnostics;

public class MapCreator : MonoBehaviour
{
    /*
    // Reference to your WorldData ScriptableObject
    public WorldData worldData;

    public MapPreviewer previewer;

    private System.Random prng;
    private Vector2[] centers;
    private int[] centerAreaIndices;
    private byte[,] canvas;

    void Update()
    {
        if (!Application.isPlaying) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateCanvas();
        }
        if (Input.GetKeyDown(KeyCode.RightShift))
        {
            previewer.RenderCanvas(canvas);
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        GenerateCanvas();
        previewer.RenderCanvas(canvas);
    }

    public void GenerateCanvas()
    {
        if (worldData == null)
        {
            UnityEngine.Debug.LogError("MapCreator: No WorldData assigned.");
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        // Use the seed from WorldData
        prng = new System.Random(worldData.seed);

        // Retrieve area/Voronoi settings from WorldData
        Vector2Int smallSize = worldData.areaGenerationSize;
        int areaPointsCount = worldData.areaPointsCount;
        float noiseScale = worldData.areaEdgeNoiseScale;
        float noiseIntensity = worldData.areaEdgeNoiseIntensity;

        // Overworld areas for now
        AreaOverworldData[] overworldAreas = worldData.overworldAreas;

        // Final canvas dimensions from worldData
        int finalWidth = worldData.worldWidth;
        int finalHeight = worldData.worldHeight;

        // Prepare small Voronoi canvas
        byte[,] smallCanvas = new byte[smallSize.x, smallSize.y];

        centers = new Vector2[areaPointsCount];
        centerAreaIndices = new int[areaPointsCount];

        // Place Voronoi center points
        for (int i = 0; i < areaPointsCount; i++)
        {
            // Randomly select one of the overworld areas
            int areaIndex = prng.Next(overworldAreas.Length);
            AreaOverworldData chosenArea = overworldAreas[areaIndex];

            // Determine Y based on [minDepth, maxDepth] mapped to smallSize.y
            float yMin = chosenArea.minDepth * smallSize.y;
            float yMax = chosenArea.maxDepth * smallSize.y;
            float randomY = Mathf.Lerp(yMin, yMax, (float)prng.NextDouble());

            // X can be anywhere in [0, smallSize.x]
            float randomX = (float)prng.NextDouble() * smallSize.x;

            centers[i] = new Vector2(randomX, randomY);
            centerAreaIndices[i] = areaIndex;
        }

        // Generate small Voronoi
        for (int y = 0; y < smallSize.y; y++)
        {
            for (int x = 0; x < smallSize.x; x++)
            {
                float bestDist = float.MaxValue;
                int bestIndex = 0;
                for (int i = 0; i < areaPointsCount; i++)
                {
                    float dx = x - centers[i].x;
                    float dy = y - centers[i].y;
                    float dist = dx * dx + dy * dy;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIndex = i;
                    }
                }

                int areaIdx = centerAreaIndices[bestIndex];
                float val = overworldAreas[areaIdx].color.grayscale * 255f;
                smallCanvas[x, y] = (byte)Mathf.Clamp(val, 0f, 255f);
            }
        }

        // Prepare final canvas
        canvas = new byte[finalWidth, finalHeight];

        float scaleX = (float)smallSize.x / finalWidth;
        float scaleY = (float)smallSize.y / finalHeight;

        // Transfer small Voronoi to the final canvas, applying Perlin-based shift
        for (int y = 0; y < finalHeight; y++)
        {
            for (int x = 0; x < finalWidth; x++)
            {
                float noiseVal = Mathf.PerlinNoise(x * noiseScale, y * noiseScale);
                float shift = (noiseVal - 0.5f) * noiseIntensity;

                float shiftedX = x + shift;
                float shiftedY = y + shift;

                int sourceX = Mathf.FloorToInt(shiftedX * scaleX);
                int sourceY = Mathf.FloorToInt(shiftedY * scaleY);

                sourceX = Mathf.Clamp(sourceX, 0, smallSize.x - 1);
                sourceY = Mathf.Clamp(sourceY, 0, smallSize.y - 1);

                canvas[x, y] = smallCanvas[sourceX, sourceY];
            }
        }

        stopwatch.Stop();
        float sizeKB = (finalWidth * finalHeight) / 1024f;
        int totalValues = finalWidth * finalHeight;
        string sizeText = sizeKB < 1024f
            ? $"{sizeKB:F2} KB"
            : $"{(sizeKB / 1024f):F2} MB";

        UnityEngine.Debug.Log($"Canvas generated in: {stopwatch.ElapsedMilliseconds} ms | Size: {sizeText} | Values: {totalValues}");
    }*/
}
