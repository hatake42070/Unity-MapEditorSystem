using System;
using UnityEngine;
using UnityEditor; // エディタ拡張に必須のネームスペース
using MapEditorSystem.Runtime;

namespace MapEditorSystem.Editor
{
    public class MapEditorWindow : EditorWindow
    {
        // ウィンドウにセットするデータ
        private MapData _currentMapData;
        private TilePalette _currentPalette;

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
            if (_currentMapData == null) return;

            float gridSize = _currentMapData.gridSize;

            // 塗られているマスに色を付ける処理
            if (_currentMapData.baseTiles != null)
            {
                // 配列の最初から最後まで順番にチェックする
                for (int i = 0; i < _currentMapData.baseTiles.Length; i++)
                {
                    int tileID = _currentMapData.baseTiles[i];
                    if (tileID > 0) // 何か塗られていたら
                    {
                        // 配列のインデックスから、2Dのグリッド座標に変換する
                        int x = i % _currentMapData.mapSize.x;
                        int y = i / _currentMapData.mapSize.x;

                        // 実際の3D空間の座標に戻す
                        Vector3 tilePos = new Vector3(x * gridSize, 0, y * gridSize);

                        // 半透明の緑色で、マス目の中心に円を描画して「塗られている感」を出す
                        Handles.color = new Color(0.0f, 1.0f, 0.0f, 0.3f);
                        Handles.DrawSolidDisc(tilePos, Vector3.up, gridSize * 0.45f);
                    }
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
                            // 地形配列のindex = X + (Y * 横幅): 1次元配列のインデックスを計算
                            int index = gridX + (gridY * _currentMapData.mapSize.x);

                            // 配列がまだ作られていなければ生成する
                            if (_currentMapData.baseTiles == null ||
                                _currentMapData.baseTiles.Length != _currentMapData.mapSize.x * _currentMapData.mapSize.y)
                            {
                                _currentMapData.baseTiles = new int[_currentMapData.mapSize.x * _currentMapData.mapSize.y];
                            }
                            
                            // Shiftキーを押しながら左クリック（消しゴムモード）
                            if (e.shift) 
                            {
                                // 0 は「空（何もない）」として保存
                                _currentMapData.baseTiles[index] = 0; 
                            }
                            // 普通の左クリック（ペイントモード）
                            else 
                            {
                                // 今は仮で「1」を保存。（パレットの0番目のアイテムを意味する）
                                _currentMapData.baseTiles[index] = 1; 
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

            // データのセット状況によって親切なメッセージを出す
            if (_currentMapData == null || _currentPalette == null)
            {
                EditorGUILayout.HelpBox("MapData と TilePalette を両方セットしてください。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("準備完了！シーンビューで編集できます。", MessageType.Info);

                // TODO: ここに後で「塗るブロックを選ぶボタン」などを追加します
            }
        }
    }
}