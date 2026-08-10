using System.Collections.Generic;
using UnityEngine;

namespace MapEditorSystem.Runtime
{
    // 地形用のデータ構造
    [System.Serializable]
    public struct TileInfo
    {
        public int tileID;
        public GameObject prefab;
        public string tileName; // エディタ上で見やすくするための名前
        
        // エディタ表示用の色
        public Color editorColor;
    }

    // オブジェクト用のデータ構造
    [System.Serializable]
    public struct ObjectInfo
    {
        public int objectID;
        public GameObject prefab;
        public string objectName;
        public Color editorColor;
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