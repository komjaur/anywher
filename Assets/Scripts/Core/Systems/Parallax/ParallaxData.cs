//
//  ParallaxLayer.cs  – one entry in a ParallaxData stack
//
using UnityEngine;

[System.Serializable]
public class ParallaxLayer
{
    public Sprite     sprite;          // texture slice to tile
    [Range(0f, 1f)] public float speed = 0.2f;  // 0 = far, 1 = camera-locked
    public Vector2    offset;          // shift whole layer
    public Vector2    autoScrollSpeed; // units / sec
    public int        sortingOrder;    // 0 = let PB auto-assign
}

//
//  ParallaxData.cs  – bundle of layers a scene can reuse
//

[CreateAssetMenu(fileName = "ParallaxData", menuName = "Game/Parallax/Data")]
public sealed class ParallaxData : ScriptableObject
{
    public ParallaxLayer[] layers;
}
