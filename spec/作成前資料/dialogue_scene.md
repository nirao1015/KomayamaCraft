# dialogue_scene（会話劇シーン）要件定義

本ドキュメントは、**メニューとゲーム本編の間**に挟む会話劇シーンの要件を定義する。  
台本は **Yarn Spinner を使わず**、**CSV（UTF-8）＋自前の会話ランナー**で読み込み・再生する。

---

## 1. 目的・位置づけ

- プレイ開始前に、**会話・ナレーション**を挟み、没入感や状況説明を行う。
- **シーン遷移の流れ（目標）**  
  `menu_scene` → **`dialogue_scene`（本シーン）** → `game_stage_scene`
- 本シーン終了後は、`menu_scene` から渡された **遷移先シーン名** へ遷移する（未指定時は `game_stage_scene`）。

---

## 2. 技術スタック

- **台本**: CSV ファイル（仕様は **「CSV 台本仕様」** 参照）。ステージ名に応じて読み込むファイルを切り替える。
- **ランタイム**: Unity（C#）で CSV を 1 行ずつ解釈し、UI・Audio に反映する。
- **永続化・分岐保存は行わない**（後述「対象外」）。
- **`SoundSettingsManager`**（master / bgm / se）を適用する。
- 画面遷移のフェードは **`FadeManager`** 方針に合わせる（秒数は Inspector）。
- 画面比率は `spec/base.md` に従い、`FixedAspectCanvasFitter` で UI を 16:9 表示領域へ追従させる。

---

## 3. 画面構成（GameObject イメージ）

- **[Background]** … 会話用背景（全画面）
- **[StandingArtRoot]** … 立ち絵親。子に **[StandingLeft]** / **[StandingRight]**（最大 2 体）
- **[DialogueWindow]** … 会話窓（画像ベースの枠＋本文 TextMeshPro）
  - 話者名ラベル（現在の話者を表示）
  - 本文（タイプライター、TMP の領域内で自動改行）
  - **待機カーソル**（点滅アニメーションで「送れる」ことを示す）
  - **[SkipButton]** … スキップ
- **BGM / SE** 用 `AudioSource`（分離）
- **EventSystem**（クリック送り用）

---

## 4. 挙動要件

### 4.1 会話の進行

- CSV の `line` 行を順に表示し、1 表示単位ごとに送り可能。
- 入力: **マウス左クリック／スペース**。
- タイプライター中に入力した場合は、**その行を全文表示**する。
- 全文表示後は、**再度入力で次行**へ進む。
- 行切替時は **0.2 秒**入力を受け付けない（予期せぬ連打対策、Inspector 調整可）。
- 待機カーソルは、送り可能なときのみ点滅表示する。
- 話者名は `line.speaker` を表示（空ならナレーション扱い）。

### 4.2 立ち絵（最大 2 体）

- スロット `L` / `R`。
- `mode`（CSV）  
  - `instant` … 画像を即時差し替え  
  - `lean_in` … 画面外からスライドイン  
  - `hide` … 該当スロット非表示

### 4.3 背景

- `bg` 行で背景キーを指定し、`Background` のスプライトを差し替える。
- 背景差し替え時は暗色乗算を避け、原色表示（`Color.white`）で表示する。

### 4.4 BGM / SE

- `bgm` 行で BGM を指定（ループ再生）。
- `bgm_stop` 行で BGM を停止。停止は既定で **フェードアウト**。
- `bgm_stop.mode` に秒数を入れた場合は、その秒数でフェード停止する（例: `1.0`）。
- `se` 行で SE を 1 発再生。
- BGM と SE は必ず別 `AudioSource` を使用する（同一 `AudioSource` 共有は禁止）。
- `AudioSource.volume` はコード側で `1f` を基本値とし、最終音量は `SoundSettingsManager` のゲインで反映する。
  - BGM: `GetBgmGain01()`
  - SE: `GetSeGain01()`
- 全体基礎倍率は `SoundSettingsManager` の `bgmBaseMultiplier` / `seBaseMultiplier` を使用する。

### 4.5 スキップ・終了・フェード

- SkipButton はフェード後に遷移先シーンへ遷移（`SceneTransitionContext.DestinationSceneName` があればそれ、無ければ `game_stage_scene`）。
- CSV 終端または `end` 行到達後も、同様にフェードして遷移先シーンへ遷移。
- `menu_scene` から遷移した直後は、残留フェードがある場合に解除してから会話再生を開始する。

### 4.6 SkipButton 表示仕様

- 背景画像: `Sprites/dialog/shape022_style02_color04`
- 背景表示: 横方向ストレッチ、`skipBackgroundTint`（既定アルファ 80%）で色・透明度を調整
- フォント: **`Fonts/LightNovelPOPv2 SDF`**
- 文字サイズ既定: 42
- 可読性向上として、黒系シャドウを適用可能（縁取り必須ではない）

---

## 5. 遷移キーと CSV の対応

- `SceneTransitionContext.DestinationSceneName`（例: `game_stage_scene`）と
  `SceneTransitionContext.StageName`（例: `stage_01`）を会話シーン開始時に読む。
- 読み込む CSV は **`Assets/GameData/Dialogue/{scene}-{stage}.csv`** を編集正本とし、ビルド同梱は **`Resources/GameData/DialogueScriptCatalog`**（TextAsset 参照）を正とする。
  - 例: `game_stage_scene-stage_01.csv`
  - `StageName` 未指定（null/empty）の場合は `null` 文字列として扱い、`{scene}-null.csv` を解決対象とする。
- 読み込み失敗時のみ、開発用 fallback CSV を参照してよい。
- ゲーム本編へ渡す前にステージ名を上書きしない。

---

## 6. 対象外

- 変数・フラグのセーブ
- 複数周回に跨る分岐記憶
- 会話中のセーブ／ロード UI、ポーズメニュー（別タスク）

---

## 7. CSV 台本仕様

### 7.1 ヘッダ（必須）

```text
type,speaker,text,slot,sprite_key,mode,audio_key
```

### 7.2 列の意味

| 列名 | 意味 |
|------|------|
| type | 行種別（`line` / `bg` / `stand` / `se` / `bgm` / `bgm_stop` / `fx_shake` / `fx_shake_start` / `fx_shake_stop` / `end`） |
| speaker | `line` 時の話者名（空ならナレーション） |
| text | `line` 時の本文 |
| slot | `stand` 時に `L` / `R` |
| sprite_key | `bg` / `stand` のキー |
| mode | `stand` の `instant` / `lean_in` / `hide`、`bgm_stop` ではフェード秒（任意）、`fx_shake*` ではプリセット名（例: `hit` / `descent`） |
| audio_key | `se` / `bgm` のキー（`seClips` / `bgmClips` の辞書キー） |

### 7.3 type 一覧

| type | 説明 |
|------|------|
| line | 会話表示（タイプライター対象） |
| bg | 背景変更 |
| stand | 立ち絵変更 |
| se | 効果音 1 発 |
| bgm | BGM 再生 |
| bgm_stop | BGM 停止（フェード） |
| fx_shake | 背景 1 ショット揺れ（`mode` = プリセット。例: `hit`） |
| fx_shake_start | 背景継続揺れ開始（`mode` = プリセット。例: `descent`） |
| fx_shake_stop | 背景継続揺れ停止 |
| end | 会話終了 |

---

## 8. stage_01 現行仕様（2026-04-28）

- 台本: `spec/シナリオ_01.txt` を `Assets/GameData/Dialogue/game_stage_scene-stage_01.csv` に反映し、`Tools/GameData/Sync All Catalogs` でカタログを更新する。
- 話者: **語り部**（立ち絵なし）。
- 背景: `Sprites/dialog/dialog_bg_01`
- BGM: `魔王魂  民族11`
- 文字送りカーソル: `Sprites/dialog/shape001_style01_color04`
- 終了時: BGM フェードアウト後、画面フェードして `SceneTransitionContext.DestinationSceneName`（未指定時は `game_stage_scene`）へ遷移。

---

## 9. 関連タスク

| タスク | 内容 |
|--------|------|
| `tasks/TASK-100-dialogue-csv.md` | CSV パース・行モデル・ファイル解決 |
| `tasks/TASK-101-dialogue-scene.md` | `dialogue_scene` と会話ランナー |
| `tasks/TASK-102-dialogue-menu-flow.md` | メニュー接続・CSV 配置 |
