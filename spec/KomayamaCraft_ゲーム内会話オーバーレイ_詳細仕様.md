# KomayamaCraft ゲーム内会話オーバーレイ 詳細仕様

> **状態**：実装対象  
> **対象シーン**：`komayama_craft_scene` のみ  
> **関連**：既存 `dialogue_scene`（OP/ED 用フルシーン。本機能とは別系統）

---

## 1. 目的

ゲームプレイ中に、**時間を止めたまま**ワールドを見せつつ会話演出を行う。

- テキスト送り・立ち絵 L/R・演出用 BG・暗幕・カメラ移動・スキップ
- **しない**：会話中セーブ、選択肢分岐、別シーンへの遷移

---

## 2. 配置方針（レイヤー）

| 案 | 判定 |
| --- | --- |
| 別シーン遷移 | 不採用（シームレス不可・ロードヒッチ・カメラ受け渡しが必要） |
| **オーバーレイ（常駐 Canvas）** | **採用** |

時間停止は `KomayamaGameClock.PushPause` / `PopPause`。  
会話 UI・タイプライター・lean_in は **unscaled** 時間で動かす。

---

## 3. シーン構成

```text
DialogueOverlayCanvas          … Sorting Layer UiSystem（Hud より上）
  Dimmer                       … 半透明黒 Image
  OverlayBackground            … 演出用 BG（既定非表示。bg 行で表示）
  StandingArtRoot
    StandingLeft / StandingRight
  DialogueWindow
    SpeakerName / BodyText / AdvanceCursor / SkipButton
```

- `KomayamaScreenCanvasBands` に本 Canvas を **UiSystem** 帯として登録する
- 開始前は Canvas ルートを非表示

---

## 4. クラス

| クラス | 役割 |
| --- | --- |
| `KomayamaCraftDialogueOverlay` | `Play(stageKey)` 窓口。Pause・入力ブロック・カメラスナップ／復元 |
| `KomayamaCraftDialogueRunner` | CSV 進行（既存 `DialogueCsvParser` 利用） |
| `KomayamaCraftCameraController` | 一時フレーミング API・会話中はパン／ズーム無効 |
| `KomayamaCraftInputController` | 会話中はワールド操作をスキップ |
| `KomayamaQuestController` / `KomayamaQuestLogView` | 会話中はクエストパネル・メニューを非表示 |

台本解決:

- `stageKey` 例: `craft_intro_01`
- ファイル: `Assets/GameData/Dialogue/komayama_craft_scene-{stageKey}.csv`
- ランタイムは `DialogueScriptCatalog`（`Tools/GameData/Sync All Catalogs`）

---

## 5. CSV

ヘッダ（既存と同じ）:

```text
type,speaker,text,slot,sprite_key,mode,audio_key
```

| type | 意味 |
| --- | --- |
| `line` / `stand` / `se` / `bgm` / `bgm_stop` / `end` | 既存 `dialogue_scene` と同じ |
| `bg` | OverlayBackground 差し替え＆表示。`mode=hide` または空 `sprite_key` で非表示（ワールドのみ） |
| `dim` | 暗幕 α。`mode` に 0〜1（未指定時は Inspector 既定） |
| `camera` | `text`=`x,y`。`mode`=移動秒（空/0=instant）。`audio_key`=任意で orthographicSize |

スキップ: 以降を飛ばして終了処理（分岐なし）。  
**自動プレイ時はスキップ相当を使わず、テキスト送り（左クリック／スペース相当）のみで進める**（`KomayamaCraft_自動化プレイテスト_詳細仕様.md`）。

フィールド会話の暗幕既定 α は **0**（不要な黒塗りを出さない）。

---

## 6. 開始／終了

1. `Play(stageKey)` → Pause → 入力ブロック → Canvas 表示 → カメラスナップショット → **クエスト／メニュー HUD 非表示**
2. CSV 再生
3. `end` または Skip → 立ち絵／BG 片付け → カメラ復元 → PopPause → 入力解除 → **HUD 復帰** → 完了コールバック

---

## 7. 改訂履歴

| 日付 | 内容 |
| --- | --- |
| 2026-09-19 | 会話中はクエストパネル・メニュー非表示を全体仕様として確定 |
| 2026-09-19 | 自動プレイは送りのみ。フィールド会話の既定 dim=0 |
| 2026-09-18 | 初版。レイヤー方針・CSV 拡張・Pause／カメラ |
| 2026-09-18 | OP：新規のみ目覚め＋`craft_op_01`。Debug「OP演出しない」。Tutorial は無効化 |
