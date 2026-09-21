# KomayamaCraft タイトル画面 詳細仕様

> **状態**：実装反映（2026-09-21）。見た目の位置・周期の微調整は Inspector 現行値を優先。Hierarchy の兄弟順はユーザー調整を尊重（勝手に並べ替えない）。

---

## 0. 文書の位置づけ

| 項目 | 内容 |
| --- | --- |
| シーン | `Assets/Scenes/title_scene.unity` |
| 目的 | タイトルの UI 階層・スロット開始・クラフト遷移ローディング・演出・SE を正本化する |
| 関連 | 基本仕様のタイトル／セーブ、`KomayamaCraft_詳細仕様_指示差分と数値.md` §8、セーブ契約、多言語詳細仕様 |
| 値の書き方 | **確定**／**現行値**（Inspector） |

---

## 1. 画面構成

### 1.1 ルート Canvas 群

メイン UI は `Canvas` 直下。基本ボタンは **実行時生成せずヒエラルキーに置く**（雛形は `CreditsButton` 系の枠＋`HoverOverlay`）。設定・スロット・ロードは **それぞれ専用 Canvas**（`ConfigCanvas` / `SlotCanvas` / `LoadCanvas`）で開閉する。

| オブジェクト | 役割 | 区分 |
| --- | --- | --- |
| `Canvas` | タイトル本体（背景・駒山・入口ボタン等） | 確定 |
| `CanvasBG` | 背景 UI（`title_bg`） | 現行 |
| `TitleStarTwinkle` | 星明滅オーバーレイ親 | 確定 |
| `unit-big-k-Object` | 駒山キャラ（遊泳・瞬き） | 確定 |
| `kougu-Object` | 工具の8の字漂い（§8） | 確定 |
| `TitleLogoObject` 等 | ロゴ | 現行 |
| `NewGameButton` | はじめから（押下で SlotCanvas＋`ConfigToggle` SE） | 確定 |
| `ContinueButton` | 続きから（セーブ無しなら非表示。押下で SlotCanvas＋`ConfigToggle` SE） | 確定 |
| `ConfigButton` | 設定（`ConfigCanvas`）を開く | 確定 |
| `_ConfigButton` | 旧設定ボタン。非アクティブ保持可 | 現行 |
| `CreditsButton` | Credits | 確定 |
| `EndButton` | 終了。`AppQuit` SE 再生完了後に Quit／Editor は Play 停止 | 確定 |
| `ConfigCanvas` | 設定 UI（通常非表示） | 確定 |
| `SlotCanvas` | スロット UI（通常非表示）。開閉は設定と同様。`ConfirmPanel` も配下 | 確定 |
| `LoadCanvas` | クラフト遷移ローディング（通常非表示） | 確定 |
| `CreditsCanvas` | Credits（通常非表示） | 現行 |
| `TitleSeManager` | タイトル SE（§10） | 確定 |

ロジックホスト:

| オブジェクト | スクリプト |
| --- | --- |
| `TitleSceneController` | `KomayamaTitleEntry`（はじめから／続きから／スロット・確認・SE） |
| `LoadCanvas` | `KomayamaCraftSceneLoader` |
| `TitleTransitionManager` | 設定・Credits 開閉、End、クラフト遷移演出（SE＋フェード） |
| `ConfigCanvas` | `TitleConfigPanelController`（タブ・一般・キー表示） |
| `TitleSeManager` | `TitleSeManager` |
| `TtitleEffectManager`（表記現行） | `TitleEffectManager`（遊泳・瞬き・スポット・HoverOverlay） |
| `TitleStarTwinkle` | `TitleBackgroundStarTwinkle` |
| `kougu-Object` | `TitleKouguDrift` + `TitleKouguSpritePicker` |

### 1.2 SlotCanvas（確定）

`ConfigCanvas` と同じ置き方。ルートに Canvas を立て、中にパネルを置く。入口ボタンはメイン `Canvas` に残す。

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 配置 | シーン直下の `SlotCanvas`（`Canvas` の子ではない） | 確定 |
| 初期状態 | **非アクティブ** | 確定 |
| 描画 | `Screen Space - Camera` ＋ `FixedAspectCanvasFitter`（Config と同系統） | 確定 |
| 直下 | `CanvasBG`（暗転）／`SlotPanel`（本体）／`ConfirmPanel`（確認・通常非表示） | 確定 |
| 開閉 | `KomayamaTitleEntry.slotCanvas` を `SetActive`。フォールバックで `slotPanel` のみ開閉も可 | 確定 |
| 表示中の入口 | **タイトルボタンは消さない**（はじめから／続きから等を隠さない） | 確定 |

`SlotPanel` 配下（3層＋スロットOj）:

| オブジェクト | 役割 | 区分 |
| --- | --- | --- |
| `SlotPanelTitle` | 見出し「はじめから」／「続きから」 | 確定 |
| `SlotPanel1` | 背景用（`save_slot-bk`） | 確定 |
| `SlotNOj1` / `ScreenshotImage` | スクショ層（N=1..3） | 確定 |
| `SlotPanel2` | スクショ透過用（`save_slot`） | 確定 |
| `SlotNOj2` / `SlotNButton` | 選択ホバー。**SlotPanel3 の下**。Rect／色はシーン調整を尊重。プレイ中 α=0、ホバーで設定色α | 確定 |
| `SlotPanel3` | スロット透過用（`save_slot-front`）。raycast ON。透過は **alphaHitTestMinimumThreshold** で下へ貫通 | 確定 |
| `SlotNOj3` | 名前／セーブ時刻／プレイ時間／削除ボタン | 確定 |
| `SlotBackButton` / `SlotNDeleteButton` | `HoverOverlay` + `TitleEffectManager` バインド | 確定 |
| カード制御 | 各スロットに `KomayamaTitleSlotCard`（表示・ホバー・削除要求） | 確定 |

確認ダイアログ（`SlotCanvas` 直下）:

| オブジェクト | 役割 | 区分 |
| --- | --- | --- |
| `ConfirmPanel` | 初期化確認／削除確認（文言切替）。**`SlotPanel` より手前の兄弟** | 確定 |
| `ConfirmYesButton` / `ConfirmNoButton` | はい／キャンセル。`HoverOverlay`＋bindings。SE は §10 | 確定 |

実装注意（確定）:

| 項目 | 内容 |
| --- | --- |
| 移設・複製時の scale | 非アクティブな Screen Space - Camera Canvas のルート scale は **0** のままシリアライズされやすい。その親へ子を移すと **子の localScale も 0 に潰れる**。移設後は `SlotPanel` を `(1,1,1)` に戻す |
| Rect／色 | ユーザーが Inspector で合わせた Slot の Rect・色はコードで上書きしない |
| Hierarchy 順 | 見た目の兄弟順はユーザー調整。エージェントは「整理依頼」または事前確認なしに並べ替えない |

---

## 2. はじめから／続きから

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 入口 | `NewGameButton`（はじめから）／`ContinueButton`（続きから） | 確定 |
| 入口 SE | 押下時 **`ConfigToggle`**（設定開閉と同じ SE） | 確定 |
| 処理 | `KomayamaTitleEntry`（Inspector 参照。`slotCanvas` / `slotPanel` / `slotCards` / `titleTransitionManager` / `titleSeManager`） | 確定 |
| スロット表示 | **`SlotCanvas` を表示**（中の `SlotPanel`）。タイトル入口ボタンは残す | 確定 |
| `SlotPanelTitle` | はじめから時は **「はじめから」**、続きから時は **「続きから」** | 確定 |
| はじめから・空き | スロット選択 → 即 `RequestNewGame` → 遷移演出 → §3 ローダ（確認なし・`ConfirmOpen` なし） | 確定 |
| はじめから・使用中 | スロット選択 → **`ConfirmOpen` SE** → ConfirmPanel（初期化確認）→ YES（`ConfirmYes` は任意・遷移 SE と重なるため通常 null）→ `RequestNewGame` → 遷移演出 → §3 | 確定 |
| 初期化確認文言 | 「スロットNにはセーブデータがあります。／初期化してこのスロットで始めますか？」／はい「はじめる」 | 確定 |
| 続きからボタン | **いずれかのスロットにデータがあるときだけ表示**。全スロット未使用なら **非表示**（灰ボタンにしない） | 確定 |
| 続きから選択 | 使用中スロットのみ選択可 → `RequestContinue` → 遷移演出 → §3（確認なし） | 確定 |
| 前回マーク | 前回プレイしたスロット名に **「前回のプレイ」** を付記（マウス前提。フォーカス初期選択はしない） | 確定 |
| 削除ボタン | 押下で **`SlotDelete` SE** → ConfirmPanel（削除確認） | 確定 |
| 削除 YES | **`ConfirmDeleteYes` SE**（削除ボタン SE とは別）→ スロットデータ削除 | 確定 |
| 確認キャンセル | **`ConfirmCancel` SE** → ConfirmPanel を閉じる | 確定 |
| SlotBack | **`ConfigToggle` SE** → SlotCanvas を閉じる | 確定 |
| 遷移演出 | `TitleTransitionManager.BeginCraftSceneTransition`（**TransitionStart SE**＋待機＋フェードアウト）。完了後に `KomayamaCraftSceneLoader` | 確定 |
| 削除後 | 全スロットが空になったら「続きから」を再び非表示 | 確定 |
| 配置 | 入口ボタンは **メイン Canvas 直下**。実行時生成しない | 確定 |

---

## 3. クラフトシーン遷移ローディング（LoadCanvas）

### 3.1 方針（確定）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 置き場所 | **タイトルシーンの `LoadCanvas`**（専用ロードシーンは作らない） | 確定 |
| 理由 | 入口がタイトル→クラフト中心のため、シーン追加の得が小さい。共通化したくなったら先にプレハブ化を検討 | 確定 |
| 非同期 | `SceneManager.LoadSceneAsync` | 確定 |
| 表示期間 | 遷移開始で表示 → ゲームシーンがロード完了するまで表示継続 → 非表示して破棄 | 確定 |
| ゲーム時間 | ロード中は止め、完了後に進める（`KomayamaCraftLoadGate` + `KomayamaGameClock`） | 確定 |

### 3.2 処理手順

1. スロット確定後、先に **§2 の遷移演出**（TransitionStart SE＋フェード）。
2. フェード完了後、`KomayamaCraftSceneLoader.LoadCraftScene(craftSceneName)`（同期 `LoadScene` は使わない）。
3. `KomayamaCraftLoadGate.BeginHold()` で `timeScale = 0`。
4. `LoadCanvas` を表示。シーン跨ぎのため `Screen Space - Overlay` に切り替え、`DontDestroyOnLoad`。
5. `LoadSceneAsync`（`allowSceneActivation` は progress≥0.9 のあと true）。
6. シーン完了後、Awake／Start（セーブ適用など）待ちで **数フレーム**待機（現行既定 2）。
7. `KomayamaCraftLoadGate.ReleaseHold()` → `KomayamaGameClock.ApplyToUnity()` でゲーム時間再開。
8. `LoadCanvas` を非表示にして破棄。

### 3.3 実装・配置

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| スクリプト | `Assets/Scripts/title/KomayamaCraftSceneLoader.cs` | 確定 |
| 演出 | `TitleTransitionManager.BeginCraftSceneTransition` | 確定 |
| 時間ゲート | `Assets/Scripts/komayama_craft_scene/KomayamaCraftLoadGate.cs` | 確定 |
| GameClock | `HoldGameTime` 中は `ApplyToUnity` でも `timeScale=0` を維持 | 確定 |
| 初期状態 | タイトル起動時 `LoadCanvas` は **非アクティブ** | 確定 |
| フォールバック | `titleTransitionManager` 未設定時は演出なしでローダ直呼び。ローダ未配置時のみ同期 `LoadScene`（警告ログ） | 確定 |

### 3.4 非スコープ（いまやらない）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 専用ロードシーン | 不採用（§3.1） | 確定 |
| 進捗バー／Tips／最低表示時間 | 待ちが体感で重い段階まで後回し | 確定 |
| タイトル以外からの同一 UI 共通化 | 必要になったらプレハブ化を先に検討 | 確定 |

---

## 4. 設定（ConfigCanvas）

### 4.1 入口

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 現行入口 | `ConfigButton` | 確定 |
| 動作 | `TitleTransitionManager.OnClickConfigOpenButton`（Button onClick 永続＋`configOpenButton` 参照） | 確定 |
| 開閉 SE | **`ConfigToggle`** | 確定 |
| 旧 | `_ConfigButton`（非アクティブ）。配線は新 `ConfigButton` 側 | 確定 |
| 閉じる | `EXITButton` → Config 非表示（同様に `ConfigToggle`） | 確定 |

### 4.2 構成（シーン現行 → 仕様）

| オブジェクト | 役割 | 区分 |
| --- | --- | --- |
| `ConfigCanvas` | 設定 UI ルート（通常非表示。開閉は `TitleTransitionManager`） | 確定 |
| `ConfigFieldImage` | パネル背景。**raycastTarget OFF**（タブクリックを奪わない） | 確定 |
| `ConfigFieldTabGeneral` / `Volume` / `Key` | タブ。子に装飾 `Image`／`ImageFilter`（raycast OFF）と **`ImageSelected`** | 確定 |
| `ImageSelected` | 選択中タブのみ active。初期は非表示 | 確定 |
| `PanelGeneral` | 一般設定本体 | 確定 |
| `PanelVolume` | 音量設定本体 | 確定 |
| `PanelKey` | キー設定（**表示のみ**。変更・セーブ・ゲーム内ショートカットは未実装） | 確定 |

### 4.3 タブ切替（確定）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 制御 | `TitleConfigPanelController` | 確定 |
| 既定 | 開いたとき **一般タブ選択**。`PanelGeneral` ＋ General の `ImageSelected` のみ表示 | 確定 |
| 切替 | タブ押下で対応パネルのみ表示（一般／音量／キー）＋選択中の `ImageSelected` のみ active | 確定 |
| 再押下 | 選択中タブは **押しても何もしない**（SE も鳴らさない）。子 `ImageSelected` の raycast だけでは親 Button にバブルするため、**`currentTab` 判定で無視** | 確定 |
| タブ SE | 未選択タブ押下時 **`ConfigTabSwitch`** | 確定 |
| 装飾 raycast | タブ子の `Image`／`ImageFilter` は raycast OFF（親 Button へクリックを通す） | 確定 |
| Hierarchy 順 | タブと背景の兄弟順は **ユーザー調整のまま**。コードで並べ替えない | 確定 |
| 選択見た目の範囲 | **`ImageSelected` の表示切替まで**。追加の色変更・アニメはしない | 確定 |
| Esc 閉じ | Config／Slot を Esc で閉じる挙動は **いまはやらない**（設計対象外） | 確定 |

### 4.4 PanelVolume（音量）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 項目 | マスター／BGM／SE（各 ± と数値表示） | 確定 |
| 範囲 | 0〜20（既存） | 確定 |
| 永続化 | `SoundSettingsManager` → `playerData.json`（`master` / `bgm` / `se`） | 確定 |
| SE | ± 押下で `ConfigVolumeUp`／`ConfigVolumeDown` | 確定 |
| 現行実装 | `TitleSceneController` が ± ボタンを配線済み | 現行 |

### 4.5 PanelGeneral（一般）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 使用言語 | TMP Dropdown。表示は **日本語 / English** のみ切替。選択した **言語 ID を保存**。**ゲーム内の実際の表記はまだ変えない**（辞書解決・即時文言反映は後日） | 確定 |
| 言語 ID | 公式 `ja` / `en`（多言語詳細仕様と一致） | 確定 |
| 言語既定 | 未保存時は `ja` | 確定 |
| 非アクティブの定義 | **ウィンドウのフォーカス喪失／最小化**のみ（Config 開閉は含めない） | 確定 |
| 非アクティブ時にポーズ | チェック。対象は **`komayama_craft_scene` のゲーム中**（放置稼ぎを許すかはプレイヤー判断）。**既定 OFF**＝非アクティブでもゲーム時間が進む | 確定 |
| 非アクティブ時に音を鳴らす | チェック。**既定 OFF**＝今どおりフォーカス喪失でミュート | 確定 |
| セレクト見た目 | 当面 TMP Dropdown。後から素材で差し替え可 | 確定 |
| チェック行の操作 | チェックボックス本体に加え、**行の当たり判定（文言含む）クリックでも切替**。見た目は `Assets/Sprites/menu/menu_sq.png` / `menu_chk.png` | 確定 |
| 現行ヒエラルキー | `PanelGeneral` に言語 Dropdown・非アクティブ系 Toggle＋`HitArea` | 現行 |

### 4.5b PanelKey（キー・表示のみ）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 目的 | 設定画面のキー一覧 UI のみ。**キー変更・セーブ・ゲーム内ショートカットはまだしない** | 確定 |
| 表示（既定） | 建築 `R`／編集 `T`／スキル `F`／ドロップ `Space`／アイテム切り替え `E` | 確定 |
| デフォルトに戻す | ボタンあり。現状は表示を既定値に戻すのみ（セーブなし） | 確定 |
| 実装 | `TitleConfigPanelController`（`ConfigFieldTabKey` ↔ `PanelKey`） | 確定 |

### 4.6 永続化（追加フィールド）

設定値は **セーブスロットとは別の全体セーブ**（`playerData.json` / `SoundSettingsManager`）に持つ。スロット1〜3の進行データとは共有しない。

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 保存単位 | **全体（プレイヤー共通）**。スロット毎ではない | 確定 |
| 保存タイミング | **変更した瞬間に即保存**（タブ切替や Config を閉じるのを待たない） | 確定 |
| 既存音量 | −／＋のたびに既に即保存している（同方針を一般設定にも適用） | 現行／確定 |

追加フィールド:

| フィールド | 型 | 既定 | 意味 | 区分 |
| --- | --- | --- | --- | --- |
| `languageId` | string | `"ja"` | 選択言語 ID（表示切替用。文言本体は未反映） | 確定 |
| `pauseWhenInactive` | bool | `false` | 非アクティブ時にクラフトをポーズ | 確定 |
| `playAudioWhenInactive` | bool | `false` | 非アクティブ時も音を出す（false ならミュート） | 確定 |

### 4.7 既存との差分（実装メモ）

| 項目 | 現状 |
| --- | --- |
| タブ | `TitleConfigPanelController`。`ImageSelected` 表示＋`currentTab` で再押下無視 |
| 音量 | 動作済み（`TitleSceneController` + `SoundSettingsManager`） |
| フォーカス時オーディオ | `playAudioWhenInactive`（既定 false）で制御 |
| フォーカス時ポーズ | `KomayamaCraftInactiveFocusController` が `pauseWhenInactive` を見て Push/Pop |
| 言語 | Dropdown で ID 保存のみ（文言未反映） |
| 終了 | `EndButton` → Quit。タイトルにスロット／進行セーブは無い（設定の軽い永続化と Steam 終了のみ） |

---

## 5. HoverOverlay（白く光る）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 仕組み | ボタン子の `HoverOverlay`（本体と同スプライト重ね）＋ `UI/Title/HoverAdditiveOverlay` | 確定 |
| 制御 | `TitleEffectManager`（`HoverOverlayEffectManagerBase`）の `hoverOverlayBindings` | 確定 |
| 対象（現行） | NewGame／Continue／Config／Credits／End／各 EXITButton／SlotBack／各 SlotDelete／**ConfirmYes／ConfirmNo** | 確定 |
| `MaskImage` | ホバー発光とは**無関係**。無効のままでよい | 確定 |
| 注意 | ボタン差し替え後は bindings の Missing を残さない | 確定 |

---

## 6. 駒山（`unit-big-k-Object`）

### 6.1 宇宙遊泳（Lissajous・手法 C）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 移動 | 中心（起動時の `anchoredPosition`）＋ X/Y 別周期の正弦 | 確定 |
| 回転 | `unit-big-k-Image` を時計回りにゆっくり（Unity +Z 反時計のため角速度はマイナス適用） | 確定 |
| 旧 Low バウンス | **廃止**（遊泳に置換） | 確定 |
| Mid／High | ホバー見回し・クリックジャンプは維持。遊泳は併存 | 確定 |
| 実装 | `TitleEffectManager` | 確定 |

現行値の目安（Inspector 優先）:

| 項目 | 現行値 |
| --- | --- |
| 振幅 X / Y | 48 / 32 |
| 周期 X / Y 秒 | 17 / 11 |
| 回転 °/秒 | 6 |

### 6.2 瞬き

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 開眼 | `title_komayama` | 確定 |
| 閉眼 | `title_komayama-close` | 確定 |
| 間隔 | 約 3.2〜7.5 秒（乱数）。閉眼約 0.1 秒。低確率で二連続 | 確定（頻度は調整可） |

### 6.3 クリック反応

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 対象 | `unit-big-k-Image`（raycast 有効） | 確定 |
| 抽選 | **回転 30%**／**耳尻尾ピコピコ 70%**（同一クールダウン） | 確定 |
| 回転演出 | 上下約 20px 縦揺れ → 遊泳回転と逆へ 1 回転（約 1.2 秒）。この間遊泳中断 | 確定 |
| 耳尻尾演出 | `title_komayama_tail_down_ears_tight` ⇔ `title_komayama_tail_up_ears_open` を **3 回**繰り返し、元画像へ戻る。**遊泳は継続** | 確定 |
| 再クリック | 演出中は受け付けない | 確定 |
| クールダウン | 演出終了後 **3 秒**（どちらの演出でも同じ） | 確定 |

---

## 7. 背景星の明滅

背景 `title_bg` は1枚絵のため、`Canvas/TitleStarTwinkle` 下に星オーバーレイを重ねる。

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 個数 | **5** | 確定 |
| タイミング | `Time.unscaledTime` のみ。再生のたび同じ | 確定 |
| ばらつき | 星ごとに周期・位相・min/max α を変える（最初から暗い／長い周期／短い周期など） | 確定 |
| 実装 | `TitleBackgroundStarTwinkle` | 確定 |
| スプライト | `Assets/Sprites/title/title_star_twinkle.png` | 現行 |

周期の現行例: 8.5 / 14.5 / 5.5 / 18 / 7 秒。位置は Inspector で背景の実星に合わせる。

---

## 8. 工具（`kougu-Object`）

### 8.1 漂い

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 対象 | 子 `Image`（初期スプライトはシーンで張ったもの＝デフォルト） | 確定 |
| 軌道 | **8 の字**（一定角速度）。右上↔左下向き（軸 -45°） | 確定 |
| 周期 | 約 **120 秒**で初期位置に戻る | 確定 |
| 回転 | 一定速度・時計回り。駒山（既定 6°/秒）より遅い（既定 **2°/秒**） | 確定 |
| 速度変化 | **しない** | 確定 |
| 実装 | `TitleKouguDrift`（`kougu-Object` に付与） | 確定 |

### 8.2 画像バリエーション（確定）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| デフォルト | シーンで最初に張った `Image.sprite`（現行 `title_kougu`） | 確定 |
| 通常群 | `Assets/Sprites/title/title_kougu-{n}.png`（現行 1〜5）。群内は均等ランダム | 確定 |
| レア群 | `Assets/Sprites/title/title_kougu-e-{n}.png`（現行 1〜9）。群内は均等ランダム | 確定 |
| セーブ無し | **必ずデフォルト**（初期表示・クリックとも） | 確定 |
| セーブ有り | **20%** デフォルト／**50%** 通常群／**30%** レア群 | 確定 |
| クリック | 子 `Image/HitArea`（透明・Image と同サイズ追従）で判定。見た目 `Image` は raycast OFF。漂い・回転に合わせて当たる。押下で差し替え | 確定 |
| クールダウン | クリック後 **0.2 秒**（`unscaledTime`） | 確定 |
| 実装 | `TitleKouguSpritePicker`（`kougu-Object`）＋子 `HitArea` / `Image` | 確定 |

### 8.3 デバッグ：クリック範囲ホバーカーソル（確定）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 対象 | `kougu-Object/Image/HitArea` と `unit-big-k-Object/unit-big-k-Image` | 確定 |
| 挙動 | 押せる範囲にマウスがあるとき、デバッグ用カーソルへ変更 | 確定 |
| 切替 | `TitleDebugManager.debugClickableHoverCursor`（Inspector） | 確定 |
| 本番 | **`masterProductionReleaseBuild` が ON なら必ず無効**（フラグ ON でも無効） | 確定 |
| 実装 | `TitleClickableHoverCursorDebug` | 確定 |

---

## 9. 終了（EndButton）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 入口 | `EndButton` | 確定 |
| 動作 | `TitleTransitionManager.OnClickEndButton` → `AppQuit` SE → **再生完了後**に終了 | 確定 |
| ビルド | `Application.Quit()` | 確定 |
| Editor | Play モード停止 | 確定 |
| 遷移中 | `isTransitioning` 中は無視（終了処理中も二重押し防止） | 確定 |
| クリップ未設定 | SE なしで即終了 | 確定 |
| セーブ | タイトルにスロット／進行セーブは無い。Quit 時に動き得るのは設定の軽い永続化（`SoundSettingsManager`）と Steam 終了処理のみ | 確定 |

---

## 10. タイトル SE（`TitleSeManager`）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 配置 | シーン直下 `TitleSeManager` | 確定 |
| 再生 | `TitleSeCue` ごとクリップ／AudioSource／基準音量。未設定クリップは鳴らさない | 確定 |
| 呼び出し | `TitleSeManager.TryPlay`（破棄済み参照でも例外にしない。C# の `?.` は使わない）。長さ付き overload で終了待ちに使用 | 確定 |

| Cue | タイミング | 区分 |
| --- | --- | --- |
| `TransitionStart` | クラフト／旧 Game 遷移のフェード前 | 確定 |
| `ConfigToggle` | Config／Credits 開閉、**はじめから／続きから**押下、**SlotBack** | 確定 |
| `ConfigVolumeUp` / `ConfigVolumeDown` | 音量 ± | 確定 |
| `ConfigTabSwitch` | 設定タブ押下（未選択→選択へ切替時） | 確定 |
| `SlotDelete` | スロット削除ボタン押下（確認オープン時） | 確定 |
| `ConfirmOpen` | **スロット選択**で確認が開くときのみ（はじめから・使用中）。即遷移時は鳴らさない | 確定 |
| `ConfirmYes` | 初期化確認の YES。**項目は残す**が、直後の `TransitionStart` と重なるため **通常は null（意図的）** | 確定 |
| `ConfirmDeleteYes` | 削除確認の YES（`SlotDelete` とは別） | 確定 |
| `ConfirmCancel` | 確認のキャンセル（No） | 確定 |
| `AppQuit` | EndButton。再生完了後にアプリ終了 | 確定 |

---

## 11. 改訂履歴

| 日付 | 内容 |
| --- | --- |
| 2026-09-21 | タブ見た目は ImageSelected のみ（色・アニメ追加なし）。Config／Slot の Esc 閉じは対象外 |
| 2026-09-21 | ConfirmYes は項目維持・通常 null（遷移 SE と重複回避）。前回のプレイ表記。SlotBack=ConfigToggle。AppQuit 再生完了後終了 |
| 2026-09-21 | §2/§5/§9/§10：入口 ConfigToggle、Confirm・削除・タブ SE、EndButton Quit、ImageSelected／再押下、Hover に Confirm、TryPlay。§3 手順に SE＋フェードを明記 |
| 2026-09-21 | ConfirmPanel を SlotCanvas 配下へ移設（SlotPanel より手前） |
| 2026-09-21 | はじめから／続きから：TransitionStart SE＋フェード後に LoadCanvas。既存スロットは初期化確認 |
| 2026-09-21 | PanelKey／ConfigFieldTabKey：キー表示のみ＋デフォルトに戻す（変更・セーブ・ショートカット未実装） |
| 2026-09-21 | §4.6：設定は全体セーブ・変更瞬間に即保存を明記 |
| 2026-09-21 | §4.3〜4.7：言語は ID 保存のみ／非アクティブ定義・ポーズ対象・既定値を確定 |
| 2026-09-21 | §4 を ConfigCanvas タブ（一般／音量／キー）仕様に拡張 |
| 2026-09-21 | kougu クリックを HitArea 分離。TitleDebugManager にクリック範囲ホバーカーソル（本番では必ず無効） |
| 2026-09-21 | kougu 画像バリエーション：セーブ無しはデフォルト固定。有りは 20/50/30。クリック CD 0.2s |
| 2026-09-20 | §1 を Canvas 群／SlotCanvas 詳細に再整理。続きから非表示・移設時 scale0 注意・`KomayamaTitleSlotCard` を確定反映 |
| 2026-09-20 | 全スロット未使用なら「続きから」ボタンを非表示 |
| 2026-09-20 | スロット UI を `SlotCanvas` 配下へ（ConfigCanvas と同様の開閉） |
| 2026-09-20 | LoadCanvas＋非同期クラフト遷移を確定。専用ロードシーンは不採用。スロット見出し「はじめから／続きから」、選択中もタイトルボタン維持 |
| 2026-09-20 | SlotPanel を背景／スクショ透過／スロット透過の3層に再解釈。Slot1Button ホバー色、削除確認、Back/Delete の HoverOverlay |
| 2026-09-20 | kougu：一定速度の8の字漂い＋ゆっくり回転（120秒周期） |
| 2026-09-20 | クリック抽選：回転30%／耳尻尾ピコピコ70%（遊泳継続）。CD共通 |
| 2026-09-20 | 駒山クリック：縦揺れ20px→逆1回転1.2s、遊泳中断、終了後CD3s |
| 2026-09-20 | 初版。Canvas 直下ボタン、HoverOverlay 再配線、Lissajous 遊泳＋瞬き、星明滅5、ConfigButton |
