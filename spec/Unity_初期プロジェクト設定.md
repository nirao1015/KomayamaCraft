# Unity 初期プロジェクト設定

## 1. 目的

本書は、新作KomayamaCraftの実装開始時に使用するUnityプロジェクト設定を定める。

旧作のシーンとPrefabが同じプロジェクト内に残っているため、既存レイヤーの番号と名称は変更せず、新作専用レイヤーを未使用枠へ追加する。旧作を完全に撤去するまでは、既存レイヤーの再利用や改名を行わない。

## 2. 現在の環境

- Unity：6000.4.0f1
- レンダーパイプライン：URP 2D
- 入力：Input System Package
- 基準解像度：1920×1080
- カラースペース：Linear
- Windows：ウィンドウサイズ変更可能

上記は新作の基盤として問題がないため、現時点では変更しない。

## 3. 新作専用レイヤー

| 番号 | レイヤー名 | 用途 |
| --- | --- | --- |
| 14 | `WorldStatic` | 地形、壁、水際など、動かないワールド形状 |
| 15 | `ResourceNode` | 鉱床、採集可能な植物などの資源発生点 |
| 16 | `NativeLife` | 非敵対の原生生物、採集対象となる生物 |
| 17 | `DroppedItem` | 地面へ排出された回収可能アイテム |
| 18 | `Facility` | 作業台、自動採集設備、宇宙船などの設置・固定設備 |
| 19 | `AutoWorker` | 自動採集・搬送・燃料補給を行う生物または機械 |
| 20 | `PlacementSurface` | 設備を配置できる領域を示す判定用Collider |
| 21 | `PlacementBlocker` | 障害物や予約領域など、設備を配置できない範囲 |
| 22 | `DropArea` | ドロップ品の散らばり先として許可された範囲 |
| 23 | `DropBlocker` | ドロップ禁止として塗った範囲。建設判定には使わない |

24〜31は、仕様確定後に必要となる機能のために未使用のまま残す。

## 4. ドロップ品の散らばり処理

`DroppedItem`と`DropArea`は役割を分ける。

- `DroppedItem`は、生成済みのアイテム本体へ設定する。
- `DropArea`は、アイテムを排出できる範囲を示すTrigger Colliderへ設定する。
- 資源発生点や設備は、Inspectorで対応する`DropArea`を参照する。
- 排出位置は`DropArea`内から選び、`DropBlocker`、設備、原生生物、ワールド外へ出ないことを検証する。
- 自由形状のドロップ禁止領域は、0.5ワールド単位の`NoDropPaint` Tilemapへ専用タイルを塗って編集する。
- `NoDropPaint`は`DropBlocker`レイヤーへ置き、建設用の`PlacementBlocker`とは別塗りにする。
- `NoDropPaint`はTilemapのセル判定を優先し、Scene Viewでは半透明赤で表示する。Game Viewは通常非表示で、デバッグ表示がオンのプレイ中だけ同じ半透明赤を出す。
- 建設不可範囲の塗分けはM2で`PlacementBlocker`側へ別途追加する。
- ドロップ品を物理的な衝突だけで散らすことには依存しない。

これにより、水中、崖の外、設備の内側など、回収できない位置への排出を防ぐ。見た目上の飛び散りは演出として加えてよいが、最終停止位置は許可範囲内に収める。

## 5. レイヤーの使用原則

レイヤーは、物理衝突、Raycast、Overlap判定などを絞り込む必要がある場合に使用する。アイテム種別、設備種別、レシピ、保管機能などのゲーム上の分類には、コンポーネントやデータIDを使用する。

1つのGameObjectは1つのレイヤーしか持てないため、「チェストであり設備でもある」といった複数の意味をレイヤーだけで表現しない。

コードではレイヤー番号を直接記述せず、Inspectorで設定する`LayerMask`を基本とする。固定名の解決が必要な共通基盤だけが`LayerMask.NameToLayer`または`LayerMask.GetMask`を使用し、未定義時の検証を行う。

## 6. Physics 2D

現在、Physics 2Dのレイヤー衝突マトリクスは全組み合わせが有効になっている。

新作の移動方式、自動作業者の経路探索、ドロップ品のCollider構成が未確定なため、初期段階では衝突マトリクスを変更しない。最初のフィールド実装時に、実際にTrigger通知が必要な組み合わせだけを残す。

想定する判定方針は次のとおり。

- ポインター選択：`ResourceNode`、`NativeLife`、`DroppedItem`、`Facility`
- 建設可能判定：`PlacementSurface`
- 建設不可判定：`WorldStatic`、`Facility`、`PlacementBlocker`
- ドロップ位置判定：`DropArea`内、かつ`DropBlocker`・設備・原生生物の外
- 自動作業者の対象検索：担当作業に必要なレイヤーだけ

大量のドロップ品同士を衝突させる必要はない。`DroppedItem`同士の物理衝突は、演出上必要でなければ無効化する。

3D Physicsは現時点で使用目的がないため、設定を変更しない。新作のゲームプレイ判定はPhysics 2Dへ統一する。

## 7. タグ

新作専用のカスタムタグは現時点では追加しない。

対象判定を`CompareTag`へ集中させず、レイヤーと役割コンポーネントで判断する。メインカメラにはUnity標準の`MainCamera`タグを使用する。

## 8. Sorting Layer

描画順はアートとフィールド構成が確定してから設定する。現時点では`Default`だけを維持する。

背景、地面、設備、原生生物、ドロップ品、エフェクト、ワールドUIなどの前後関係は、最初の新作フィールドシーンを作成するときに確定する。旧シーンが同じ`Default`を使用しているため、先に並び順を変更して旧画面へ影響を与えない。

## 9. Player Settings

新作の内部識別名は`KomayamaCraft`、メインフィールドシーン名は`komayama_craft_scene`で固定する。

次の既存設定は維持する。

- 1920×1080
- Linear Color Space
- Input System Package
- ウィンドウサイズ変更可能
- バックグラウンド非実行

次の設定は正式名称決定後に変更する。

- Product Name
- Application Identifier
- Bundle Version
- Build Settingsの登録シーン

特にCompany NameとProduct Nameは`persistentDataPath`を決めるため、セーブ実装後に安易に変更しない。正式名称を確定してから固定する。

旧作シーンと共有基盤の撤去・分離手順は`spec/旧作資産撤去計画.md`に従う。

## 10. 旧作レイヤーの扱い

`BGImg`、`Field1`、`Player`、`Enemy`、`Weapon`、`Field2`、`Transition`は旧シーンと旧Prefabが使用しているため、現在は削除・改名しない。

新作には敵が登場しないため、`Enemy`を新作オブジェクトへ設定しない。旧作シーンとPrefabを完全に撤去した後、使用箇所が0件であることを確認してからレイヤー整理を行う。

