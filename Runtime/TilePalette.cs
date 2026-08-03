using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct TileInfo
{
    public int tileID;
    public GameObject prefab;
    public string tileName; // エディタ上で見やすくするための名前
}

/// <summary>
/// 「ID番号」と「実際の3Dモデル(Prefab)」をひも図けるカタログ
/// </summary>
[CreateAssetMenu(fileName = "NewTilePalette", menuName = "MapEditor/TilePalette")]
public class TilePalette : ScriptableObject
{
    [Header("Base Tiles (1x1 Grid)")]
    [Tooltip("床や基本の壁など、塗りつぶす用のブロック")]
    public List<TileInfo> baseTiles = new List<TileInfo>();

    [Header("Objects (Any Size)")]
    [Tooltip("3x6の壁やギミックなど、スタンプのように置くオブジェクト")]
    public List<TileInfo> objects = new List<TileInfo>();
}