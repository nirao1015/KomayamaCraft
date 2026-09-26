# KomayamaCraft 作業引継（2026-09-26）

> **用途**：長いチャットを切り、新会話でゲーム作成を続けるときの入口。  
> **シーン**：`Assets/Scenes/komayama_craft_scene.unity`  
> **前チャット**：地図アンビエント一式・制限グリッド全面塗り・FPS API＋設定 UI・RestrictionGrid 起動時アクティブ化まで完了。

---

## 1. いまの確定状態（触ってよい／触らなくてよい）

| 領域 | 状態 | 正本 |
| --- | --- | --- |
| 地図アンビエント | **OK（一旦完了）**。雲・胞子・沼気泡・熱嚢草・間欠泉・砂漠砂嵐・海上風筋・西海岸白波・浮小岩／小島・狐うたたね | `spec/KomayamaCraft_地図演出コントローラー_詳細仕様.md` |
| 地図アンビエント軽量化 | **保留**。「重い」と言われてから §8 | 同上 §8 |
| FPS 切替 | **完了**（無制限／60／30・既定60・`playerData.json`＋一般タブ Dropdown） | タイトル仕様 §4.5〜4.6／`GameFrameRate.cs`／`TitleConfigPanelController` |
| Drop／Build 制限塗り | 海外接＋外周10マスまで**全面塗り済み**。ユーザーが許可範囲を消していく | 指示差分 §3.5／建設メニュー §8.1 |
| RestrictionGrid 起動 | Play 開始で `DropRestrictionGrid`／`BuildRestrictionGrid`（子 Paint 含む）を強制アクティブ | `KomayamaRestrictionGridBoot` on `World` |

---

## 2. すぐやる候補（ユーザー作業とエージェント作業）

### ユーザー側（手元）

1. **NoDrop／NoBuild の許可範囲を消す**（Scene の塗りツール）
2. 環境エフェクト見た目の微調整（必要なら）

### エージェント側（新チャットで依頼しやすい順）

1. 地図演出残り：`Unity_control_notes.md` の **小型軌道繭**／**位相断層の緑色雷**
2. ユーザーが「重い」と言ったら地図仕様 §8 の軽量化
3. 本番データ／クエスト／施設など本線進行（このチャット以前の本線タスク）

---

## 3. キーファイル

| 種類 | パス |
| --- | --- |
| 地図演出コントローラ | `Assets/Scripts/komayama_craft_scene/KomayamaMapAmbienceController.cs` ＋各 `*Ambience*.cs` |
| 浮石 | `KomayamaFloatBobAmbience.cs`（岩＝初期、島＝1.4倍距離・3倍移動・端8〜12秒停止） |
| FPS | `GameFrameRate.cs`／`SoundSettingsManager.cs`／`TitleConfigPanelController`（`FrameRateRow`） |
| 制限グリッド起動 | `KomayamaRestrictionGridBoot.cs`（`World`） |
| 素材メモ | `Assets/Sprites/map_effect/Unity_control_notes.md` |
| 長い会話警告ルール | `.cursor/rules/warn-long-conversation.mdc` |

---

## 4. 新チャット開始プロンプト例

```
KomayamaCraft 続き。入口は spec/KomayamaCraft_作業引継_2026-09-26.md。
地図アンビエント・FPS設定UIは完了扱い。次は（例: 軌道繭／位相雷／NoBuild 以外の本線）をやって。
```

---

## 5. 注意

- Inspector 値・Hierarchy 兄弟順は勝手に変えない（既存ルール）
- Find／実行時 AddComponent は原則禁止。参照はシーン配線
- 環境エフェクトを「重い」と言うまで軽量化しない
