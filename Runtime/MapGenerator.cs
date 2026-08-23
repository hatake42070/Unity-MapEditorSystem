using UnityEngine;
using System.Collections.Generic; // 辞書(Dictionary)を使うために追加

namespace MapEditorSystem.Runtime 
{
    /// <summary>
    /// MapDataとTilePaletteを受け取り、実際の3Dオブジェクトを生成するクラス
    /// </summary>
    public static class MapGenerator
    {
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

            // --- 床（Layer 1）の生成 ---
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
                            
                            // 役割がTerrainの場合のみ、メッシュ結合のリストに追加
                            if (info.role == MapRole.Terrain)
                            {
                                // メッシュ結合用のデータを収集
                                MeshFilter mf = go.GetComponent<MeshFilter>(); // MeshFilter: 形状データ（頂点やポリゴン）
                                if (mf != null)
                                {
                                    CombineInstance ci = new CombineInstance(); // 1つのパーツをどこにどう置くかという指示書
                                    ci.mesh = mf.sharedMesh; // shardMesh: インスタンスを作成せず参照する
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
                }
            
                // コライダーの中心位置を計算（ブロックの中心が0,0,0からスタートしている前提）
                float centerX = (width - 1) * gridSize / 2f;
                float centerZ = (height - 1) * gridSize / 2f;
                // コライダーを作る時に計算した中心座標を代入してあげる
                mapCenter = new Vector3(centerX, 0, centerZ);
                
                // 床の当たり判定を作成するメソッドを呼び出す
                CreateFloorCollider(terrainRoot, floorCombiners);
            }

            // --- オブジェクト（Layer 2）の生成 ---
            if (mapData.objects != null)
            {
                foreach (var obj in mapData.objects)
                {
                    ObjectInfo info = palette.placeableObjects.Find(o => o.objectID == obj.objectID);
                    
                    // カタログに存在しない場合はスキップ
                    if (info.objectID == 0) continue; 

                    // 分岐1：スポーンマーカーの場合は、座標を記録するだけで生成しない
                    if (info.role == MapRole.SpawnMarker)
                    {
                        // 今回は分かりやすさのため、オブジェクト名(objectName)で1Pか2Pかを判別
                        // インスペクターで「1P Spawn」「2P Spawn」という名前に設定する
                        if (info.objectName.Contains("1P"))
                        {
                            spawn1P = new Vector3(obj.gridPos.x * gridSize, gridSize, obj.gridPos.y * gridSize);
                        }
                        else if (info.objectName.Contains("2P"))
                        {
                            spawn2P = new Vector3(obj.gridPos.x * gridSize, gridSize, obj.gridPos.y * gridSize);
                        }
                        continue; // 次のブロックへ
                    }

                    // 分岐2：プレハブを通常通り生成する（障害物、または動的キャラ）
                    if (info.prefab != null)
                    {
                        Vector3 pos = new Vector3(obj.gridPos.x * gridSize, gridSize, obj.gridPos.y * gridSize);
                        GameObject go = Object.Instantiate(info.prefab, pos, Quaternion.identity, objectRoot);
                        go.transform.localScale = new Vector3(gridSize, gridSize, gridSize);
                        
                        // 壁（StaticObstacle）か、動的キャラ（DynamicEntity）かの判断は、
                        // この後呼ばれる CombineWallMeshes の中で自動的に行われる
                    }
                }
            }
            
            // 全ての生成が終わった後に、壁のメッシュ結合を実行
            // （引数に palette を追加して、情報を渡すようにします）
            CombineWallMeshes(objectRoot, palette);
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
            combinedFloorMesh.CombineMeshes(combiners.ToArray(), true, true); // 1つの巨大なメッシュデータを生成

            // terrainRoot自身にMeshColliderを追加し、結合したメッシュを割り当てる
            MeshCollider floorCollider = root.gameObject.AddComponent<MeshCollider>();
            floorCollider.sharedMesh = combinedFloorMesh;
            
            // （任意）床に対してタグを設定したい場合はここで指定できる
            // root.gameObject.tag = "Floor";
        }
        
        /// <summary>
        /// 壁オブジェクトのメッシュを結合し、継ぎ目のない当たり判定を作成する
        /// </summary>
        private static void CombineWallMeshes(Transform root, TilePalette palette)
        {
            // root(ObjectRoot)以下にある全ての GameObject を取得
            Transform[] allObjects = root.GetComponentsInChildren<Transform>();
            
            // マテリアルごとにメッシュを分類する辞書
            Dictionary<Material, List<CombineInstance>> materialDic = new Dictionary<Material, List<CombineInstance>>();
            
            // 元の壁についていた「摩擦ゼロのマテリアル」を記憶するための変数
            PhysicsMaterial sharedPhysicMaterial = null;

            foreach (Transform t in allObjects)
            {
                // 親のroot自身はスキップ
                if (t == root) continue;

                // 生成されたオブジェクトの名前から、(Clone)という文字を消してプレハブ名に戻す
                string cleanName = t.name.Replace("(Clone)", "");

                // カタログを検索して、このオブジェクトの役割を調べる
                ObjectInfo info = palette.placeableObjects.Find(o => o.prefab != null && o.prefab.name == cleanName);
                
                // もしカタログにない、または役割が StaticObstacle（壁）以外なら結合しない！（タグ判定は廃止）
                if (info.prefab == null || info.role != MapRole.StaticObstacle)
                {
                    continue; 
                }
                
                // 形データ（MeshFilter）を取得。形を持たない空オブジェクトなどはスキップする
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (mf == null) continue;

                // 見た目データ（MeshRendererとMaterial）を取得。絵の具が塗られていない場合はスキップ
                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null) continue;

                // マテリアルごとに合体させるため、辞書（Dictionary）にグループを作る
                Material mat = mr.sharedMaterial;
                if (!materialDic.ContainsKey(mat))
                {
                    // まだそのマテリアルのグループがなければ、新しくリスト（合体用データの束）を作成する
                    materialDic[mat] = new List<CombineInstance>();
                }

                // メッシュ結合用のデータ（CombineInstance）を作成し、形と場所を記録する
                CombineInstance ci = new CombineInstance();
                ci.mesh = mf.sharedMesh; // ブロックの「形」を記録
                // Root（親オブジェクト）から見た相対的な座標・回転・大きさを計算して、正確な配置場所を記録
                ci.transform = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                // 記録したデータを、同じマテリアルのグループ（リスト）に綴じる
                materialDic[mat].Add(ci);

                // 物理演算用のツルツル素材（摩擦ゼロの PhysicMaterial）を1つだけ抜き出して記憶しておく
                // （後で完成した巨大な当たり判定に、このツルツル素材を引き継いで貼り付けるため）
                BoxCollider bc = t.GetComponent<BoxCollider>();
                if (bc != null && sharedPhysicMaterial == null)
                {
                    sharedPhysicMaterial = bc.sharedMaterial;
                }

                // 合体用のデータは全て取り終わったので、元のバラバラのブロックはゲームから完全に消去する
                // これを消さないと、合体後の巨大な壁と元の壁が重なって描画されてしまい、逆に重くなるため
                Object.Destroy(t.gameObject);
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
                // 生成された巨大な壁の「レイヤー」を "Wall" に設定する
                root.gameObject.layer = LayerMask.NameToLayer("Wall");
                
                // 元のブロックから記憶しておいた「摩擦ゼロ」を巨大コライダーに適用
                if (sharedPhysicMaterial != null)
                {
                    collider.material = sharedPhysicMaterial;
                }
            }
        }
    }
}