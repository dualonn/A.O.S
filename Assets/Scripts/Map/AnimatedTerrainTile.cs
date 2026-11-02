using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A tile that loops through sprites (e.g. rippling water). All copies stay in step,
/// so patterns that run across tile edges line up.
/// </summary>
public class AnimatedTerrainTile : TileBase
{
    public Sprite[] frames;
    public float framesPerSecond = 3f;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        tileData.color = Color.white;
        tileData.colliderType = Tile.ColliderType.None;
        tileData.flags = TileFlags.LockAll;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data)
    {
        if (frames == null || frames.Length < 2)
        {
            return false;
        }
        data.animatedSprites = frames;
        data.animationSpeed = framesPerSecond;
        data.animationStartTime = 0;
        return true;
    }
}
