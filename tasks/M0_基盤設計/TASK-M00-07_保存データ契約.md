# TASK-M00-07 保存データ契約

## 目的

後から保存対応を追加してデータ構造を作り直すことがないよう、M2以降で保存する状態とJSON用DTOの境界をM0で定義する。

## 参照仕様

- `spec/KomayamaCraft_ゲーム基本仕様.md`
- `spec/作成前資料/player_data_version_spec.md`
- `spec/作成前資料/クラウドsave実績仕様書.txt`

## 依存タスク

- `TASK-M00-01_永続IDとデータ規約.md`
- `TASK-M00-02_アイテム定義.md`
- `TASK-M00-03_レシピ定義.md`
- `TASK-M00-04_設備と資源発生点定義.md`
- `TASK-M00-05_解放条件定義.md`

## 作業範囲

- 新作セーブデータのルートDTOと責務を定義する。
- すべてのセーブファイルに`version`、`buildVersion`、`updatedAtUtc`を持たせる。
- 少なくとも次の保存単位に対応できるDTOを定義する。
  - 手持ちと個数
  - 地面アイテムの定義ID、instance ID、位置、個数
  - 配置設備の定義ID、instance ID、位置、向き
  - 設備の入力、出力、燃料、選択レシピ、加工進捗
  - 資源発生点の定義ID、instance ID、再採集状態
  - 解放済みIDと進行中の納品状態
- UnityEngine.Object参照やScriptableObject本体をDTOへ保存しない。
- Vector型などUnity依存値を直接保存するか、専用の数値DTOへ変換するかを統一する。
- 未知ID、廃止ID、欠損フィールド、未知versionの読み込み方針を定義する。
- 定義データと実行時状態、DTOの変換境界を決める。
- JSONのサンプルと将来の移行テスト方針を仕様へ記録する。

## 対象外

- 実ファイルへの保存・読み込み
- オートセーブのタイミング
- Steam Auto-Cloud接続
- バックアップと破損復旧の実装

## 完了条件

- [x] M2〜M5で必要になる主要状態をDTOで表現できる。
- [x] 定義参照には永続ID、実体参照にはinstance IDを使用している。
- [x] DTOがUnityアセット参照を保持していない。
- [x] version更新と移行処理を追加できる構造になっている。
- [x] 未知・廃止IDを検出して安全に読み飛ばせる方針がある。
- [x] サンプルJSONをレビューできる。

## 確認方法

1. 手持ち、地面品、設備、加工中状態、解放状態を持つサンプルDTOを作る。
2. JSONへ変換し、復元後に同じID・数量・進捗を得られることを確認する。
3. 定義IDを未知の値へ変更し、安全なエラーとして扱えることを確認する。
4. 古いversionのサンプルを用意し、移行処理を追加できる境界になっていることを確認する。

## 実装メモ

M0では保存I/Oを完成させない。ただし実行時モデルを作る時点から保存可能な情報だけで状態を表現し、M2での保存実装時に構造を作り直さないことを目的とする。

## 成果物

- `Assets/Scripts/GameData/KomayamaCraft/KomayamaCraftSaveData.cs`
  - `CurrentVersion = 1`のルートDTOと共通ヘッダーを定義する。
  - 手持ち、地面アイテム、配置設備、設備内在庫・燃料・加工、資源発生点、解放、納品、TierをJsonUtility対応の値だけで保持する。
  - `Float2SaveDto`を使用し、UnityのVector型やアセット参照をDTOへ持ち込まない。
  - Listを空Listで初期化し、欠損フィールド復元後の`EnsureCollections()`を提供する。
- `Assets/Scripts/GameData/KomayamaCraft/IKomayamaCraftSaveMigration.cs`
  - ファイルI/Oから分離した隣接version間JSON変換の責務を定義する。
- `Assets/Scripts/GameData/KomayamaCraft/UnlockStateRecord.cs`
  - 既存の解放状態レコードを公開値フィールドの純粋なJsonUtility DTOへ整理する。
- `spec/データ基盤_保存データ契約.md`
  - 正本ファイル名、DTO全項目、実行時モデルとの境界、未知・廃止ID、欠損、未知version、バックアップ保護、サンプルJSON、移行テスト方針を定義する。

## 保留

- 実ファイルI/O、一時ファイル置換、バックアップ作成、保存タイミング、Steam Auto-Cloud接続は対象外とする。
- DTOの実行時Mapper、読み込み診断、version 1より後の具体的な移行実装とEditModeテストは、それぞれの機能実装時に本契約へ従って追加する。

