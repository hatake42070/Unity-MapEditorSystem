using UnityEngine;
using System.Collections.Generic;

namespace MapEditorSystem.Runtime
{
    // オブジェクトの配置情報を記憶する構造体
    [System.Serializable]
    public struct PlacedObject
    {
        public int objectID;    // パレットに登録したID
        public Vector2Int gridPos;  // 配置したマスの起点(x, y)
    }

    /// <summary>
    /// 「どの座標に」「何番のブロックがあるか」を記録するデータ
    /// </summary>
    [CreateAssetMenu(fileName = "NewMapData", menuName = "MapEditor/MapData")]
    public class MapData : ScriptableObject
    {
        // ここでサイズを自由に変えられるようにする
        [Header("Grid Settings")]
        public Vector2Int mapSize = new Vector2Int(20, 15);
        public float gridSize = 3.0f; // 戦車ゲームではこれを 3 に設定する
    
        [Header("Layer 1: Base Terrain")]
        // 地形用：mapSize.x * mapSize.y の長さになる1次元配列、中に入るのは tileID
        //[HideInInspector]
        public int[] baseTiles;

        [Header("Layer 2: Placed Objects")]
        // オブジェクト用：サイズに関係なく配置されたもののリスト
        [HideInInspector]
        public List<PlacedObject> objects = new List<PlacedObject>();
    }
}