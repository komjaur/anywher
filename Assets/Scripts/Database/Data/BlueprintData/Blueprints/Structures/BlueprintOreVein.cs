using UnityEngine;
#if false
[CreateAssetMenu(
        fileName = "Blueprint_OreVein",
        menuName = "Game/World/Blueprint/Structure/Ore Vein Blueprint")]
public class BlueprintOreVein : BlueprintStructure
{
    /* ────────────────────────────────────────────────────────────
       1 ▸ Ore choice (depth‑gated, weighted once per vein)
       ----------------------------------------------------------------*/
    [System.Serializable]
    public struct PossibleOre
    {
        public TileData oreTile;                 // required
        [Range(0.01f, 10f)] public float weight; // weight if depth fits
        [Range(0f,1f)] public float minDepthNorm;
        [Range(0f,1f)] public float maxDepthNorm;
    }

    [Header("Possible Ores (≥1)")]
    public PossibleOre[] possibleOres;

    /* ────────────────────────────────────────────────────────────
       2 ▸ Vein‑shape presets (weighted pick)
       ----------------------------------------------------------------*/
    [System.Serializable]
    public struct VeinShape
    {
        [Header("Main path length (random in range)")]
        public int   minVeinLength;
        public int   maxVeinLength;

        [Header("Main path size & turn")]
        public int   veinRadius;
        public bool  radiusTapers;
        [Tooltip("Start direction in degrees (0 = +X, 90 = +Y)")]
        public float startAngleDeg;
        public float maxTurnAngle;
        [Range(0f,1f)] public float turnChance;

        [Header("Branching")]
        public bool  spawnBranches;
        [Range(0f,1f)] public float branchChance;
        public int   branchMaxLength;
        public int   branchRadius;

        [Header("Fill‑rate per tile (0 = sparse, 1 = solid)")]
        [Range(0f,1f)] public float fillChance;

        [Header("Selection weight")]
        [Range(0.01f,10f)] public float weight;
    }

    [Header("Possible Vein Shapes (≥1)")]
    public VeinShape[] possibleShapes;

    /* ================================================================== */
    public override void PlaceStructure(World world, int anchorX, int anchorY)
    {
        if (possibleOres == null || possibleOres.Length == 0)
        {
            Debug.LogWarning("OreVein blueprint needs possibleOres defined!");
            return;
        }
        if (possibleShapes == null || possibleShapes.Length == 0)
        {
            Debug.LogWarning("OreVein blueprint needs possibleShapes defined!");
            return;
        }

        int worldTilesY = world.heightInChunks * world.chunkSize;
        float startDepthNorm = Mathf.Clamp01(anchorY / (float)worldTilesY);

        // 1️⃣ choose ore suitable to depth
        TileData chosenOre = ChooseOreForDepth(startDepthNorm);
        if (chosenOre == null) { Debug.Log("No suitable ore – vein skipped."); return; }

        // 2️⃣ choose vein shape by weighted random
        VeinShape shape = ChooseShape();

        // 3️⃣ decide actual length within [min,max]
        int actualLength = Random.Range(shape.minVeinLength, shape.maxVeinLength + 1);
        if (actualLength <= 0) return;

        float fillChance = Mathf.Clamp01(shape.fillChance);

        float angleRad = shape.startAngleDeg * Mathf.Deg2Rad;
        Vector2 pos    = new(anchorX, anchorY);

        for (int step = 0; step < actualLength; step++)
        {
            int radius = shape.radiusTapers ?
                          Mathf.Max(1, Mathf.FloorToInt(Mathf.Lerp(shape.veinRadius, 1, step/(float)actualLength))) :
                          shape.veinRadius;

            CarveCircle(world, pos, radius, chosenOre, fillChance);

            if (shape.spawnBranches && Random.value < shape.branchChance)
                CarveBranch(world, pos,
                            Random.Range(3, shape.branchMaxLength + 1),
                            shape.branchRadius,
                            shape.maxTurnAngle,
                            chosenOre,
                            fillChance);

            if (Random.value < shape.turnChance)
                angleRad += Random.Range(-shape.maxTurnAngle, shape.maxTurnAngle) * Mathf.Deg2Rad;

            pos.x += Mathf.Cos(angleRad);
            pos.y += Mathf.Sin(angleRad);
        }
    }

    /* ================================================================== */
    /* helpers */
    /* ================================================================== */

    TileData ChooseOreForDepth(float depthNorm)
    {
        float total = 0f;
        foreach (var o in possibleOres)
            if (depthNorm >= o.minDepthNorm && depthNorm <= o.maxDepthNorm && o.oreTile)
                total += Mathf.Max(0.01f, o.weight);
        if (total == 0f) return null;

        float roll = Random.value * total;
        foreach (var o in possibleOres)
        {
            if (depthNorm < o.minDepthNorm || depthNorm > o.maxDepthNorm || !o.oreTile) continue;
            roll -= Mathf.Max(0.01f, o.weight);
            if (roll <= 0f) return o.oreTile;
        }
        return null;
    }

    VeinShape ChooseShape()
    {
        float total = 0f;
        foreach (var s in possibleShapes) total += Mathf.Max(0.01f, s.weight);
        float roll = Random.value * total;
        foreach (var s in possibleShapes)
        {
            roll -= Mathf.Max(0.01f, s.weight);
            if (roll <= 0f) return s;
        }
        return possibleShapes[^1];
    }

    // ------------------------------------------------------------------
    //  carving helpers (skip Air & Liquid) + per‑tile fill chance
    // ------------------------------------------------------------------
    void CarveCircle(World world, Vector2 center, int radius, TileData ore, float fillChance)
    {
        int minX = Mathf.FloorToInt(center.x - radius);
        int maxX = Mathf.FloorToInt(center.x + radius);
        int minY = Mathf.FloorToInt(center.y - radius);
        int maxY = Mathf.FloorToInt(center.y + radius);

        for (int y = minY; y <= maxY; y++)
        {
            float dy = y - center.y;
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - center.x;
                if (dx * dx + dy * dy > radius * radius) continue;
                if (Random.value > fillChance) continue; // sparse fill

                int existingID = world.GetTileID(x, y);
                if (existingID <= 0) continue; // out‑of‑bounds/void

                TileData existing = world.tiles.GetTileDataByID(existingID);
                if (existing == null) continue;
                if (existing.behavior == BlockBehavior.Air || existing.behavior == BlockBehavior.Liquid)
                    continue;                                  // leave liquids and air untouched

                world.SetTileID(x, y, ore.tileID, false);
            }
        }
    }

    void CarveBranch(World world, Vector2 start, int length, int radius, float maxTurn, TileData ore, float fillChance)
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 pos = start;

        for (int i = 0; i < length; i++)
        {
            CarveCircle(world, pos, radius, ore, fillChance);

            if (Random.value < 0.5f)
                angle += Random.Range(-maxTurn, maxTurn) * Mathf.Deg2Rad;

            pos.x += Mathf.Cos(angle);
            pos.y += Mathf.Sin(angle);
        }
    }
}
#endif