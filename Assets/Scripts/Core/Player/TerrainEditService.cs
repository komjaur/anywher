using System.Collections.Generic;
using UnityEngine;

/* ───────────────────────────────────────────────
   Lightweight stubs (shared across project)
────────────────────────────────────────────────── */

public struct ToolStats
{
    public int power;
    public static readonly ToolStats Infinite = new() { power = int.MaxValue };
}

public enum EditFail
{
    None, OutOfBounds, Protected, Unsupported, LiquidFilled,
    AlreadySolid, AlreadyEmpty, ToolTooWeak
}

public readonly struct EditResult
{
    public readonly bool     success;
    public readonly EditFail failReason;
    public readonly int      oldTile;
    public readonly int      newTile;
    public readonly ItemData drop;

    public EditResult(bool ok, EditFail r, int oldId, int newId, ItemData d)
    {
        success    = ok;
        failReason = r;
        oldTile    = oldId;
        newTile    = newId;
        drop       = d;
    }
}

/* ───────────────────────────────────────────────
   TerrainEditService – Terraria-style world edits
────────────────────────────────────────────────── */

public sealed class TerrainEditService
{
    readonly World              world;
    readonly WorldTilemapViewer viewer;
    readonly TileDatabase       db;

    readonly Stack<Vector2Int> floodStack = new();   // re-used, no GC
   /* -----------------------------------------------------------------
     *  Mining HP tracking  (key = world-tile coordinates)
     * ----------------------------------------------------------------- */
    readonly Dictionary<Vector2Int,int> hpLeft = new(128);
        readonly Sprite[] crackStages;               // ⭠  frames from GameData

    /*  The overlay caller (UI) can query the current progress percentage
        via GetMiningProgress(wx,wy).  If you don’t use crack sprites you
        can ignore it.                                                   */

    /// <summary>0-1 progress for the tile currently being chipped.</summary>
    public float GetMiningProgress(int wx,int wy)
    {
        var key = new Vector2Int(wx, wy);
        if (!hpLeft.TryGetValue(key, out int hpRemain)) return 0f;

        int maxHp = db.GetTileDataByID(world.GetTileID(wx, wy))?.health ?? 100;
        return Mathf.Clamp01(1f - (float)hpRemain / maxHp);
    }


    public TerrainEditService(World w, WorldTilemapViewer v)
    {
        world = w;
        viewer = v;
        db = w.tiles;
                crackStages = GameManager.Instance?.GameData?.crackStages;

    }

     public EditResult TryMineTile(int wx, int wy,
                                  bool bg        = false,
                                  int  toolPower = int.MaxValue,
                                  int  toolDmg   = 1)
    {
        if (!Inside(wx, wy))
            return Fail(wx, wy, bg, EditFail.OutOfBounds);

        ChunkLayer layer = bg ? ChunkLayer.Background : ChunkLayer.Front;
        int oldId = world.GetTileID(wx, wy, layer);

        if (bg && !IsAir(world.GetTileID(wx, wy, ChunkLayer.Front)))
            return Fail(wx, wy, bg, EditFail.Unsupported);
        if (IsAir(oldId))
            return Fail(wx, wy, bg, EditFail.AlreadyEmpty);

        TileData td = db.GetTileDataByID(oldId);
        if (td == null)                       return Fail(wx, wy, bg, EditFail.AlreadyEmpty);
        if (td.unbreakable)                   return Fail(wx, wy, bg, EditFail.Protected);
        if (td.requiredToolPower > toolPower) return Fail(wx, wy, bg, EditFail.ToolTooWeak);

        /* ─── smart trunk lock (Terraria rule) ───
           Refuse mining a block if it is not itself a tree
           and it directly supports a tree trunk tile         */
        if (!bg && td.tag != BlockTag.Tree && Inside(wx, wy + 1))
        {
            int aboveId  = world.GetTileID(wx, wy + 1, ChunkLayer.Front);
            TileData aTd = db.GetTileDataByID(aboveId);
            if (aTd != null && aTd.tag == BlockTag.Tree)
                return Fail(wx, wy, bg, EditFail.Unsupported);
        }

        /* ─── HP bookkeeping & crack overlay ─── */
        var key = new Vector2Int(wx, wy);
        if (!hpLeft.TryGetValue(key, out int hp))
            hp = td.health;

        hp -= Mathf.Max(1, toolDmg);
        hpLeft[key] = hp;

        float prog = 1f - (float)hp / td.health;
        viewer.SetOverlayTile(wx, wy, GetCrackSprite(prog));

        if (hp > 0)
            return new EditResult(false, EditFail.None, oldId, oldId, null);

        /* ─── block breaks ─── */
        hpLeft.Remove(key);
        viewer.SetOverlayTile(wx, wy, null);

        int newId = 0;
        if (!bg)
        {
            int bgId = world.GetTileID(wx, wy, ChunkLayer.Background);
            newId = (!IsAir(bgId) && db.UndergroundAirTile)
                    ? db.UndergroundAirTile.tileID : 0;
        }

        WriteTile(wx, wy, layer, newId);
        if (!bg)
        {
            WriteTile(wx, wy, ChunkLayer.Liquid , 0);
            WriteTile(wx, wy, ChunkLayer.Overlay, 0);

        }

        PostEffects(wx, wy, oldId, newId, td);

        if (!bg && td.tag == BlockTag.Vine)
            RemoveVineBelow(wx, wy - 1, newId);
        if (!bg && td.tag == BlockTag.Tree)
            RemoveWholeTree(wx, wy + 1, td.tileID, newId);

        if (!bg && td.tag != BlockTag.Vine && wy > 0)
        {
            int belowId = world.GetTileID(wx, wy - 1, ChunkLayer.Front);
            TileData bTd = db.GetTileDataByID(belowId);
            if (bTd && bTd.tag == BlockTag.Vine)
                RemoveVineBelow(wx, wy - 1, newId);
        }

        if (!bg && Inside(wx, wy + 1))
        {
            int aboveId = world.GetTileID(wx, wy + 1, ChunkLayer.Front);
            TileData aTd = db.GetTileDataByID(aboveId);
            if (aTd && aTd.tag == BlockTag.Foliage)
            {
                WriteTile(wx, wy + 1, ChunkLayer.Front, 0);
                PostEffects(wx, wy + 1, aboveId, 0, aTd);
            }
        }

        return Succeed(wx, wy, bg, oldId, newId, td.dropItem);
    }
    public Sprite GetCrackSprite(float progress)
    {
        if (crackStages == null || crackStages.Length == 0) return null;

        /* map 0 → - (no crack)  , 1 → last frame                  */
        int idx = Mathf.Clamp(
                     Mathf.FloorToInt(progress * crackStages.Length),
                     0, crackStages.Length - 1);

        return crackStages[idx];
    }
    /* =================================================================
     *  PLACING  (right-click) – unchanged except IsAir is now public
     * ================================================================= */
    public EditResult TryPlaceTile(int wx, int wy,
                                   int newId,
                                   bool bg = false)
    {
        if (!Inside(wx, wy))
            return Fail(wx, wy, bg, EditFail.OutOfBounds);

        ChunkLayer layer = bg ? ChunkLayer.Background : ChunkLayer.Front;
        if (!IsAir(world.GetTileID(wx, wy, layer)))
            return Fail(wx, wy, bg, EditFail.AlreadySolid);

        TileData nd = db.GetTileDataByID(newId);
        if (nd == null)
            return Fail(wx, wy, bg, EditFail.Protected);

        if (bg && nd.renderLayer != RenderGroup.Wall)
            return Fail(wx, wy, bg, EditFail.Unsupported);

        if (!bg && !HasSolidSupport(wx, wy))
            return Fail(wx, wy, bg, EditFail.Unsupported);
        if (bg && !HasWallSupport(wx, wy))
            return Fail(wx, wy, bg, EditFail.Unsupported);

        WriteTile(wx, wy, layer, newId);
        PostEffects(wx, wy, 0, newId, null);

        return Succeed(wx, wy, bg, 0, newId, null);
    }

    /* ───────────── Chain helpers (vines, trees) ───────────── */

/// downward until a non-vine tile is encountered.
void RemoveVineBelow(int wx, int startY, int airId)
{
    for (int y = startY; y >= 0; --y)
    {
        int id = world.GetTileID(wx, y, ChunkLayer.Front);

        // stop when we reach something that is not tagged as a vine
        TileData td = db.GetTileDataByID(id);
        if (td == null || td.tag != BlockTag.Vine)
            break;

        WriteTile(wx, y, ChunkLayer.Front, airId);
        PostEffects(wx, y, id, airId, td);   // honours per-segment drops / XP
    }
}

/// Removes the entire tree that the player just cut, without touching
/// other nearby trees.
///
/// • Starts at (rootX,rootY) – the mined tile.
/// • Walks upward, plus at most one tile left/right **from the trunk
///   column only**.  Never looks below the cut, so roots stay.
///
void RemoveWholeTree(int rootX, int rootY, int treeId, int airId)
{
    var sm = GameManager.Instance?.SkillManager;
    if (sm == null) return;

    int trunkX = rootX;                                   // ← lock column

    floodStack.Clear();
    floodStack.Push(new Vector2Int(rootX, rootY));

    int mapMaxY = world.heightInChunks * world.chunkSize - 1;

    while (floodStack.Count > 0)
    {
        Vector2Int p = floodStack.Pop();
        if (!Inside(p.x, p.y) || p.y > mapMaxY) continue;

        int      id = world.GetTileID(p.x, p.y, ChunkLayer.Front);
        TileData td = db.GetTileDataByID(id);
        if (td == null || td.tag != BlockTag.Tree) continue;

        WriteTile(p.x, p.y, ChunkLayer.Front, airId);
        PostEffects(p.x, p.y, id, airId, td);

        /* always go up */
        floodStack.Push(new Vector2Int(p.x, p.y + 1));

        /* go left / right only when still on the trunk column */
        if (p.x == trunkX)
        {
            floodStack.Push(new Vector2Int(p.x - 1, p.y));
            floodStack.Push(new Vector2Int(p.x + 1, p.y));
        }
    }
}


    /* ───────────── Write helper ───────────── */
    void WriteTile(int wx, int wy, ChunkLayer layer, int newId)
    {
        if (world.GetTileID(wx, wy, layer) == newId) return;
        world.SetTileID(wx, wy, newId, layer);
        viewer.ApplyTileEdit(wx, wy);
    }

    /* ───────────── Support checks & misc ───────────── */
    bool Inside(int x, int y) =>
        x >= 0 && y >= 0 &&
        x < world.widthInChunks * world.chunkSize &&
        y < world.heightInChunks * world.chunkSize;

  public bool IsAir(int tileID)
    {
        if (tileID <= 0) return true;                     // empty
        TileData td = db.GetTileDataByID(tileID);
        return td != null && td.behavior == BlockBehavior.Air;
    }

    static readonly (int dx,int dy)[] dirs4 = {(-1,0),(1,0),(0,-1),(0,1)};

 bool HasSolidSupport(int wx, int wy)
{
    /* 1 ▸ background wall directly behind the new tile */
    if (world.GetTileID(wx, wy, ChunkLayer.Background) > 0)
        return true;

    /* 2 ▸ check the four neighbours */
    foreach (var d in dirs4)
    {
        int nx = wx + d.dx;
        int ny = wy + d.dy;

        /* neighbour foreground block */
        int frontId = world.GetTileID(nx, ny, ChunkLayer.Front);
        if (!IsAir(frontId))
        {
            TileData td = db.GetTileDataByID(frontId);
            if (td != null &&
               (td.behavior == BlockBehavior.Solid ||
                td.behavior == BlockBehavior.Platform))
                return true;
        }

        /* neighbour background wall */
        if (world.GetTileID(nx, ny, ChunkLayer.Background) > 0)
            return true;
    }

    return false;   // no suitable support found
}

    bool HasWallSupport(int wx, int wy)
    {
        foreach (var d in dirs4)
        {
            if (!IsAir(world.GetTileID(wx + d.dx, wy + d.dy, ChunkLayer.Front)))
                return true;
            if (world.GetTileID(wx + d.dx, wy + d.dy, ChunkLayer.Background) > 0)
                return true;
        }
        return false;
    }

    /* ───────────── Mini-log wrappers ───────────── */
    EditResult Fail(int wx,int wy,bool bg,EditFail why)=>
        new(false,why,0,0,null);

    EditResult Succeed(int wx,int wy,bool bg,int oldId,int newId,ItemData drop)=>
        new(true,EditFail.None,oldId,newId,drop);

    /* ───────────── FX / drops / XP ───────────── */
    void PostEffects(int wx,int wy,int oldId,int newId,TileData oldData)
    {
        if (oldData?.dropItem != null)
        {
            if (ItemManager.Instance == null) ItemManager.Initialize();

            Vector2 jitter = new(Random.Range(-0.2f, 0.2f),
                                 Random.Range( 0.05f,0.30f));
            Vector2 pos = new Vector2(wx + 0.5f, wy + 0.5f) + jitter;

            var itm = ItemManager.Instance.Spawn(oldData.dropItem,pos);
            if (itm && itm.TryGetComponent(out Rigidbody2D rb))
            {
                rb.AddForce(new Vector2(Random.Range(-1.5f,1.5f),
                                        Random.Range( 2.0f,3.5f)),
                            ForceMode2D.Impulse);
            }
        }

        /* immediate XP for non-tree tiles */
        AwardInstantXp(oldData);
    }

    
    void AwardInstantXp(TileData td)
    {
        if (td == null) return;

        var sm = GameManager.Instance?.SkillManager;
        if (sm == null) return;

        SkillId skill = (td.tag == BlockTag.Vine || td.tag == BlockTag.Tree)
                        ? SkillId.Woodcutting
                        : SkillId.Mining;

        int xp = td.xpOnMine;

        sm.AddXp(skill, xp);
    }
}
