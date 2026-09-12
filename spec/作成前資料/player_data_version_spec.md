# playerData.json：ビルドバージョン記録と比較ルール

## 概要

`saveData/playerData.json` には、セーブ形式の整数 `version`（`SoundSettingsManager.PlayerDataVersion`）に加え、**ビルド時の Version 文字列**を `buildVersion` として記録する。

## ビルド Version の取得元

| 環境 | 取得方法 |
|------|----------|
| エディタ Play / ビルド済み Player | `Application.version` |
| Unity 設定上の元 | **File > Build Profiles** でアクティブなプロファイルの **Player Settings Overrides > Version**（`bundleVersion`）。アクティブプロファイルの上書きが Play / ビルドに反映される（Unity 6 Build Profiles 仕様）。 |

実装: `GameBuildVersion.GetCurrentApplicationVersion()` → 内部で `Application.version` を返す。

## 音量・進行の永続化

- **正本は `saveData/playerData.json` のみ**（Steam Auto-Cloud 同期対象）。
- 旧 `sound_volume.json` は**読み書きしない**。残っていれば起動時に削除する。
- **PlayerPrefs は使用しない**（音量・デバッグ全解放とも `playerData.json`）。
- セーブ無し時の音量は、起動シーンの `SoundSettingsManager` Inspector 値（コード既定は各 20）。

## 記録タイミング（title_scene）

1. `SoundSettingsManager` が `Awake` で `playerData.json` を読み込む。
2. `title_scene` の `TitleSceneController.Start` が `SoundSettingsManager.UpdateRecordedBuildVersionOnTitleEntry()` を呼ぶ。
3. 現在の `Application.version` とセーブ上の `buildVersion` を**文字列として比較**する。
4. **未設定（空）または不一致**のとき、現在の Version を `buildVersion` に書き込み、即 `playerData.json` を保存する（**最後に遊んだビルド**の記録）。
5. **一致しているときは書き込まない**（同一ビルドでタイトルを開き直しただけの場合）。

## JSON フィールド

| フィールド | 型 | 意味 |
|------------|-----|------|
| `version` | int | セーブデータ形式の版（コード定数 `PlayerDataVersion`）。ビルド表示 Version とは別。 |
| `buildVersion` | string | **最後に `title_scene` で遊んだとき**のビルド Version 文字列（例: `β_1.0`, `Demo0.1`）。 |
| `master` / `bgm` / `se` | int | 音量 0–20 |
| `game01Cleared` 等 | bool | 各ゲームクリア済み |
| `debugUnlockAllGames` | bool | タイトル Config「全ゲーム解放」 |

同様に **`saveData/game02Data.json`**（`Game02SaveData`）と **`saveData/game03Data.json`**（`Game03MetaSaveData`）も、**各ファイルの `TrySave` 時**に `buildVersion` へ現在の `Application.version` を書き込む（`GameBuildVersion.GetCurrentApplicationVersion()`）。

## バージョン比較ルール（`GameBuildVersion`）

**現時点ではセーブ互換判定などには未使用。** 将来のセーブ無効化・マイグレーション用に API を用意する。

### プレフィックス無視

比較時は **先頭から最初の ASCII 数字が現れる位置以降**だけを対象とし、それより前の文字列（`β`, `α-`, `VER_`, `Demo` 等）は無視する。

| 入力例 | 比較に使う数値列 |
|--------|------------------|
| `α-4.0.1` | `4`, `0`, `1` |
| `β2.3.1` | `2`, `3`, `1` |
| `VER_1.2.3` | `1`, `2`, `3` |
| `Demo0.1` | `0`, `1` |

### セグメント比較

- `.` で区切った各セグメントを**左から順に整数比較**する。
- セグメント数が少ない側の欠けた桁は **0** として扱う。
- 例: `2.0.0` と `1.11.0` → 先頭 `2` vs `1` で `2.0.0` の方が新しい。

### 大小関係の例（新しい → 古い）

`2.0.0` > `1.11.0` > `1.1.1` > `1.1.0` > `1.0.1` > `1.0.0`

### API

- `GameBuildVersion.Compare(a, b)` … `a` が新しければ正、`b` が新しければ負、同値なら 0
- `GameBuildVersion.IsNewerThan(a, b)`
- `GameBuildVersion.TryParseNumericComponents(version, out int[])`

## 実装担当

| 処理 | クラス |
|------|--------|
| 取得・比較 | `GameBuildVersion` |
| セーブ読込・`buildVersion` 永続化 | `SoundSettingsManager` |
| title で最終ビルド Version 更新の起動 | `TitleSceneController` |
