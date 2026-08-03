using UnityEngine;

// プロジェクトウィンドウの右クリックから作成できるようにする
[CreateAssetMenu(fileName = "NewMapData", menuName = "MapEditor/MapData")]
public class MapData : ScriptableObject
{
    // ここでサイズを自由に変えられるようにする
    [Header("Grid Settings")]
    public float gridSize = 3.0f; // 戦車ゲームではこれを 3 に設定する
    public Vector2Int mapSize = new Vector2Int(20, 15);

    // ブロックの配置データ（IDの1次元または2次元配列）
    [HideInInspector]
    public int[] tileIDs; 
}