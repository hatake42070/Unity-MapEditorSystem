# Unity-MapEditorSystem

個人用3Dゲーム制作のための、軽量かつ柔軟なカスタムマップエディタ＆ジェネレーターパッケージです．
二次元配列（MapData）とカタログ（TilePalette）を組み合わせることで、コードを一切書き換えることなく、新しいブロックやギミックを自由に追加できます．

---

## 🌟 主な機能

- **データ駆動型設計 (Data-Driven):** IDとプレハブの紐付けを `TilePalette`（ScriptableObject）で一元管理．
- **自動メッシュ結合 (Mesh Combining):** 
  - **床 (Terrain):** 個別のコライダーを統合し、弾の引っかかりのない1枚の巨大なMeshColliderを自動生成．
  - **壁 (StaticObstacle):** マテリアルごとに見た目のメッシュを結合し、ドローコール（描画負荷）を極限まで削減．
- **オブジェクト自動仕分け:** スポーン位置（1P/2P）の割り出しや、敵・ギミック（DynamicEntity）の動的生成を自動化．

---

## 📁 フォルダ構成

```text
MapEditorSystem/
 ┣ Runtime/
 ┃  ┣ MapData.cs         # マップの配列データ（GridSizeやID配列）
 ┃  ┣ TilePalette.cs     # ブロックやオブジェクトのカタログ（ScriptableObject）
 ┃  ┗ MapGenerator.cs    # 実際の3D空間へマップを構築・最適化するクラス
 ┗ Editor/
    ┗ MapEditorWindow.cs # （必要に応じて）エディタ拡張用のウィンドウ
```



## 🚀 使用方法
Unityエディタを開き、メニューの Window から Package Manager を選択．
ウィンドウ左上の +（Add）ボタンをクリックします。メニューから Add package from git URL を選択します。GitHubリポジトリの Git URL（.git で終わるURL）を入力し、Add をクリック
### 1.    TilePalette(カタログ)の作成
1. Create > MapEditor > TilePaletteを選択し、アセットを作成する．
2. インスペクターから以下のレイヤーを設定する

  ・Layer1(baseTiles): 床や基本の壁など（Role を Terrain や StaticObstacle に設定）

  ・Layer 2 (placeableObjects): 障害物、スポーンマーカー、敵AIなど（Role を設定）
### 2.    MapData（マップデータ）の準備
1. MapData アセットを作成し、マップのサイズ（Width/Height）とグリッドサイズを設定
2. 配置したいブロックのIDを配列に割り当てる

## 3. MapGenerator の呼び出し
ゲームシーン内のマネージャー等から、以下のように GenerateMap を呼び出す．
```C#
using UnityEngine;
using MapEditorSystem.Runtime;

public class GameManager : MonoBehaviour
{
    [SerializeField] private MapData mapData;
    [SerializeField] private TilePalette tilePalette;

    private void Start()
    {
        // マップを生成し、スポーン位置や中心座標を受け取る
        MapGenerator.GenerateMap(
            mapData, 
            tilePalette, 
            out Vector3 spawn1P, 
            out Vector3 spawn2P, 
            out Vector3 mapCenter
        );

        Debug.Log("1P Spawn: " + spawn1P);
        Debug.Log("2P Spawn: " + spawn2P);
    }
}
```


## 🛠️ 拡張・カスタマイズ
新しいオブジェクトやギミック（例: サンドバッグ、敵AI、壊れる壁など）を追加する際、パッケージ内のコード（MapGenerator）を変更する必要はありません。

新しいプレハブを作成し、任意のタグやスクリプトをアタッチする。

TilePalette に新しいIDを追加し、Prefabと適切な MapRole（役割）を割り当てる。

Terrain: 床としてメッシュ結合する

StaticObstacle: 壁としてメッシュ結合する

DynamicEntity: 結合せず、そのままインスタンス化する（AI・サンドバッグ等）

SpawnMarker: プレハブを生成せず、座標マーカーとして利用する

マップデータの配列にIDを配置するだけで、自動的に構築されます。
