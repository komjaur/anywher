using UnityEngine;
#if false
[CreateAssetMenu(fileName = "Blueprint_UltraAdvancedMiner",
 menuName = "Game/World/Blueprint/Structure/UltraAdvancedMiner Blueprint")]
public class BlueprintUltraAdvancedMiner : BlueprintStructure
{
#region ── Inspector ───────────────────────────────────────────────────────────
[Header("General Miner Settings")]
public int  maxDiggerMoves = 150;
public bool stopIfOutOfBounds = true;

[Header("Tunnel Dimensions & Tiles")]
public TileData airTile;      // mandatory
public TileData torchTile;    // optional
public TileData chestTile;    // optional

[Header("Movement Chances")]
[Range(0f,1f)] public float randomDownChance      = 0.10f;
[Range(0f,1f)] public float randomUpChance        = 0.05f;
[Range(0f,1f)] public float directionChangeChance = 0.05f;

[Header("Side Tunnels")]
[Range(0f,1f)] public float sideTunnelChance = 0.02f;
public Vector2Int sideTunnelLengthRange = new(4,8);

[Header("Other Features")]
public int  torchSpacing = 5;
[Range(0f,1f)] public float chestChance = 0.02f;
public bool allowSteppingUp = true;

[Header("Secret Rooms")]
[Range(0f,1f)] public float secretRoomChance = 0.01f;
public Vector2Int secretRoomOffsetRangeH = new(2,4);   // horizontal pad
public Vector2Int secretRoomOffsetRangeV = new(2,4);   // vertical pad
public Vector2Int secretRoomSizeMin      = new(4,3);
public Vector2Int secretRoomSizeMax      = new(7,5);
#endregion

enum DirX { Left=-1, Right=1 }
enum DirY { Down=-1, Up=1 }

Vector2Int pos;
DirX dirX;
int  steps;
int  pendingSecretH = -1;   // –1 means none
int  pendingSecretV = 0;    //  0 only decided when scheduled

// ────────────────────────────────────────────────────────────────────────────
public override void PlaceStructure(World world,int anchorX,int anchorY)
{
    if(!airTile){Debug.LogWarning("airTile missing"); return;}

    pos  = new Vector2Int(anchorX,anchorY);
    dirX = Random.value<.5f?DirX.Right:DirX.Left;
    steps=0;

    for(int i=0;i<maxDiggerMoves;i++)
    {
        CarveMainCorridor(world);

        MaybeEvery(torchSpacing,()=>PlaceTorch(world));
        TryChance(chestChance,  ()=>SetTile(world,pos,chestTile));

        if(IsCaveBelow(world)){Descend(world);continue;}
        if(CheckLiquid(world)) continue;
        if(TryChance(randomDownChance,()=>DigVertical(world,DirY.Down))) continue;
        if(TryChance(randomUpChance,  ()=>DigVertical(world,DirY.Up)))   continue;

        TryChance(sideTunnelChance,   ()=>CreateSideTunnel(world));
        TryChance(directionChangeChance,()=>dirX=(DirX)(-(int)dirX));

        // ── secret room scheduling / execution ──────────────────────────
        if(pendingSecretH>=0)
        {
            pendingSecretH--;
            if(pendingSecretH==0)
            {
                CreateSecretRoom(world,pendingSecretV);
                pendingSecretH=-1;
            }
        }
        else if(Random.value<secretRoomChance)
        {
            pendingSecretH = Random.Range(secretRoomOffsetRangeH.x,
                                          secretRoomOffsetRangeH.y+1);
            // decide vertical offset now (positive=up, 0=same level, negative=down)
            int v = Random.Range(secretRoomOffsetRangeV.x,
                                 secretRoomOffsetRangeV.y+1);
            pendingSecretV = Random.value<.5f? v : -v;
        }

        if(!Advance(world)) break;
    }
}

#region ── Corridor & movement ────────────────────────────────────────────────
void CarveMainCorridor(World w)=>
    CarveRect(w,(dirX==DirX.Right)?pos:pos+Vector2Int.left,2,3,airTile);

bool Advance(World w)
{
    if(allowSteppingUp && ObstacleAhead(w) && Air(w,pos+Vector2Int.up*2))
    {
        pos.y++; CarveMainCorridor(w);
    }
    pos.x+=(int)dirX; steps++;
    return !stopIfOutOfBounds || InBounds(w,pos);
}
#endregion

#region ── Decorations ────────────────────────────────────────────────────────
void PlaceTorch(World w)
{
    if(!torchTile) return;
    var p = pos + new Vector2Int(dirX==DirX.Right?0:1,2);
    SetTile(w,p,torchTile);
}
#endregion

#region ── Vertical dig & cave descent ───────────────────────────────────────
void DigVertical(World w,DirY dir)
{
    int len=Random.Range(dir==DirY.Down?3:2,dir==DirY.Down?7:5);
    for(int i=0;i<len && InBounds(w,pos);i++)
    { pos.y+=(int)dir; CarveRect(w,pos,2,2,airTile);}
}
bool IsCaveBelow(World w)=>Same(w.GetTileID(pos.x,pos.y-1),w.tiles.UndergroundAirTile);
void Descend(World w){while(IsCaveBelow(w)&&InBounds(w,pos)){pos.y--;CarveRect(w,pos,2,2,airTile);}}
#endregion

#region ── Side tunnels ──────────────────────────────────────────────────────
void CreateSideTunnel(World w)
{
    int sideDir = Random.value<.5f?-(int)dirX:(int)dirX;
    int len=Random.Range(sideTunnelLengthRange.x,sideTunnelLengthRange.y+1);
    Vector2Int cur=pos;
    for(int i=0;i<len&&InBounds(w,cur);i++,cur.x+=sideDir)
    {
        CarveRect(w,(sideDir>0)?cur:cur+Vector2Int.left,2,3,airTile);
        if(torchTile && i>0 && i%torchSpacing==0)
            SetTile(w,cur+new Vector2Int(sideDir>0?0:1,2),torchTile);
    }
}
#endregion

#region ── Secret rooms ──────────────────────────────────────────────────────
void CreateSecretRoom(World w,int vOffset)
{
    int width = Random.Range(secretRoomSizeMin.x,secretRoomSizeMax.x+1);
    int height= Random.Range(secretRoomSizeMin.y,secretRoomSizeMax.y+1);

    // Horizontal start: corridor 2-wide + 1 wall + offset padding + 1 wall
    int padH = 1;  // keep one wall right next to corridor no matter what
    int startX = (dirX==DirX.Right)
                 ? pos.x + 2 + padH + 1           // corridor + wall + 1
                 : pos.x - (width+2+padH+1);      // mirror on left

    // Vertical start: same floor + vOffset, but ensure a full row of wall
    int startY = pos.y + vOffset;
    if(vOffset>=0) startY += 1;  // room is above -> add buffer row
    else            startY -= 1; // room is below -> buffer row below corridor

    CarveRect(w,new(startX,startY),width,height,airTile);

    if(chestTile)
        SetTile(w,new Vector2Int(startX+width/2,startY),chestTile);

    if(torchTile)
        SetTile(w,new Vector2Int(startX+width/2,startY+height-2),torchTile);
}
#endregion

#region ── Liquids / obstacles & helpers ──────────────────────────────────────
bool CheckLiquid(World w)
{
    Vector2Int f=pos+new Vector2Int((int)dirX,0);
    if(!InBounds(w,f)) return false;
    var td=w.tiles.GetTileDataByID(w.GetTileID(f.x,f.y));
    if(td==null||td.behavior!=BlockBehavior.Liquid) return false;
    dirX=(DirX)(-(int)dirX); return true;
}
bool ObstacleAhead(World w)
{
    Vector2Int f=pos+new Vector2Int((int)dirX,0);
    return InBounds(w,f)&&!Air(w,f)&&Air(w,f+Vector2Int.up);
}

// helpers
static bool TryChance(float p,System.Action a){if(p>0&&Random.value<p){a();return true;}return false;}
void MaybeEvery(int spacing,System.Action a){if(spacing>0&&steps>0&&steps%spacing==0) a();}
void CarveRect(World w,Vector2Int s,int wdt,int hgt,TileData t){for(int y=0;y<hgt;y++)for(int x=0;x<wdt;x++)SetTile(w,s+new Vector2Int(x,y),t);}
void SetTile(World w,Vector2Int p,TileData t){if(t&&(!stopIfOutOfBounds||InBounds(w,p)))w.SetTileID(p.x,p.y,t.tileID,false);}
bool Air(World w,Vector2Int p)=>Same(w.GetTileID(p.x,p.y),w.tiles.SkyAirTile,w.tiles.UndergroundAirTile);
bool Same(int id,params TileData[] ts){foreach(var t in ts) if(t&&id==t.tileID) return true; return false;}
static bool InBounds(World w,Vector2Int p){int wx=w.widthInChunks*w.chunkSize,hy=w.heightInChunks*w.chunkSize;return p.x>=0&&p.x<wx&&p.y>=0&&p.y<hy;}
#endregion
}
#endif