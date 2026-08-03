using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewTilePalette", menuName = "MapEditor/TilePalette")]
public class TilePalette : ScriptableObject
{
    // IDとプレハブを紐付けるための構造体
    [System.Serializable]
    public struct TileInfo
    {
        public int tileID;
        public GameObject prefab;
    }

    public List<TileInfo> tiles = new List<TileInfo>();
}