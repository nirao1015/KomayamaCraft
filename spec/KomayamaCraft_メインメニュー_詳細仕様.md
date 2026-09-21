# KomayamaCraft メインメニュー（menu設定）詳細仕様

> **状態**：実装済み（2026-09-21）。見た目素材（透過背景・バツ画像・ボタン枠）はシーン／Inspector で調整可。

---

## 0. 文書の位置づけ

| 項目 | 内容 |
| --- | --- |
| シーン | `Assets/Scenes/komayama_craft_scene.unity` |
| 入口 | 右端 `MenuObject / menu設定`（メニュー解禁後のみ表示・操作可） |
| 目的 | 設定ボタン押下時の「何をするか選ぶ」画面、設定パネル再利用、セーブしてタイトル／デスクトップ |
| 関連 | タイトル設定（`KomayamaCraft_タイトル画面_詳細仕様.md` §4／§10）、建設メニュー詳細（MenuObject）、セーブ契約、`KomayamaSaveService`、`KomayamaGameClock` |
| 値の書き方 | **確定**／**現行値**（Inspector） |
| セットアップ | メインメニュー：`KomayamaCraft/Craft/Setup Main Menu Scene`。設定プレハブ：`KomayamaCraft/Shared/Setup ConfigCanvas Prefab (Title+Craft)`／`Repair ConfigCanvas Prefab Scale` |

参考見た目（他作品）：中央にメインメニュー枠、画面全体の薄い暗転、右端リボン列は残る。本仕様では右端は**見た目のみ**残し操作は不可。

---

## 1. 概要

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 名称 | メインメニュー（仮。見出し文言は Inspector／TMP で調整可。例：「メインメニュー」） | 確定 |
| 入口 | `menu設定` 押下 | 確定 |
| 段階 | **①メインメニュー** →（設定時）**②設定パネル**。セーブ系は ① から直接遷移 | 確定 |
| 右端スライド | **見た目はそのまま表示継続**。メインメニュー／設定表示中は **クリック不可** | 確定 |
| フィールド操作 | 薄い透過オーバーレイで遮断（カメラ WASD・ズーム・採集・建設等）。メインメニュー表示中は入力／カメラも停止 | 確定 |
| ゲーム時間 | 表示中は `KomayamaGameClock.PushPause`／閉じたら `PopPause`（一時停止） | 確定 |
| 解禁条件 | `KomayamaQuestController.AreBuildAndSettingsUnlocked` が true のときのみ入口が有効（既存） | 確定 |

---

## 2. UI 構成

### 2.1 階層（確定）

`SystemCanvas` 配下。

| オブジェクト | 役割 | 区分 |
| --- | --- | --- |
| `MainMenuRoot` | メインメニュー全体。通常非表示 | 確定 |
| `MainMenuDim` | 全画面薄い透過背景。raycast ON でフィールド入力をブロック | 確定 |
| `MainMenuPanel` | 中央パネル本体 | 確定 |
| `MainMenuTitle` | 見出し TMP（任意） | 確定 |
| `MainMenuCloseButton` | 右上バツ。押下でメインメニューを閉じる | 確定 |
| `MainMenuSettingsButton` | 「設定」 | 確定 |
| `MainMenuSaveTitleButton` | 「セーブしてタイトル」 | 確定 |
| `MainMenuSaveQuitButton` | 「セーブしてデスクトップ」 | 確定 |
| `MainMenuController` | `KomayamaCraftMainMenuController` | 確定 |
| `CraftConfigCanvas` | 設定 UI。共通プレハブのインスタンス。通常非表示（§4） | 確定 |

右端 `MenuObject`（`ImageMenuSlide`＋各 menu）は既存のまま。メインメニュー表示中は **CanvasGroup／Selectable で操作不可**（非表示にはしない）。

### 2.2 見た目

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 暗転 | 薄い透過画像。α・スプライトは Inspector | 確定 |
| パネル枠・ボタン | シーン調整可 | 確定 |
| バツ | 右上 | 確定 |
| ボタン文言 | 「設定」「セーブしてタイトル」「セーブしてデスクトップ」 | 確定 |
| Hover | タイトル同様 `HoverOverlay`＋`KomayamaCraftHoverOverlayEffectManager`（Close／設定／セーブ2種） | 確定 |

---

## 3. 開閉・入力

| 操作 | 結果 | 区分 |
| --- | --- | --- |
| `menu設定` 押下（閉じている） | メインメニューを開く。Pause Push。右端メニュー操作不可。フィールド操作不可。SE（§6） | 確定 |
| バツ押下 | メインメニューを閉じる。Pause Pop。右端メニュー操作復帰。SE | 確定 |
| **Esc** | バツと同じ（メインメニュー表示中）。設定パネル表示中の Esc は §4 | 確定 |
| メインメニュー表示中に建設スライド等 | **反応しない** | 確定 |
| 会話／OP／修理演出中 | 既存どおりメニュー解禁・HUD 抑止規則を優先 | 確定 |

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| `menu設定` 再押下 | **トグルで閉じる**（バツ／Esc と同じ） | 確定 |

---

## 4. 設定パネル（共通プレハブ）

### 4.1 挙動（クラフト）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 内容 | タイトルと**同じ**（一般：言語 ID・非アクティブ系／音量／キー表示のみ） | 確定 |
| 永続化 | 既存 `SoundSettingsManager`（変更即保存） | 確定 |
| 開き方 | メインメニュー「設定」→ **メインメニューパネルを隠す** → 設定表示 | 確定 |
| EXIT／閉じる | **メインメニューに戻る**（設定非表示＋メイン再表示）。Pause は維持 | 確定 |
| Esc（設定表示中） | EXIT と同じ | 確定 |

### 4.2 プレハブ共有

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| プレハブ | `Assets/Prefabs/KomayamaCraft/ConfigCanvas.prefab` | 確定 |
| タイトル配置 | シーン直下 `ConfigCanvas`（Overlay・独自 Canvas） | 確定 |
| クラフト配置 | `SystemCanvas/CraftConfigCanvas`（プレハブインスタンス） | 確定 |
| クラフト描画 | **ネスト Canvas は持たない**。親 `SystemCanvas` で描画。`localScale = (1,1,1)`。全面ストレッチ | 確定 |
| 共通コンポーネント | `TitleConfigPanelController`／`ConfigVolumeUi`／`ConfigSePlayer`（＋`AudioSource`） | 確定 |
| 片側専用（タイトル） | 開閉：`TitleTransitionManager`＋`TitleSeManager`（`ConfigToggle`）。BGM 即時反映：`ConfigVolumeUi.titleBgmManager` | 確定 |
| 片側専用（クラフト） | 開閉・戻る：`KomayamaCraftMainMenuController`。BGM 参照は null | 確定 |

コピー／差し替え時は **ルート scale を 0 にしない**（`SetParent(..., worldPositionStays: false)` ＋ scale 正規化）。修理メニュー：`Repair ConfigCanvas Prefab Scale`。

### 4.3 設定パネル SE（クラフト独自なし）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 方針 | **クラフト scene 独自の設定 SE は持たない** | 確定 |
| 再生 | プレハブ上の `ConfigSePlayer` | 確定 |
| クリップ元 | タイトル `TitleSeManager` の設定系と同内容をプレハブへ同期（タブ／音量±） | 確定 |
| Cue | `ConfigTabSwitch`／`ConfigVolumeUp`／`ConfigVolumeDown` | 確定 |
| 使わないもの | クラフトの `KomayamaCraftSeManager`、クラフト用 `TitleSeManager` 参照 | 確定 |

※ メインメニュー開閉・セーブ遷移の SE は §6（別系統）。

---

## 5. セーブしてタイトル／デスクトップ

### 5.1 共通

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| セーブ | `KomayamaSaveService.TrySave` | 確定 |
| 成功判定 | `TrySave` が true | 確定 |
| 失敗時 | **遷移しない**。ステータス文言で失敗を示す。メインメニューに留まる。Pause 維持 | 確定 |
| スロットサムネ | セーブ時のキャプチャは **ゲーム画面のみ**（`Screen Space - Camera` UI／メインメニューは一時オフして撮る） | 確定 |

### 5.2 セーブしてタイトル

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 手順 | セーブ成功 → **遷移 SE＋フェードアウト** → `title_scene` をロード。**押下 SE なし**（遷移 SE のみ） | 確定 |
| SE／フェード | `TransitionToTitle`＋`FadeCanvas`（タイトル→クラフトと同系統） | 確定 |

### 5.3 セーブしてデスクトップ

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 手順 | セーブ成功 → Quit（Editor は Play 停止）。**押下 SE なし** | 確定 |
| 終了 SE | なし（押下時・終了待ち用 SE は鳴らさない） | 確定 |

---

## 6. SE（メインメニュー専用・`KomayamaCraftSeManager`）

設定パネル内 SE は §4.3。本節は **メインメニューの開閉・セーブ導線のみ**。

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| 管理 | `KomayamaCraftSeManager` | 確定 |
| 初期クリップ | 開閉系はタイトル `ConfigToggle` 同クリップ。遷移は `TransitionStart` 同クリップ | 確定 |
| 後から差し替え | Inspector で Cue ごとに個別指定 | 確定 |

| Cue | タイミング | 区分 |
| --- | --- | --- |
| `MainMenuToggle` | メインメニュー開閉（`menu設定`／バツ／Esc／トグル） | 確定 |
| `MainMenuSettings` | 「設定」押下（設定パネルを開くとき） | 確定 |
| `MainMenuSaveTitle` | （未使用）セーブしてタイトル押下用。遷移は `TransitionToTitle` のみ | 確定 |
| `MainMenuSaveQuit` | （未使用）セーブしてデスクトップ押下／終了待ち用。現状鳴らさない | 確定 |
| `TransitionToTitle` | タイトルへ戻るフェード前 | 確定 |

---

## 7. 実装メモ（配置・参照）

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| メインメニュー制御 | `Assets/Scripts/komayama_craft_scene/KomayamaCraftMainMenuController.cs` | 確定 |
| 設定プレハブ側 | `TitleConfigPanelController`／`ConfigVolumeUi`／`ConfigSePlayer`（`Assets/Scripts/title/`） | 確定 |
| 参照 | Inspector アタッチ。名前検索で自動解決しない | 確定 |
| Pause | メインメニュー開始で Push、完全に閉じたときだけ Pop。設定へ潜る間は Pop しない | 確定 |
| 入力ブロック | Dim の raycast ＋右端 CanvasGroup／Selectable。カメラ WASD も `IsOpen` で停止 | 確定 |
| 既存 Debug ポーズボタン | 別系統。本メニューとは独立 | 確定 |

---

## 8. 受け入れ（実装後）

- [x] メニュー解禁後、`menu設定` でメインメニューが開き、フィールド操作不可・ゲーム時間停止。
- [x] 右端スライドは見えるが押せない。
- [x] バツ／Esc／`menu設定` 再押下で閉じ、操作と時間が復帰する。
- [x] 「設定」でメインメニューが隠れ、タイトル同等の設定が出る。EXIT／Esc でメインメニューに戻る。
- [x] 設定パネル SE（タブ／音量±）はプレハブ `ConfigSePlayer`（タイトル同クリップ）。クラフト独自設定 SE は無い。
- [x] 「セーブしてタイトル」でセーブ後、遷移 SE＋フェードして title_scene（押下 SE なし）。
- [x] 「セーブしてデスクトップ」でセーブ成功後 Quit（押下／終了待ち SE なし）。
- [x] セーブ失敗時は遷移せずメニューに留まる。

---

## 9. 改訂履歴

| 日付 | 内容 |
| --- | --- |
| 2026-09-21 | セーブサムネは Screen Space Camera UI を一時オフしゲーム面のみキャプチャ |
| 2026-09-21 | セーブしてタイトル／デスクトップは押下 SE なし（タイトルは遷移 SE のみ、デスクトップは無音で Quit） |
| 2026-09-21 | メインメニューボタンに HoverOverlay を配線（`KomayamaCraftHoverOverlayEffectManager`） |
| 2026-09-21 | 設定 SE：クラフト独自なし／`ConfigSePlayer` に整理。§4・§6 分離。ネスト Canvas 禁止・scale 注意を明記 |
| 2026-09-21 | ConfigCanvas 共通プレハブ化。音量 UI を `ConfigVolumeUi` に統合。片側専用はホスト配線 |
| 2026-09-21 | 実装反映。シーン配線・受け入れチェック |
| 2026-09-21 | 初版。入口・UI・Pause・Esc・設定再利用・セーブしてタイトル／デスクトップ・SE Cue |
