# TASK-M00-02 アイテム定義

## 目的

素材、中間品、修復部品、燃料などを共通形式で追加できるアイテム定義を実装する。

## 参照仕様

- `spec/KomayamaCraft_ゲーム基本仕様.md`
- TASK-M00-01で作成するデータ基盤仕様

## 依存タスク

- `TASK-M00-01_永続IDとデータ規約.md`

## 作業範囲

- 共通利用する`ItemDefinition`をScriptableObjectとして実装する。
- 少なくとも次の情報をInspectorで設定できるようにする。
  - 永続ID
  - 表示名
  - 説明
  - アイコン
  - 分類
  - 1スタックの上限
  - 必要に応じて検索・用途判定へ使う特性
- 個数付き参照を表す共通データ型を用意する。
- アイテム定義へ加工や使用時の個別ロジックを持たせない。
- M1用の検証アイテムをデータ追加だけで作成する。
- 共通スクリプトは新作共通のGameDataフォルダへ配置する。

## 対象外

- 手持ち、チェスト、設備内在庫
- アイテムの地面表示と回収
- Tier 1全アイテムの本登録
- 売値や通貨換算

## 完了条件

- [x] コード変更なしで新しいアイテム定義を作成できる。
- [x] 同じアイテム定義を素材、燃料、完成品の参照元として利用できる。
- [x] スタック上限に0以下などの不正値を設定できない、または検証で検出できる。
- [x] 表示名を変えても永続IDが維持される。
- [x] M1用検証アイテムをカタログから取得できる。

## 確認方法

1. Unity Editorで検証アイテムを2種類作成する。
2. 永続IDから各定義を取得できることを確認する。
3. 表示名とアイコンを変更しても参照関係が壊れないことを確認する。

## 実装メモ

設備用、燃料用などのItemDefinition派生クラスは初期段階では作らない。挙動差はレシピ、設備、特性データ側で表現する。

完了成果物：

- `Assets/Scripts/GameData/KomayamaCraft/GameDataId.cs`
- `Assets/Scripts/GameData/KomayamaCraft/ItemDefinition.cs`
- `Assets/Scripts/GameData/KomayamaCraft/ItemAmount.cs`
- `Assets/Scripts/GameData/KomayamaCraft/ItemDefinitionCatalog.cs`
- `Assets/GameData/KomayamaCraft/Items/`
- `Assets/Resources/GameData/KomayamaItemCatalog.asset`

