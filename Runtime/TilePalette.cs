using System.Collections.Generic;
using UnityEngine;

namespace MapEditorSystem.Runtime
{
    // マップに配置されるものの「役割」を定義
    public enum MapRole
    {
        Terrain,            // 地形（床としてメッシュ結合する）
        StaticObstacle,     // 静的障害物（壁としてメッシュ結合する）
        DynamicEntity,      // 動的キャラ・ギミック（結合せず、そのまま配置する）
        SpawnMarker          // スポーン地点（プレハブは生成せず、座標だけを外部に渡す）
    }
    
    // 地形用のデータ構造
    [System.Serializable]
    public struct TileInfo
    {
        public int tileID;
        public GameObject prefab;
        public string tileName; // エディタ上で見やすくするための名前
        public Color editorColor; // エディタ表示用の色
        public MapRole role;
    }

    // オブジェクト用のデータ構造
    [System.Serializable]
    public struct ObjectInfo
    {
        public int objectID;
        public GameObject prefab;
        public string objectName;
        public Color editorColor;
        public MapRole role;
    }
    
    /// <summary>
    /// 「ID番号」と「実際の3Dモデル(Prefab)」を紐付けるカタログ
    /// </summary>
    [CreateAssetMenu(fileName = "NewTilePalette", menuName = "MapEditor/TilePalette")]
    public class TilePalette : ScriptableObject
    {
        [Header("Layer 1: 地形用カタログ")]
        [Tooltip("床や基本の壁など、塗りつぶす用のブロック")]
        public List<TileInfo> baseTiles = new List<TileInfo>();

        [Header("Layer 2: オブジェクト用カタログ")]
        [Tooltip("3x6の壁やギミックなど、スタンプのように置くオブジェクト")]
        public List<ObjectInfo> placeableObjects = new List<ObjectInfo>();
    }  
}