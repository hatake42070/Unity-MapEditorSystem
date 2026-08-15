using UnityEngine;

namespace MapEditorSystem.Runtime 
{
    public static class MapGenerator
    {
        // メソッドも static にする
        public static void GenerateMap(MapData mapData, TilePalette palette)
        {
            if (mapData == null || palette == null) return;

            float gridSize = mapData.gridSize;
            int width = mapData.mapSize.x;

            Transform terrainRoot = new GameObject("--- Terrain Root ---").transform;
            Transform objectRoot = new GameObject("--- Object Root ---").transform;

            if (mapData.baseTiles != null)
            {
                for (int i = 0; i < mapData.baseTiles.Length; i++)
                {
                    int tileID = mapData.baseTiles[i];
                    if (tileID > 0)
                    {
                        TileInfo info = palette.baseTiles.Find(t => t.tileID == tileID);
                        if (info.prefab != null)
                        {
                            int x = i % width;
                            int y = i / width;
                            Vector3 pos = new Vector3(x * gridSize, 0, y * gridSize);
                            
                            // 生成したオブジェクトを変数(go)として受け取る
                            GameObject go = Object.Instantiate(info.prefab, pos, Quaternion.identity, terrainRoot);
                            
                            go.transform.localScale = new Vector3(gridSize, gridSize, gridSize);
                        }
                    }
                }
            }

            if (mapData.objects != null)
            {
                foreach (var obj in mapData.objects)
                {
                    ObjectInfo info = palette.placeableObjects.Find(o => o.objectID == obj.objectID);
                    if (info.prefab != null)
                    {
                        Vector3 pos = new Vector3(obj.gridPos.x * gridSize, gridSize, obj.gridPos.y * gridSize);
                        GameObject go = Object.Instantiate(info.prefab, pos, Quaternion.identity, objectRoot);
                        go.transform.localScale = new Vector3(gridSize, gridSize, gridSize);
                    }
                }
            }
        }
    }
}