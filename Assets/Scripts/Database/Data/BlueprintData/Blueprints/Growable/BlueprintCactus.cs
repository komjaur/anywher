using UnityEngine;

[CreateAssetMenu(fileName = "Blueprint_Cactus_", menuName = "Game/World/Blueprint/Growable/Cactus Blueprint")]
public class BlueprintCactus : BlueprintGrowable
{
    public int cactusHeight = 3;
    public TileData cactusTile;

    public override void PlaceStructure(World world, int anchorX, int anchorY)
    {
        if (cactusTile == null) return;

        // Place a vertical cactus column up to cactusHeight
        for (int i = 0; i < cactusHeight; i++)
        {
            int py = anchorY + i;
            world.SetTileID( anchorX, py, cactusTile.tileID);
        }
    }
}
