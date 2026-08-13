using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor; // エディタ拡張に必須のネームスペース
using MapEditorSystem.Runtime;

namespace MapEditorSystem.Editor
{
    public class MapEditorWindow : EditorWindow
    {
        // ウィンドウにセットするデータ
        private MapData _currentMapData = null;
        private TilePalette _currentPalette = null;
        
        // 編集モードを管理する仕組み
        private enum EditMode
        {
            Terrain, // 地形モード
            Object   // オブジェクトモード
        }

        private EditMode _currentMode = EditMode.Terrain;
        
        // 地形用とオブジェクト用で、別々に「選択中の番号」を記憶する
        private int _selectedTileIndex = 0;
        private int _selectedObjectIndex = 0;
        
        // ---------------------------------------------------------------------
        // Unityの上部メニューに「MapEditor > Open Window」を追加する魔法の属性
        [MenuItem("MapEditor/Open Window")]
        public static void ShowWindow()
        {
            // ウィンドウを生成・表示する
            GetWindow<MapEditorWindow>("Map Editor");
        }

        // ウィンドウが開いたときに呼ばれる
        private void OnEnable()
        {
            // Unityのシーンビュー監視システムに、この関数の登録を行う
            SceneView.duringSceneGui += OnSceneGUI;
        }

        // ウィンドウが閉じたときに呼ばれる
        private void OnDisable()
        {
            // 登録を解除する（これを忘れるとウィンドウを閉じてもエラーが出続ける）
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        // シーンビュー上にGUIを描画する（シーンビュー上でマウスが動くたびに呼ばれる）
        private void OnSceneGUI(SceneView sceneView)
        {
            // キャンバスがセットされていない時は何もしない
            if (_currentMapData == null || _currentPalette == null) return;

            float gridSize = _currentMapData.gridSize;
            int width = _currentMapData.mapSize.x;
            int height = _currentMapData.mapSize.y;
            
            //　マップ全体の輪郭を赤枠で描画
            Handles.color = Color.red;
            float half = gridSize / 2f; 
            Vector3 p1 = new Vector3(-half, 0, -half);
            Vector3 p2 = new Vector3(width * gridSize - half, 0, -half);
            Vector3 p3 = new Vector3(width * gridSize - half, 0, height * gridSize - half);
            Vector3 p4 = new Vector3(-half, 0, height * gridSize - half);
            Vector3[] mapOutline = { p1, p2, p3, p4, p1 };
            Handles.DrawPolyLine(mapOutline);

            // 塗られているマスに色を付ける処理(地形)
            if (_currentMapData.baseTiles != null)
            {
                // 配列の最初から最後まで順番にチェックする
                for (int i = 0; i < _currentMapData.baseTiles.Length; i++)
                {
                    int tileID = _currentMapData.baseTiles[i];
                    if (tileID > 0) // 何か塗られていたら
                    {
                        // パレットの中から、tileIDと一致するTileInfoを探す(一致するIDが見つからなかったら、t.tileID=0となる)
                        TileInfo info = _currentPalette.baseTiles.Find(t => t.tileID == tileID);

                        Color drawColor = info.tileID != 0 ? info.editorColor : Color.white;
                        
                        // 半透明にする
                        drawColor.a = 0.4f;
                         
                        // 配列のインデックスから、2Dのグリッド座標に変換する
                        int x = i % _currentMapData.mapSize.x;
                        int y = i / _currentMapData.mapSize.x;

                        // 実際の3D空間の座標に戻す
                        Vector3 tilePos = new Vector3(x * gridSize, 0, y * gridSize);

                        // グリッドサイズの「塗りつぶされた四角形（Cube）」を描画する
                        Handles.color = drawColor;
                        // DrawSolidRectangleWithOutline: 中身が塗られた四角形
                        Vector3[] verts = new Vector3[]
                        {
                            tilePos + new Vector3(-gridSize/2, 0, -gridSize/2), // 左下
                            tilePos + new Vector3(-gridSize/2, 0,  gridSize/2), // 左上
                            tilePos + new Vector3( gridSize/2, 0,  gridSize/2), // 右上
                            tilePos + new Vector3( gridSize/2, 0, -gridSize/2)  // 右下
                        };
            
                        // 中身の色、枠線の色（少し濃くする）
                        Handles.DrawSolidRectangleWithOutline(verts, drawColor, new Color(drawColor.r, drawColor.g, drawColor.b, 1.0f));
                    }
                }
            }
            // オブジェクトの描画(オブジェクト)
            if (_currentMapData.objects != null)
            {
                foreach (var obj in _currentMapData.objects)
                {
                    ObjectInfo info = _currentPalette.placeableObjects.Find(o => o.objectID == obj.objectID);
                    Color drawColor = info.objectID != 0 ? info.editorColor : Color.gray;
                    
                    drawColor.a = 1.0f;
                    Handles.color = drawColor;
                    // 地形に埋もれないように高さを Y=1.0 に設定して描画
                    Vector3 objPos = new Vector3(obj.gridPos.x * gridSize, 0.1f, obj.gridPos.y * gridSize);
                    Handles.DrawSolidDisc(objPos, Vector3.up, gridSize * 0.35f);
                }
            }

            // 1. マウスカーソルの位置から、画面の奥に向かって放つ「見えない光線（Ray）」を作る
            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            // 2. 高さが0の地面（Plane）を作り、光線が地面とぶつかった場所を計算する
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enter))
            {
                // ぶつかった正確なワールド座標を取得
                Vector3 hitPoint = ray.GetPoint(enter);

                // 3. グリッド（3x3）にピタッと吸い付くように座標を計算する（四捨五入）
                int gridX = Mathf.RoundToInt(hitPoint.x / gridSize);
                int gridY = Mathf.RoundToInt(hitPoint.z / gridSize); // 3D空間なので奥行きはZ軸

                // スナップされたワールド座標
                Vector3 snappedPos = new Vector3(gridX * gridSize, 0, gridY * gridSize);
                
                // カーソルの色をモードで変える
                Handles.color = (_currentMode == EditMode.Terrain) ? Color.cyan : Color.yellow;
                Handles.DrawWireCube(snappedPos, new Vector3(gridSize, 0.1f, gridSize));

                // 4. シーンビュー上に赤い枠線（カーソル）を描画する
                Handles.color = Color.red;
                Handles.DrawWireCube(snappedPos, new Vector3(gridSize, 0.1f, gridSize));

                // 5. グリッドの座標（gridX, gridY）が、マップの範囲内かチェックする
                if (gridX >= 0 && gridX < _currentMapData.mapSize.x &&
                    gridY >= 0 && gridY < _currentMapData.mapSize.y)
                {
                    // マウスの左ボタンが押されているか、またはドラッグ中かチェック
                    if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                    {
                        if (e.button == 0) // 左クリック
                        {
                            // モードによる処理の分岐
                            if (_currentMode == EditMode.Terrain)
                            {
                                int index = gridX + (gridY * _currentMapData.mapSize.x);

                                if (_currentMapData.baseTiles == null ||
                                    _currentMapData.baseTiles.Length != _currentMapData.mapSize.x * _currentMapData.mapSize.y)
                                {
                                    _currentMapData.baseTiles = new int[_currentMapData.mapSize.x * _currentMapData.mapSize.y];
                                }
                                
                                // shift+左クリックで消去
                                if (e.shift) 
                                {
                                    _currentMapData.baseTiles[index] = 0; 
                                }
                                else 
                                {
                                    int selectedID = _currentPalette.baseTiles[_selectedTileIndex].tileID;
                                    _currentMapData.baseTiles[index] = selectedID;
                                }
                            }
                            else if (_currentMode == EditMode.Object)
                            {
                                // 古いMapDataだった場合、リストがnullでエラーになるのを防ぐ
                                if (_currentMapData.objects == null)
                                {
                                    _currentMapData.objects = new List<PlacedObject>();
                                }
                                
                                Vector2Int targetPos = new Vector2Int(gridX, gridY);
                                
                                // クリックした場所にあるオブジェクトをリストから削除（Shift消去 兼 重複防止）
                                _currentMapData.objects.RemoveAll(o => o.gridPos == targetPos);

                                // Shiftが押されていなければ、新しいオブジェクトを配置
                                if (!e.shift)
                                {
                                    PlacedObject newObj = new PlacedObject
                                    {
                                        objectID = _currentPalette.placeableObjects[_selectedObjectIndex].objectID,
                                        gridPos = targetPos
                                    };
                                    _currentMapData.objects.Add(newObj);
                                }
                            }

                            // MapDataのScriptableObjectに値を保存
                            EditorUtility.SetDirty(_currentMapData);

                            // マウスイベントを消費して、シーンビューの他の操作（選択など）を無効化する
                            e.Use();
                        }
                    }
                }

                // 6. マウスが動くたびにシーンビューを強制的に再描画して、カーソルを滑らかに動かす
                sceneView.Repaint();
            }
        }

        // ウィンドウ内のUIを描画する関数（毎フレーム自動で呼ばれます）
        private void OnGUI()
        {
            // タイトル文字
            GUILayout.Label("マップエディタ設定", EditorStyles.boldLabel);
            EditorGUILayout.Space(); // 少し隙間を空ける

            // MapDataをドラッグ＆ドロップでセットする枠
            _currentMapData = (MapData)EditorGUILayout.ObjectField(
                "Map Data (キャンバス)",
                _currentMapData,
                typeof(MapData),
                false // シーン上のオブジェクトではなく、プロジェクト内のアセットのみ許可
            );

            // TilePaletteをドラッグ＆ドロップでセットする枠
            _currentPalette = (TilePalette)EditorGUILayout.ObjectField(
                "Tile Palette (絵の具)",
                _currentPalette,
                typeof(TilePalette),
                false
            );

            EditorGUILayout.Space();
            // モード切り替えタブの追加
            string[] modeLabels = {"地形モード", "オブジェクトモード"};
            
            if (_currentMapData == null || _currentPalette == null)
            {
                EditorGUILayout.HelpBox("MapData と TilePalette を両方セットしてください。", MessageType.Warning);
        
                return;
            }
            
            // GUILayout.Toolbar: タブのようなUIを作る。選択中のタブのインデックスを返す
            _currentMode = (EditMode)GUILayout.Toolbar((int)_currentMode, modeLabels); // 引数１: 現在の番号、引数2: ボタンの文字配列
            EditorGUILayout.Space();
            
            // 現在のモードに合わせて、表示するUIを分岐させる
            if (_currentMode == EditMode.Terrain)
            {
                DrawTerrainUI(); // 地形用UIを描画
            }
            else if (_currentMode == EditMode.Object)
            {
                DrawObjectUI();  // オブジェクト用UIを描画
            }
        }
        
        // 地形用のプルダウン描画メソッド
        private void DrawTerrainUI()
        {
            if (_currentPalette.baseTiles != null && _currentPalette.baseTiles.Count > 0)
            {
                string[] displayOptions = new string[_currentPalette.baseTiles.Count];
                for (int i = 0; i < displayOptions.Length; i++)
                {
                    TileInfo info = _currentPalette.baseTiles[i];
                    string displayName = string.IsNullOrEmpty(info.tileName) ? "名称未設定" : info.tileName;
                    displayOptions[i] = $"{displayName} (ID:{info.tileID})";
                }
                _selectedTileIndex = EditorGUILayout.Popup("🖌️ 塗るブロック", _selectedTileIndex, displayOptions);
            }
            else
            {
                EditorGUILayout.HelpBox("TilePalette に 地形データが登録されていません。", MessageType.Warning);
            }
        }
        
        // オブジェクト用のプルダウン描画メソッド
        private void DrawObjectUI()
        {
            if (_currentPalette.placeableObjects != null && _currentPalette.placeableObjects.Count > 0)
            {
                string[] displayOptions = new string[_currentPalette.placeableObjects.Count];
                for (int i = 0; i < displayOptions.Length; i++)
                {
                    ObjectInfo info = _currentPalette.placeableObjects[i];
                    string displayName = string.IsNullOrEmpty(info.objectName) ? "名称未設定" : info.objectName;
                    displayOptions[i] = $"{displayName} (ID:{info.objectID})";
                }
                // こっちは _selectedObjectIndex を使う
                _selectedObjectIndex = EditorGUILayout.Popup("🍎 置くオブジェクト", _selectedObjectIndex, displayOptions);
            }
            else
            {
                EditorGUILayout.HelpBox("TilePalette に オブジェクトが登録されていません。", MessageType.Warning);
            }
        }
    }
}