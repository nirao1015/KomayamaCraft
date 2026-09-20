# KomayamaCraft タイトル画面 詳細仕様

> **状態**：実装反映（2026-09-20）。見た目の位置・周期の微調整は Inspector 現行値を優先。

---

## 0. 文書の位置づけ

| 項目 | 内容 |
| --- | --- |
| シーン | `Assets/Scenes/title_scene.unity` |
| 目的 | タイトルの UI 階層・スロット開始・クラフト遷移ローディング・演出を正本化する |
| 関連 | 基本仕様のタイトル／セーブ、`KomayamaCraft_詳細仕様_指示差分と数値.md` §8、セーブ契約 |
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
| `NewGameButton` | はじめから | 確定 |
| `ContinueButton` | 続きから（§2：セーブ無しなら非表示） | 確定 |
| `ConfigButton` | 設定（`ConfigCanvas`）を開く | 確定 |
| `_ConfigButton` | 旧設定ボタン。非アクティブ保持可 | 現行 |
| `CreditsButton` | Credits | 確定 |
| `EndButton` | 終了系（シーン現行） | 現行 |
| `ConfirmPanel` | 上書き／削除確認（現行はメイン Canvas 直下） | 確定 |
| `ConfigCanvas` | 設定 UI（通常非表示） | 確定 |
| `SlotCanvas` | スロット UI（通常非表示）。開閉は設定と同様 | 確定 |
| `LoadCanvas` | クラフト遷移ローディング（通常非表示） | 確定 |
| `CreditsCanvas` | Credits（通常非表示） | 現行 |

ロジックホスト:

| オブジェクト | スクリプト |
| --- | --- |
| `TitleSceneController` | `KomayamaTitleEntry`（はじめから／続きから／スロット開閉） |
| `LoadCanvas` | `KomayamaCraftSceneLoader` |
| `TitleTransitionManager` | 設定・Credits 開閉、旧 Game 遷移参照など |
| `TtitleEffectManager`（表記現行） | `TitleEffectManager`（遊泳・瞬き・スポット・HoverOverlay） |
| `TitleStarTwinkle` | `TitleBackgroundStarTwinkle` |
| `kougu-Object` | `TitleKouguDrift` |

### 1.2 SlotCanvas（確定）

`ConfigCanvas` と同じ置き方。ルートに Canvas を立て、中にパネルを置く。入口ボタンはメイン `Canvas` に残す。

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 配置 | シーン直下の `SlotCanvas`（`Canvas` の子ではない） | 確定 |
| 初期状態 | **非アクティブ** | 確定 |
| 描画 | `Screen Space - Camera` ＋ `FixedAspectCanvasFitter`（Config と同系統） | 確定 |
| 直下 | `CanvasBG`（暗転）／`SlotPanel`（本体） | 確定 |
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

確認ダイアログ:

| オブジェクト | 役割 | 区分 |
| --- | --- | --- |
| `ConfirmPanel` | 上書き確認／削除確認（文言切替）。現行は **メイン `Canvas` 直下**（`SlotCanvas` 外） | 確定 |

実装注意（確定）:

| 項目 | 内容 |
| --- | --- |
| 移設・複製時の scale | 非アクティブな Screen Space - Camera Canvas のルート scale は **0** のままシリアライズされやすい。その親へ子を移すと **子の localScale も 0 に潰れる**。移設後は `SlotPanel` を `(1,1,1)` に戻す |
| Rect／色 | ユーザーが Inspector で合わせた Slot の Rect・色はコードで上書きしない |

---

## 2. はじめから／続きから

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 入口 | `NewGameButton`（はじめから）／`ContinueButton`（続きから） | 確定 |
| 処理 | `KomayamaTitleEntry`（Inspector 参照。`slotCanvas` / `slotPanel` / `slotCards`） | 確定 |
| スロット表示 | **`SlotCanvas` を表示**（中の `SlotPanel`）。タイトル入口ボタンは残す | 確定 |
| `SlotPanelTitle` | はじめから時は **「はじめから」**、続きから時は **「続きから」** | 確定 |
| はじめから | スロット選択 → 空きはそのまま／使用中は上書き確認 → `KomayamaBootRequest.RequestNewGame` → §3 ローダ → `komayama_craft_scene` | 確定 |
| 続きからボタン | **いずれかのスロットにデータがあるときだけ表示**。全スロット未使用なら **非表示**（灰ボタンにしない） | 確定 |
| 続きから選択 | 使用中スロットのみ選択可 → `RequestContinue` → §3 ローダ | 確定 |
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

1. スロット確定後、`KomayamaCraftSceneLoader.LoadCraftScene(craftSceneName)` を呼ぶ（同期 `LoadScene` は使わない）。
2. `KomayamaCraftLoadGate.BeginHold()` で `timeScale = 0`。
3. `LoadCanvas` を表示。シーン跨ぎのため `Screen Space - Overlay` に切り替え、`DontDestroyOnLoad`。
4. `LoadSceneAsync`（`allowSceneActivation` は progress≥0.9 のあと true）。
5. シーン完了後、Awake／Start（セーブ適用など）待ちで **数フレーム**待機（現行既定 2）。
6. `KomayamaCraftLoadGate.ReleaseHold()` → `KomayamaGameClock.ApplyToUnity()` でゲーム時間再開。
7. `LoadCanvas` を非表示にして破棄。

### 3.3 実装・配置

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| スクリプト | `Assets/Scripts/title/KomayamaCraftSceneLoader.cs` | 確定 |
| 時間ゲート | `Assets/Scripts/komayama_craft_scene/KomayamaCraftLoadGate.cs` | 確定 |
| GameClock | `HoldGameTime` 中は `ApplyToUnity` でも `timeScale=0` を維持 | 確定 |
| 初期状態 | タイトル起動時 `LoadCanvas` は **非アクティブ** | 確定 |
| フォールバック | ローダ未配置時のみ同期 `LoadScene`（警告ログ） | 確定 |

### 3.4 非スコープ（いまやらない）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 専用ロードシーン | 不採用（§3.1） | 確定 |
| 進捗バー／Tips／最低表示時間 | 待ちが体感で重い段階まで後回し | 確定 |
| タイトル以外からの同一 UI 共通化 | 必要になったらプレハブ化を先に検討 | 確定 |

---

## 4. 設定ボタン

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 現行入口 | `ConfigButton` | 確定 |
| 動作 | `TitleTransitionManager.OnClickConfigOpenButton`（Button onClick 永続＋`configOpenButton` 参照） | 確定 |
| 旧 | `_ConfigButton`（非アクティブ）。配線は新 `ConfigButton` 側 | 確定 |

---

## 5. HoverOverlay（白く光る）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 仕組み | ボタン子の `HoverOverlay`（本体と同スプライト重ね）＋ `UI/Title/HoverAdditiveOverlay` | 確定 |
| 制御 | `TitleEffectManager`（`HoverOverlayEffectManagerBase`）の `hoverOverlayBindings` | 確定 |
| 対象（現行） | NewGame／Continue／Config／Credits／End／各 EXITButton／SlotBack／各 SlotDelete | 現行 |
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

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 対象 | 子 `Image`（`title_kougu`） | 確定 |
| 軌道 | **8 の字**（一定角速度）。右上↔左下向き（軸 -45°） | 確定 |
| 周期 | 約 **120 秒**で初期位置に戻る | 確定 |
| 回転 | 一定速度・時計回り。駒山（既定 6°/秒）より遅い（既定 **2°/秒**） | 確定 |
| 速度変化 | **しない** | 確定 |
| 実装 | `TitleKouguDrift`（`kougu-Object` に付与） | 確定 |

---

## 9. 改訂履歴

| 日付 | 内容 |
| --- | --- |
| 2026-09-20 | §1 を Canvas 群／SlotCanvas 詳細に再整理。続きから非表示・移設時 scale0 注意・`KomayamaTitleSlotCard` を確定反映 |
| 2026-09-20 | 全スロット未使用なら「続きから」ボタンを非表示 |
| 2026-09-20 | スロット UI を `SlotCanvas` 配下へ（ConfigCanvas と同様の開閉） |
| 2026-09-20 | LoadCanvas＋非同期クラフト遷移を確定。専用ロードシーンは不採用。スロット見出し「はじめから／続きから」、選択中もタイトルボタン維持 |
| 2026-09-20 | SlotPanel を背景／スクショ透過／スロット透過の3層に再解釈。Slot1Button ホバー色、削除確認、Back/Delete の HoverOverlay |
| 2026-09-20 | kougu：一定速度の8の字漂い＋ゆっくり回転（120秒周期） |
| 2026-09-20 | クリック抽選：回転30%／耳尻尾ピコピコ70%（遊泳継続）。CD共通 |
| 2026-09-20 | 駒山クリック：縦揺れ20px→逆1回転1.2s、遊泳中断、終了後CD3s |
| 2026-09-20 | 初版。Canvas 直下ボタン、HoverOverlay 再配線、Lissajous 遊泳＋瞬き、星明滅5、ConfigButton |
