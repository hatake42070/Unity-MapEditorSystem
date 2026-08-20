using UnityEngine;
using System.Collections.Generic; // 辞書(Dictionary)を使うために追加

namespace MapEditorSystem.Runtime 
{
    /// <summary>
    /// MapDataとTilePaletteを受け取り、実際の3Dオブジェクトを生成するクラス
    /// </summary>
    public static class MapGenerator
    {
        // 1Pと2PのIDを定義
        private const int SPAWN_1P_ID = 101;
        private const int SPAWN_2P_ID = 102;
        
        // 引数に「out Vector3」を追加して、結果を外に渡せるようにする
        public static void GenerateMap(MapData mapData, TilePalette palette, out Vector3 spawn1P, out Vector3 spawn2P, out Vector3 mapCenter)
        {
            spawn1P = Vector3.zero;
            spawn2P = Vector3.zero;
            mapCenter = Vector3.zero;
            
            if (mapData == null || palette == null) return;

            float gridSize = mapData.gridSize;
            int width = mapData.mapSize.x;
            int height = mapData.mapSize.y;

            Transform terrainRoot = new GameObject("--- Terrain Root ---").transform;
            Transform objectRoot = new GameObject("--- Object Root ---").transform;

            if (mapData.baseTiles != null)
            {
                // 床のメッシュ結合用のリストを用意
                List<CombineInstance> floorCombiners = new List<CombineInstance>();
                
                for (int i = 0; i < mapData.baseTiles.Length; i++)
                {
                    int tileID = mapData.baseTiles[i];
                    if (tileID > 0)
                    {
                        TileInfo info = palette.baseTiles.Find(t => t.tileID == tileID);
                        if (info.prefab != null)
                        {
                            // 配列のインデックスから x, y 座標を計算する
                            int x = i % width;
                            int y = i / width;
                            Vector3 pos = new Vector3(x * gridSize, 0, y * gridSize);
                            
                            // 生成したオブジェクトを変数(go)として受け取る
                            GameObject go = Object.Instantiate(info.prefab, pos, Quaternion.identity, terrainRoot);
                            go.transform.localScale = new Vector3(gridSize, gridSize, gridSize);
                            
                            // メッシュ結合用のデータを収集
                            MeshFilter mf = go.GetComponent<MeshFilter>();
                            if (mf != null)
                            {
                                CombineInstance ci = new CombineInstance();
                                ci.mesh = mf.sharedMesh;
                                // terrainRootから見た相対的な位置・回転・スケールを計算して正確に配置
                                ci.transform = terrainRoot.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                                floorCombiners.Add(ci);
                            }
                            
                            // 個別の床タイルについている不要なコライダーを削除（パフォーマンス最適化と引っかかり防止）
                            Collider col = go.GetComponent<Collider>();
                            if (col != null)
                            {
                                Object.Destroy(col);
                            }
                        }
                    }
                }
            
                // コライダーの中心位置を計算（ブロックの中心が0,0,0からスタートしている前提）
                float centerX = (width - 1) * gridSize / 2f;
                float centerZ = (height - 1) * gridSize / 2f;
                // コライダーを作る時に計算した中心座標を代入してあげる
                mapCenter = new Vector3(centerX, 0, centerZ);
                
                // 床の当たり判定を作成するメソッドを呼び出す
                CreateFloorCollider(terrainRoot, floorCombiners);
            }

            if (mapData.objects != null)
            {
                foreach (var obj in mapData.objects)
                {
                    // 1Pのスポーン地点を見つけた場合
                    if (obj.objectID == SPAWN_1P_ID)
                    {
                        spawn1P = new Vector3(obj.gridPos.x * gridSize, gridSize, obj.gridPos.y * gridSize);
                        continue; // Prefabは生成しない
                    }
                    
                    // 2Pのスポーン地点を見つけた場合
                    if (obj.objectID == SPAWN_2P_ID)
                    {
                        spawn2P = new Vector3(obj.gridPos.x * gridSize, gridSize, obj.gridPos.y * gridSize);
                        continue; // Prefabは生成しない
                    }
                    
                    ObjectInfo info = palette.placeableObjects.Find(o => o.objectID == obj.objectID);
                    if (info.prefab != null)
                    {
                        Vector3 pos = new Vector3(obj.gridPos.x * gridSize, gridSize, obj.gridPos.y * gridSize);
                        GameObject go = Object.Instantiate(info.prefab, pos, Quaternion.identity, objectRoot);
                        go.transform.localScale = new Vector3(gridSize, gridSize, gridSize);
                    }
                }
            }
            // 全ての生成が終わった後に、壁のメッシュ結合を実行
            CombineWallMeshes(objectRoot);
        }

        /// <summary>
        /// 収集した床のメッシュ情報を結合し、継ぎ目のない1枚の当たり判定を作成する
        /// </summary>
        private static void CreateFloorCollider(Transform root, List<CombineInstance> combiners)
        {
            if (combiners.Count == 0) return;

            // 収集したメッシュを1つに結合する
            Mesh combinedFloorMesh = new Mesh();
            combinedFloorMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedFloorMesh.CombineMeshes(combiners.ToArray(), true, true);

            // terrainRoot自身にMeshColliderを追加し、結合したメッシュを割り当てる
            MeshCollider floorCollider = root.gameObject.AddComponent<MeshCollider>();
            floorCollider.sharedMesh = combinedFloorMesh;
            
            // （任意）床に対してタグを設定したい場合はここで指定できる
            // root.gameObject.tag = "Floor";
        }
        
        /// <summary>
        /// 壁オブジェクトのメッシュを結合し、継ぎ目のない当たり判定を作成する
        /// </summary>
        private static void CombineWallMeshes(Transform root)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>();
            
            // マテリアルごとにメッシュを分類する辞書
            Dictionary<Material, List<CombineInstance>> materialDic = new Dictionary<Material, List<CombineInstance>>();
            
            // 元の壁についていた「摩擦ゼロのマテリアル」を記憶するための変数
            PhysicsMaterial sharedPhysicMaterial = null;

            foreach (MeshFilter mf in filters)
            {
                // ※重要※ 画像で確認した「Wall」タグがついているオブジェクトのみを結合する！
                // これにより、もし今後「宝箱」などを置いても、それが壁に結合されてしまうのを防ぎます。
                if (!mf.gameObject.CompareTag("Wall")) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;
                
                Material mat = mr.sharedMaterial;
                if (!materialDic.ContainsKey(mat))
                {
                    materialDic[mat] = new List<CombineInstance>();
                }
                
                CombineInstance ci = new CombineInstance();
                ci.mesh = mf.sharedMesh;
                // Rootから見た相対的な位置・回転・スケールを計算して正確に配置
                ci.transform = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                materialDic[mat].Add(ci);

                // 摩擦ゼロの物理マテリアルを1つだけ記憶しておく
                BoxCollider bc = mf.GetComponent<BoxCollider>();
                if (bc != null && sharedPhysicMaterial == null)
                {
                    sharedPhysicMaterial = bc.sharedMaterial;
                }
                
                // 結合が終わった元のブロックは不要になるため削除する（処理を軽くするため）
                Object.Destroy(mf.gameObject);
            }
            
            // 物理判定用の巨大メッシュを作るためのリスト
            List<CombineInstance> physicsCombiners = new List<CombineInstance>();
            
            // マテリアルごとに「見た目」のオブジェクトを生成
            foreach (var kvp in materialDic)
            {
                Material mat = kvp.Key;
                List<CombineInstance> combineInstances = kvp.Value;
                
                Mesh combinedMesh = new Mesh();
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // ブロック数が多い場合のエラー防止
                combinedMesh.CombineMeshes(combineInstances.ToArray(), true, true);
                
                // 見た目用の空オブジェクトを生成
                GameObject visualObj = new GameObject("CombinedWall_" + mat.name);
                visualObj.transform.SetParent(root);
                visualObj.transform.localPosition = Vector3.zero;
                visualObj.transform.localRotation = Quaternion.identity;
                visualObj.transform.localScale = Vector3.one;
                
                MeshFilter mf = visualObj.AddComponent<MeshFilter>();
                mf.sharedMesh = combinedMesh;
                
                MeshRenderer mr = visualObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                
                // 物理コライダー用にメッシュ情報を渡す
                CombineInstance physicsCi = new CombineInstance();
                physicsCi.mesh = combinedMesh;
                physicsCi.transform = Matrix4x4.identity; 
                physicsCombiners.Add(physicsCi);
            }
            
            // 全てのマテリアルのメッシュをさらに1つに結合して、完璧な1枚の当たり判定を作る
            if (physicsCombiners.Count > 0)
            {
                Mesh finalPhysicsMesh = new Mesh();
                finalPhysicsMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                finalPhysicsMesh.CombineMeshes(physicsCombiners.ToArray(), true, true);
                
                // rootに巨大な MeshCollider を追加
                MeshCollider collider = root.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = finalPhysicsMesh;
                // 巨大コライダーを持ったオブジェクトをWallとして認識させる
                root.gameObject.tag = "Wall";
                
                // 元のブロックから記憶しておいた「摩擦ゼロ」を巨大コライダーに適用
                if (sharedPhysicMaterial != null)
                {
                    collider.material = sharedPhysicMaterial;
                }
            }
        }
    }
}