# TASK-M00-03 レシピ定義

## 目的

入力、出力、加工時間、対応設備、解放条件をデータとして追加できるレシピ定義を実装する。

## 参照仕様

- `spec/KomayamaCraft_ゲーム基本仕様.md`
- `spec/要素設計書/KomayamaCraft_非食材サプライチェーン.md`

## 依存タスク

- `TASK-M00-01_永続IDとデータ規約.md`
- `TASK-M00-02_アイテム定義.md`

## 作業範囲

- `RecipeDefinition`をScriptableObjectとして実装する。
- 少なくとも次の情報をInspectorで設定できるようにする。
  - 永続ID
  - 表示名
  - 1種類以上の入力アイテムと必要数
  - 1種類以上の出力アイテムと生成数
  - 加工時間
  - 対応する設備定義または設備能力ID
  - 必要な解放ID
- 確定出力と、共生組立槽で使う重み付き抽選出力を表現できるようにする。
- 確率値は合計100固定ではなく重みとして扱い、比率調整を容易にする。
- 入出力に同じアイテムが含まれる場合も明示的に扱えるようにする。
- M1用の検証レシピを1件作成する。

## 対象外

- 実際の加工進行とタイマー
- 設備への投入操作
- Tier 1全レシピの本登録
- バランス上の必要数と加工時間の確定

## 完了条件

- [x] コード変更なしで入力、出力、加工時間の異なるレシピを追加できる。
- [x] 複数入力、複数出力、重み付き抽選出力を表現できる。
- [x] 入力数、出力数、加工時間の不正値を検出できる。
- [x] 未登録アイテム、未登録設備、未登録解放IDへの参照を検出できる（M00-06で横断検証）。
- [x] M1用検証レシピをカタログから取得できる。

## 確認方法

1. 1入力1出力、複数入力、抽選出力の検証レシピを作成する。
2. 各レシピを永続IDから取得し、設定値を読み出せることを確認する。
3. 空の入力や0個の出力を設定し、検証でエラーになることを確認する。

## 実装メモ

レシピごとの専用クラスを追加しない。特殊な設備挙動は設備能力として分離し、レシピは材料変換のデータに集中させる。

完了成果物：

- `Assets/Scripts/GameData/KomayamaCraft/RecipeOutput.cs`
- `Assets/Scripts/GameData/KomayamaCraft/RecipeDefinition.cs`
- `Assets/Scripts/GameData/KomayamaCraft/RecipeDefinitionCatalog.cs`
- `Assets/GameData/KomayamaCraft/Recipes/PrototypeProcessing.asset`
- `Assets/Resources/GameData/KomayamaRecipeCatalog.asset`

参照先が各カタログへ登録済みかを横断確認する処理は、M00-06の`KomayamaGameDataValidator`へ統合した。

