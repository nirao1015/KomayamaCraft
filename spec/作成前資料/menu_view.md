# menu_view
-[]でくくったものはUNITY内の GameObject である
- <>でくくったものは Inspector変数 である

## 画面構成
- 背景画像[背景画像]を表示
- 画面中央上部に画像[タイトル画像]を配置
- 画面中央中部に画像[挿絵画像]を配置
- [挿絵画像]の下方向から速度線エフェクトを表示（演出用）
- 画面中央下部に[メニューUI]を配置
 * [メニューUI]内に画像[ゲームスタートボタン]を配置
 * [メニューUI]内に[ConfigButton]を配置
 * [メニューUI]内に[PlayManualButton]を配置（仕様は別途）
 * [メニューUI]内に[SoundTestButton]を配置（仕様は別途）
 * [ConfigCanvas]を初期状態で非アクティブ配置
   * [ConfigCanvasBG]（メニュー全体を半透明で覆う）
   * [ConfigPanel]
     * [ConfigTitleText]
     * [MasterRow]
       * [MasterLabelText]
       * [MasterMinusButton]
       * [MasterValueText]
       * [MasterPlusButton]
     * [BgmRow]
       * [BgmLabelText]
       * [BgmMinusButton]
       * [BgmValueText]
       * [BgmPlusButton]
     * [SeRow]
       * [SeLabelText]
       * [SeMinusButton]
       * [SeValueText]
       * [SePlusButton]
    * [ConfigExitButton]
 * [PlayManualCanvas]を初期状態で非アクティブ配置
   * [CanvasBG]（メニュー全体を半透明で覆う）
   * [Panel]
     * [PlayManualImageRoot]（遊び方画像を貼るための固定領域）
    * [PlayManualExitButton]
 * [SoundTestCanvas]を初期状態で非アクティブ配置
   * [CanvasBG]（メニュー全体を半透明で覆う）
   * [Panel]
     * [BgmSectionRoot]（BGMテストボタン群）
     * [SeSectionRoot]（SEテストボタン群）
    * [SoundTestExitButton]
 * [IllustSpeedLineRoot]（挿絵周辺の速度線描画用ルート）

## 挙動
- シーン開始時
 * 画面構成 のオブジェクトを表示

## menu_scene 遷移・オーディオ管理（実装）

### 役割分担
- **`MenuTransitionManager`**（`menu_scene` ルートの管理オブジェクト）  
  - **担当するもの:** `ConfigCanvas` / `PlayManualCanvas` / `SoundTestCanvas` の開閉、スタート・タイトル戻り・（任意の）Game02 ボタンに伴うシーン遷移とフェード、上記に付随するボタン押下演出（`MenuImageButtonPressFeedback`）。  
  - **担当しないもの:** マスター／BGM／SE の数値変更や音量ボタンのロジック（`MenuSceneController`）。上記以外の目的で Canvas の表示切替や `LoadScene` を増やす場合は、設計と重複しないか先に確認する。  
  - ボタンの **OnClick** は原則 `MenuTransitionManager` の public メソッドを参照する。`TitleReturnButton` の挙動は `menu02_scene` の同名ボタンと同様（タイトル戻り専用 SE、`SceneTransitionContext` のクリア、遷移 SE は遷移開始用とは別）。  
  - 遷移先シーン名・待機秒数・フェード・FadeCanvas プレハブ・対象 Canvas 参照は本コンポーネントの Inspector で **フラットな項目**として管理する（リスト化しない）。  
- **`MenuSeManager`**  
  - `menu_scene` 内の SE クリップと再生先 `AudioSource` をインスペクターでフラットに管理する。未設定のクリップ／ソースは **エラーにせず再生しない**。  
  - 遷移・各 Canvas 開閉の SE は主に `MenuTransitionManager` から `PlayByCue` で呼ぶ。設定パネル内の音量ボタン SE は `MenuSceneController` から同じ `MenuSeManager` を参照する。  
- **`MenuBgmManager`**  
  - メイン BGM とサウンドテスト用 BGM 試聴の `AudioClip` / `AudioSource` をインスペクターで管理する（メインと試聴で **別 AudioSource** を推奨）。未設定時は無音。  
  - `MenuSceneController` のサウンドテスト表示切替から `BeginSoundTestMode` / `EndSoundTestMode` / 試聴再生を呼ぶ。  
- **`MenuSceneController`**  
  - イラスト・スプライン、`SoundTestCanvas` のランタイム生成とリスト試聴、Config 内の音量 UI と `SoundSettingsManager` 連携を担当する。サウンドテストの EXIT はランタイム生成のため、`Awake` 時に `MenuTransitionManager.OnClickSoundTestExitButton` へリスナーを接続する。  
- **レガシー `AudioSource`（シーン上の単体 BGM 用）**  
  - `MenuBgmManager` へ移行済みのため **非アクティブ**とし、二重再生を避ける。

### `StartButton (1)`
- 見た目確認用。OnClick や参照は変更しない。

---

## menu02_scene 遷移管理（実装）
- `Menu02TransitionManager` が `menu02_scene` の Canvas開閉と画面遷移を一元管理する
- 対象ボタン
  - `TitleReturnButton`（`title_scene` へ遷移）
  - `StartButton`（`dialogue_scene` 経由で `game02_scene` へ遷移）
  - `ContinueButton`（`game02_scene` へ遷移）
  - `PlayManualButton` / `PlayManual EXITButton`（`PlayManualCanvas` の開閉）
- `ConfigCanvas` / `ConfigButton` は `menu02_scene` の遷移管理対象から除外（削除予定）
- 遷移時のSEは `Menu02SeManager` を経由して再生する
- 遷移設定（遷移先シーン名、待機秒数、フェード秒数、対象Canvas参照）は `Menu02TransitionManager` の Inspector で管理する
- `PlayManualCanvas` はシーン上で `FixedAspectCanvasFitter` を事前アタッチして運用する（実行時の自動付与に依存しない）
- `PlayManualCanvas` 側の `FixedAspectCanvasFitter.forceFrontSorting` は OFF を基本とし、表示順は Canvas の Inspector 設定を優先する

## [管理オブジェクト]での挙動制御
- シーン開始時
 * 指定のBGMを繰り返し再生開始
- [ゲームスタート画像]押下
 * game_stageシーンへ遷移
  * game_stageシーンへは <ステージ名> を渡し指定した値を利用できるようにする
  * シーンをまたいだ変数は全シーンで必要なものではなく、遷移先シーンだけで必要なもの
  * 変数数も5を超えないかつ変数の値も255文字を超えない想定で実装をしてほしい

## ConfigButton 仕様（実装済み）

### 目的
- menu_scene 内で音量設定を変更できるようにする
- 対象は `SoundSettingsManager` の以下3値
  - `masterVolume`
  - `bgmVolume`
  - `seVolume`

### 初期状態
- [ConfigCanvas] は `inactive`
- [ConfigCanvas] が非表示の間は、通常のメニュー操作のみ可能

### [ConfigButton] 押下時
- [ConfigCanvas] を `active` にする
- [ConfigCanvas] は常に他UIより前面に描画する
  - `Screen Space Overlay`
  - `overrideSorting = true`
  - `sortingOrder = 1000`
- [ConfigCanvasBG] を半透明で表示し、メニュー画面が隠れる見た目にする
- [ConfigCanvas] 表示中は、下層のメニューUIを操作不可とする
- 開く時に Config 開閉SEを再生する

### [ConfigExitButton] 押下時
- [ConfigCanvas] を `inactive` にする
- メニューUIの通常操作に復帰する
- 閉じる時に Config 開閉SEを再生する

### 音量UIの具体提案
- 各項目（Master/BGM/SE）は `-` ボタン + 数値表示 + `+` ボタンの 1 行構成
- 数値表示は `1 - 20` の整数（`SoundSettingsManager` と同じスケール）
- ボタン操作仕様
  - `Minus`: 1 ずつ減少（下限 1）
  - `Plus`: 1 ずつ増加（上限 20）
- `Minus` 押下時は音量ダウンSEを再生する
- `Plus` 押下時は音量アップSEを再生する
- 値変更時は即時 `SoundSettingsManager` に反映
  - Master: `SetMasterVolume(int)`
  - BGM: `SetBgmVolume(int)`
  - SE: `SetSeVolume(int)`
- [ConfigCanvas] 表示時に現在値を読み出して表示同期
  - `GetMasterVolume()`
  - `GetBgmVolume()`
  - `GetSeVolume()`

### 画面デザイン（実装）
- [ConfigCanvasBG]
  - 画面全体アンカー（stretch）
  - 色: 黒（単色塗り）
  - アルファは `<ConfigCanvasBgAlpha>` で調整
- [ConfigPanel]
  - 中央配置、角丸矩形背景（既存UIスプライト流用可）
  - サイズ目安: 760 x 460（FHD基準）
- 文字
  - タイトル: `CONFIG`
  - 行ラベル: `MASTER`, `BGM`, `SE`
  - 値表示: `01 - 20` の2桁表示推奨

### 入力とフォーカス
- マウスクリック操作を前提
- [ConfigCanvas] 表示中は ESC キーでも閉じる拡張を許可（任意）

### 非対象（今回）
- 設定値の永続保存（PlayerPrefs等）は今回対象外

## PlayManualButton 仕様（実装済み）

### 目的
- menu_scene 内で「遊び方」一枚絵を表示する

### 表示・閉じる
- [PlayManualButton] 押下で [PlayManualCanvas] を `active`
- [PlayManualCanvas] は [ConfigCanvas] と同じ表示ルールで最前面表示
  - `Screen Space Overlay`
  - `overrideSorting = true`
  - `sortingOrder = 1000`
- [PlayManualExitButton] 押下で [PlayManualCanvas] を `inactive`
- [PlayManualCanvas] 表示中は通常メニューUI（Start/Config/PlayManual/SoundTest）を操作不可

### 背景表示
- [CanvasBG] は黒単色 + 半透明
- アルファは `<PlayManualCanvasBgAlpha>` で調整

### 画像表示領域
- [PlayManualImageRoot] を固定オブジェクトとして配置する
- 遊び方画像はこの領域に Image を貼って運用する
- PlayManual は動的生成を行わない（Canvas/Panel/ImageRoot はシーンに事前配置）

## Illust 速度線演出（実装済み）

### 目的
- 8の字移動する [挿絵画像] に、下方向からの速度線を追加してスピード感を強化する

### 表示仕様
- 速度線は [挿絵画像] の少し下から上方向へ流れる
- 線の太さ・長さ・色・透明度は Inspector で調整可能
- 白背景でも視認しやすいよう、線色はシアン〜青紫系グラデーション + 輪郭補強を使う

### 描画順
- 速度線は [挿絵画像] の背面で描画する
- Config / PlayManual / SoundTest の各 Canvas 前面表示を邪魔しない

### パフォーマンス方針
- 速度線オブジェクトはプール再利用し、毎フレームの生成/破棄は行わない
- 発生タイミングは事前生成したパターンをループ再生する
- 発生量を挿絵移動速度に連動させる設定を許可する（任意）

## Illust 前景雲演出（実装済み）

### 目的
- [Illust] の手前に雲を高速通過させ、高空からの落下感を補強する

### 表示仕様
- `cloud01`: 8秒ごとに 1 回、画面外下から上方向へ高速移動
- `cloud02`: 13秒ごとに 1 回、画面外下から上方向へ高速移動
- どちらも前景表示のアルファは 70%（`0.7`）

### 開始位置・倍率
- 雲の開始位置は、シーン上の [cloud01] / [cloud02] オブジェクト配置位置をそのまま使う
- 雲の表示倍率（サイズ）は、シーン上の [cloud01] / [cloud02] の Transform スケールを反映する

### 描画順
- 雲は [Illust] の手前レイヤーに表示する
- Config / PlayManual / SoundTest の各 Canvas 前面表示を邪魔しない

## SoundTestButton 仕様（実装済み）

### 目的
- 開発用に `Assets/BGM` と `Assets/SE` 配下の音を再生し、音量バランス確認を行う

### 表示・閉じる
- [SoundTestButton] 押下で [SoundTestCanvas] を `active`
- [SoundTestCanvas] は最前面で表示
  - `Screen Space Overlay`
  - `overrideSorting = true`
  - `sortingOrder = 1100`
- [SoundTestExitButton] 押下で [SoundTestCanvas] を `inactive`
- [SoundTestCanvas] 表示中は通常メニューUI（Start/Config/PlayManual/SoundTest）を操作不可

### 背景表示
- [CanvasBG] は黒単色 + 半透明
- アルファは `<SoundTestCanvasBgAlpha>` で調整

### 動的ボタン生成（Editor 実行）
- `AssetDatabase` で以下を検索してボタンを動的生成する
  - BGM: `Assets/BGM`
  - SE: `Assets/SE`（サブフォルダ含む）
- Build（非Editor）での同等機能は対象外

### 再生仕様
- BGM:
  - ボタン押下で再生
  - 同じBGMボタン再押下で停止
  - 別BGM押下で切替（同時再生しない）
- SE:
  - 押下時に1回だけ `PlayOneShot`

### メニューBGMとの関係
- SoundTest開始時にメニューBGMを一時停止
- SoundTest終了時にメニューBGMを復帰

## Audio 実装ルール（基本）
- menu_scene の BGM と SE は必ず別 `AudioSource` を使用する
- `AudioSource.volume` はコード側で `1f` を基本値とする
- 最終音量は `SoundSettingsManager` のゲインで反映する
  - BGM: `GetBgmGain01()`
  - SE: `GetSeGain01()`
- 全体基礎倍率は `SoundSettingsManager` の `bgmBaseMultiplier` / `seBaseMultiplier` を使用する

## Inspector変数定義 [menu_scene_controller]
- Header：ステージ設定
* <ステージ名>の詳細
 * char デフォルト：stage_01
 * Tooltip：遷移先ステージ名

- Header：Config
* <ConfigCanvas>
 * ConfigButton 押下時に active にする対象
* <ConfigCanvasBgAlpha>
 * Config 背景暗転のアルファ（0-1）
* <MasterValueText>
* <BgmValueText>
* <SeValueText>
* <MasterMinusButton> / <MasterPlusButton>
* <BgmMinusButton> / <BgmPlusButton>
* <SeMinusButton> / <SePlusButton>
* <ConfigExitButton>
* <ConfigToggleSeClip>
 * Config開閉時SE（例: 魔王魂 効果音 システム13）
* <ConfigVolumeUpSeClip>
 * 音量アップ押下時SE（例: 魔王魂 効果音 システム37）
* <ConfigVolumeDownSeClip>
 * 音量ダウン押下時SE（例: 魔王魂 効果音 システム28）
* <PlayManualCanvas>
 * PlayManualButton 押下時に active にする対象
* <PlayManualCanvasBgAlpha>
 * PlayManual 背景暗転のアルファ（0-1）
* <PlayManualExitButton>
 * PlayManualCanvas を閉じるボタン
* <PlayManualImageRoot>
 * 遊び方画像を貼る固定領域
* <SoundTestCanvas>
 * SoundTestButton 押下時に active にする対象
* <SoundTestCanvasBgAlpha>
 * SoundTest 背景暗転のアルファ（0-1）
* <SoundTestExitButton>
 * SoundTestCanvas を閉じるボタン
* <SoundTestBgmSectionRoot>
 * BGM テストボタンを動的配置する親
* <SoundTestSeSectionRoot>
 * SE テストボタンを動的配置する親

## Inspector変数定義 [MenuIllustSpeedLineEffect]
* <PatternLoopSeconds>
 * 発生パターン1周秒
* <PatternEventCount>
 * 1周あたりの発生イベント数
* <PoolSize>
 * 再利用する線オブジェクト数
* <StartYOffset> / <SpawnXRange>
 * 挿絵基準の発生開始位置
* <MinSpeed> / <MaxSpeed>
 * 速度線の移動速度レンジ
* <StartWidth> / <EndWidth>
 * 線の太さ（白背景向けに太め推奨）
* <LineColorStart> / <LineColorEnd>
 * 線色グラデーション
* <UseContrastOutline> / <OutlineColor> / <OutlineDistance>
 * 視認性補強（背景明度対策）

## Inspector変数定義 [MenuIllustForegroundCloudEffect]
* <EnableEffect>
 * 雲の前景通過演出 ON/OFF
* <Alpha>
 * 前景雲の透過率（既定 0.7）
* <Cloud01.IntervalSeconds>
 * `cloud01` 通過周期（既定 8 秒）
* <Cloud02.IntervalSeconds>
 * `cloud02` 通過周期（既定 13 秒）
* <Cloud01.MoveDurationSeconds> / <Cloud02.MoveDurationSeconds>
 * 下→上移動にかける時間（短いほど高速）

