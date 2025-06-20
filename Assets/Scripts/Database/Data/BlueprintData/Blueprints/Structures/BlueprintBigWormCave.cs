using UnityEngine;
#if false
[CreateAssetMenu(fileName = "Blueprint_BigWormCave",
                 menuName = "Game/World/Blueprint/Structure/BigWormCave Blueprint")]
public class BlueprintBigWormCave : BlueprintStructure
{
    /* ─────────────────────────  MAIN WORM TUNNEL  ───────────────────────── */
    [Header("Main Worm Tunnel")]
    public int   wormTunnelLength = 50;
    public int   tunnelRadius     = 3;
    public float startAngleDeg    = 0f;
    public float maxTurnAngle     = 20f;
    [Range(0f,1f)] public float turnChance = 0.3f;

    [Header("Cave Tile")]
    public TileData caveAirTile;
    public bool allowStartInAir = true;

    [Header("Radius Decrease")]
    public bool radiusDecreases = false;

    /* ───────────────────────────── SIDE TUNNELS ─────────────────────────── */
    [Header("Side Tunnels")]
    public bool  spawnSideTunnels   = false;
    [Range(0f,1f)] public float sideTunnelChance = 0.05f;
    public int  sideTunnelMaxLength = 10;
    public int  sideTunnelRadius    = 2;

    /* ───────────────────────────── END ROOMS ────────────────────────────── */
    [Header("End Rooms (Circular)")]
    [Tooltip("Chance to carve a circular room at the END of the MAIN tunnel.")]
    [Range(0f,1f)] public float endRoomChance = 0.25f;

    [Tooltip("Chance to carve a circular room at the END of a SIDE tunnel.")]
    [Range(0f,1f)] public float sideEndRoomChance = 0.25f;

    [Tooltip("Radius (in tiles) of every end-room.")]
    public int endRoomRadius = 6;

    [Tooltip("Chest (loot) tile placed at a room’s centre. Leave null for none.")]
    public TileData chestTile;

    /* ──────────────────────────── MAIN ENTRY ────────────────────────────── */
    public override void PlaceStructure(World world, int anchorX, int anchorY)
    {
        if (!caveAirTile)
        {
            Debug.LogWarning("BigWormCave: 'caveAirTile' missing!");
            return;
        }

        // Abort if start tile is air and that isn’t allowed
        if (!allowStartInAir && world.GetTileID(anchorX, anchorY) <= 0)
            return;

        float  angleRad = startAngleDeg * Mathf.Deg2Rad;
        Vector2 cur     = new(anchorX, anchorY);

        for (int step = 0; step < wormTunnelLength; step++)
        {
            // 1) carve main tunnel (shrinking radius optional)
            CarveCircle(world, cur, ComputeCurrentRadius(step));

            // 2) maybe spawn a side tunnel
            if (spawnSideTunnels && Random.value < sideTunnelChance)
                CarveSideTunnel(world, cur,
                                Random.Range(1, sideTunnelMaxLength + 1));

            // 3) random heading change
            if (Random.value < turnChance)
                angleRad += Random.Range(-maxTurnAngle, maxTurnAngle) * Mathf.Deg2Rad;

            // 4) advance
            cur.x += Mathf.Cos(angleRad);
            cur.y += Mathf.Sin(angleRad);
        }

        // 5) optional room at main‐tunnel end
        MaybeCarveEndRoom(world, cur, endRoomChance);
    }

    /* ────────────────────── SIDE TUNNEL CARVER ──────────────────────────── */
    void CarveSideTunnel(World world, Vector2 start, int length)
    {
        float   angle = Random.Range(0f, 2f * Mathf.PI);
        Vector2 p     = start;

        for (int i = 0; i < length; i++)
        {
            CarveCircle(world, p, sideTunnelRadius);

            if (Random.value < 0.5f)
                angle += Random.Range(-maxTurnAngle, maxTurnAngle) * Mathf.Deg2Rad;

            p.x += Mathf.Cos(angle);
            p.y += Mathf.Sin(angle);
        }

        // optional end-room for this side tunnel
        MaybeCarveEndRoom(world, p, sideEndRoomChance);
    }

    void MaybeCarveEndRoom(World world, Vector2 centre, float chance)
    {
        if (Random.value >= chance) return;

        CarveCircle(world, centre, endRoomRadius);

        if (chestTile)
            world.SetTileID(Mathf.RoundToInt(centre.x),
                            Mathf.RoundToInt(centre.y),
                            chestTile.tileID, false);
    }

    /* ──────────────────  RADIUS HELPER (SHRINK)  ─────────────────────────── */
    int ComputeCurrentRadius(int step)
    {
        if (!radiusDecreases) return tunnelRadius;

        float t = step / (float)wormTunnelLength;
        return Mathf.Max(1,
            Mathf.FloorToInt(Mathf.Lerp(tunnelRadius, 1, t)));
    }

    /* ────────────────────  CIRCLE CARVER  ───────────────────────────────── */
    void CarveCircle(World world, Vector2 centre, int radius)
    {
        int minX = Mathf.FloorToInt(centre.x - radius);
        int maxX = Mathf.FloorToInt(centre.x + radius);
        int minY = Mathf.FloorToInt(centre.y - radius);
        int maxY = Mathf.FloorToInt(centre.y + radius);

        float r2 = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            float dy2 = (y - centre.y) * (y - centre.y);
            for (int x = minX; x <= maxX; x++)
            {
                float dx2 = (x - centre.x) * (x - centre.x);
                if (dx2 + dy2 <= r2)
                    world.SetTileID(x, y, caveAirTile.tileID, false);
            }
        }
    }
}
#endif