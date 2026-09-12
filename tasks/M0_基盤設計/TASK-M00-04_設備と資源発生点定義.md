# TASK-M00-04 設備と資源発生点定義

## 目的

設備と資源発生点の共通情報をデータ化し、新しい設備や採集対象をコード変更なしで追加できるようにする。

## 参照仕様

- `spec/KomayamaCraft_ゲーム基本仕様.md`
- `spec/Unity_初期プロジェクト設定.md`
- `spec/要素設計書/KomayamaCcraft_要素設計書.md`

## 依存タスク

- `TASK-M00-01_永続IDとデータ規約.md`
- `TASK-M00-02_アイテム定義.md`
- `TASK-M00-03_レシピ定義.md`

## 作業範囲

- `FacilityDefinition`をScriptableObjectとして実装する。
- 設備定義に、永続ID、表示情報、Prefab、建設入力、対応レシピ、入出力容量、燃料利用、解放IDを設定できるようにする。
- チェストなど加工を行わない設備も表現できる構造にする。
- `ResourceNodeDefinition`をScriptableObjectとして実装する。
- 資源発生点定義に、永続ID、表示情報、Prefab、産出アイテム、採集量、採集時間、再採集条件、解放IDを設定できるようにする。
- 生物か非生物かは定義データで区別し、敵として扱わない。
- シーン上のDropAreaや配置位置は定義へ固定せず、インスタンス側のInspector参照として扱う。
- M1用の検証設備と検証資源発生点を作成する。

## 対象外

- 設備の配置・移設・撤去
- 採集入力とドロップ生成
- 加工タイマーと在庫処理
- 原生生物のアニメーション

## 完了条件

- [x] データ追加だけで設備と資源発生点を追加できる。
- [x] 加工設備、保管設備、燃料設備を同じ基盤から表現できる。
- [x] 資源発生点ごとに産出物と再採集条件を設定できる。
- [x] Prefab、アイテム、レシピ、解放IDの参照切れを検出できる。
- [x] M1用定義を永続IDから取得できる。

## 確認方法

1. 加工設備、保管設備、資源発生点の検証データを作成する。
2. 各定義をカタログから取得して設定値を確認する。
3. 参照を意図的に外し、検証エラーになることを確認する。

## 実装メモ

設備の種類をレイヤーだけで判定しない。シーン上の設備は`Facility`レイヤーを使用し、具体的な能力はFacilityDefinitionと役割コンポーネントから判断する。

## 成果物

- `FacilityDefinition` / `FacilityDefinitionCatalog`
  - 加工・保管能力、建設コスト、対応レシピ、入出力・保管容量、燃料設定、Prefab、解放IDを定義する。
- `ResourceNodeDefinition` / `ResourceNodeDefinitionCatalog`
  - 生物・植物・非生物、産出物と量、採集時間、再採集方式、Prefab、解放IDを定義する。
- `GameDataCatalogs.KomayamaFacilities` / `GameDataCatalogs.KomayamaResourceNodes`
  - `Resources/GameData`のカタログを読み込み、永続IDで定義を取得する。
- M1検証データ
  - 加工・燃料設備`facility.prototype_processor`
  - 保管設備`facility.prototype_storage`
  - 非生物資源発生点`resource_node.prototype_deposit`
  - Facilityレイヤー18、ResourceNodeレイヤー15を設定した最小Prefab

有効な形式だが存在しない解放IDを含む横断的な参照切れ検出は、M00-06の`KomayamaGameDataValidator`へ統合した。

