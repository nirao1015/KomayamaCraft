# KomayamaCraft 宇宙船修理演出（雨漏り納品達成）詳細仕様

> **状態**：確定（実装済み・調整継続）。  
> **対象シーン**：`komayama_craft_scene`  
> **発火**：雨漏りクエスト鱗鉄板 3／3 納品 → 完了会話終了の直後  
> **実装**：`KomayamaShipVisual` ＋ `KomayamaShipRepairCinematic`（宇宙船オブジェクト）

---

## 0. 文書の位置づけ

| 項目 | 内容 |
| --- | --- |
| 目的 | 初期納品達成後の宇宙船ビジュアル更新と駒山喜び演出を固定する |
| 非目的 | 他 Tier の船段階演出の汎用フレームワーク全体 |
| 関連 | NPC 初期納品、会話オーバーレイ、カメラ、クエストログ、SE／BGM、マウス狐 |

見た目オブジェクトは `World/WorldLayers/Layer_Objects/宇宙船`（子 `見た目`）。論理船 `World/CrashedShip`（`KomayamaShip`）とは別。

---

## 1. 発火タイミング（確定）

| 順 | 内容 |
| --- | --- |
| 1 | 鱗鉄板 3／3 納品 |
| 2 | 完了会話 `craft_quest_leak_deliver_done` |
| 3 | **本演出**（ズーム〜繭〜喜び〜ズーム戻し） |
| 4 | 雨漏りクエスト Completed、DeliveryNpc 復帰、次クエスト！ など既存後始末 |

セーブ上の船見た目段階は、演出が終わって `ship_2` になった状態を正とする（スキップ時も最終状態まで揃える）。

---

## 2. 演出シーケンス（確定）

開始時にカメラ位置・ortho をスナップショットする。終了時（スキップ含む）に必ず復元する。  
演出中は会話と同様、クエストパネル・メニュー非表示、フィールド操作ロック。

| 順 | 内容 |
| --- | --- |
| 1 | ズームイン（§2.1） |
| 2 | どくん ×3（§2.2） |
| 3 | 繭 CLOSE → `ship_2` → 発光イン 0.6s → 光った静止 0.4s → OPEN（光ったまま）→ 発光アウト → 解除後静止（§2.3） |
| 4 | 駒山喜びモーション（§2.4）※まだズーム中 |
| 5 | ズームアウト／カメラ復元 |

### 2.1 ズームイン

| 項目 | 内容 |
| --- | --- |
| 対象 | 宇宙船（見た目）のワールド中心 |
| ズーム | **`KCConfigValues` / `KCWorldSettings` のズーム下限**（最大ズーム）まで。通常上下限は超えない |
| 時間 | Inspector「ズームイン秒」 |
| パン | 船が画面中央付近に来る |

### 2.2 どくん ×3

| 項目 | 内容 |
| --- | --- |
| 回数 | **3** |
| 手段 | 宇宙船と同スプライトを 1 枚重ねる |
| 動き | **1 秒**かけて拡大しつつアルファ減衰 → **0.4 秒**停止、を繰り返す |
| パラメータ | Inspector「散らす秒」「散らしたあと停止秒」「ピーク拡大倍率」 |

### 2.3 繭（CLOSE / OPEN + 加算グロー）

| 項目 | 内容 |
| --- | --- |
| オーバーレイ | `ship_effect-1` → `2` → `3` → `4` |
| 1 コマ | **0.3 秒**（Inspector「1コマ秒」） |
| 大きさ | **1.4 倍**（Inspector「繭の拡大倍率」） |
| 材質（色付き繭） | **Unlit**（船のアウトライン材は使わない） |
| CLOSE | コマ 1→4 で閉じる |
| 差し替え | CLOSE 直後にベースを **`ship_2`** へ |
| 白発光 | 同スプライトをわずかに拡大した **加算（Additive）** レイヤ。URP Bloom・真っ白塗りは使わない |
| 発光マテリアル | `Assets/Materials/KomayamaCraft/SpriteAdditiveUnlit.mat`（`KomayamaCraft/Sprite-Unlit-Additive`）。**Outline 強制の対象外**（`KomayamaPreserveSpriteMaterial`） |
| DrawOrder | グロー／繭オーバーレイは `KomayamaWorldLayerDrawOrder` の材差し替え対象外。方針は `描画バンド整理` §2.4 |
| 発光イン | CLOSE／`ship_2` 直後に **0.6 秒**かけてフェードイン（Inspector「発光イン秒」） |
| 光ったあと静止 | 発光イン完了後 **0.4 秒**待ってから OPEN（Inspector「光ったあと静止秒」） |
| OPEN | **光ったまま**コマ 4→1 逆再生。グローは同コマに追従。色付き繭は α=0 |
| 発光アウト | OPEN **完了後**、**色付き繭を先に無効化**してからフェードアウト（Inspector「発光アウト秒」。既定 0.8）。アウト中に色付き繭の α を戻さない |
| グロー枚数 | 内側 1 枚＋任意の外側 1 枚（拡大・強度は Inspector） |
| 解除後静止 | **0.8 秒**（Inspector「解除後の静止秒」） |

### 2.4 駒山・喜びモーション（確定）

繭 OPEN・解除後静止のあと、**ズーム戻し前**に続ける。

| 項目 | 内容 |
| --- | --- |
| 素材置き場 | `Assets/Sprites/chara/` |
| 現行シート例 | `fox_repair_complete_special_spritesheet_6x4.png`（差し替え可） |
| Inspector | `Fox Joy Frames`（スプライト配列）／`Fox Joy Frame Seconds`（各コマ秒。空なら内蔵デフォルト） |
| 配置 | 宇宙船の**左横**ワールド座標（`船左オフセット`）。オブジェクト名 `FoxRepairJoyAccessory` |
| 描画帯 | **`Layer_Mouse` / `WorldMouse`**。マウス追従狐（`FoxCursorFollower`）と**同帯・別オブジェクト**。Unlit＋`KomayamaPreserveSpriteMaterial` |
| マウス狐 | 下表「一時非表示ルール」 |
| 尺 | 先頭静止 → 本編 1 回再生 → 最終静止（Inspector「開始静止秒」「終了静止秒」。既定 0.6／0.6） |
| 本編後／中断 | アクセサリを消し、マウス狐の一時非表示を必ず解除（`finally`／スキップ／演出終了） |
| BGM / SE | 仕組みあり。再生は `playCelebrationAudio`（既定 OFF）。SE cue=`ShipRepairCompleteJoy` |

#### マウス狐・一時非表示ルール（確定）

| 項目 | 内容 |
| --- | --- |
| API | `KCMouseFoxFollower.SetSuppressedForCinematic(true/false)` |
| やること | Sprite を隠すだけ。追従狐オブジェクトは消さない・親も変えない |
| やってはいけない | 演出のために `displayMode = Off` にすること（セーブに Off が残り、次回 Play で狐が消える） |
| セーブ | `displayMode` は変えない。Capture 中に演出が走っても Off は書かない |
| 誤セーブ復旧 | ロード時にセーブ上の Off は **FollowMouse へ戻す**（恒久 Off 用途は無し。詳細は指示差分 §7.2.2） |
| 帯の誤解 | 喜びアニメを `Layer_Mouse` に置いても追従狐は消えない。消える原因は上記の Off 誤セーブ |

#### ジャンプ位置上げ（確定）

ジャンプコマ表示時だけ、船左オフセットに **Y 上げ**を加算する。枚目は **1 始まり**。量は Inspector で調整する。

| Inspector | 既定 |
| --- | --- |
| ジャンプ枚目A | **12** |
| ジャンプ上げA（Y） | **0.6** |
| ジャンプ枚目B | **13** |
| ジャンプ上げB（Y） | **0.35** |

該当しない枚目の上げ量は 0。

---

## 3. 操作ロックとスキップ（確定）

| 項目 | 内容 |
| --- | --- |
| ロック | 演出中はフィールド操作・パン／ズーム入力を受けない（会話中と同様） |
| HUD | クエストパネル・建設／設定メニューは非表示。開いていた施設／建設メニューは閉じる |
| スキップ入力 | **左クリック長押しのみ** |
| 必要時間 | **1.2 秒**押し続け（Inspector「長押しスキップ秒」） |
| インジケーター | マウスカーソル周りに円形の進行表示。押しているあいだだけ増える。離すとリセット |
| スキップ時 | シーケンス打ち切り → **`ship_2`**、演出レイヤ非表示、**マウス狐の一時非表示解除**、カメラ復元、後始末へ |

右クリック・その他ボタンではスキップしない。

---

## 4. 見た目段階（確定・初期）

| 段階 | スプライト | いつ |
| --- | --- | --- |
| 0（初期） | `ship_1` | デフォルト／雨漏り未達成 |
| 1 | `ship_2` | 本演出完了後 |

ロード時：雨漏りクエストが Completed なら `ship_2`、否则 `ship_1`。  
将来の `ship_3` 以降は別演出で段階を増やす（本書の範囲外）。

---

## 5. Inspector 早見（`KomayamaShipRepairCinematic`）

| グループ | 主な項目 |
| --- | --- |
| ズーム | ズーム Ortho Size（0＝ワールド設定の下限）／ズームイン秒／ズームアウト秒 |
| どくん | 回数／散らす秒／散らしたあと停止秒／ピーク拡大倍率 |
| 繭 | コマ配列／1コマ秒／拡大倍率／**光ったあと静止秒**／解除後の静止秒 |
| 繭・白発光 | 加算マテリアル／発光イン秒（0.6）／発光アウト秒／グロー拡大・強度（内側／外側）／外側グローを使う |
| スキップ | 長押しスキップ秒 |
| 喜びモーション | Frames／Frame Seconds／開始・終了静止秒／船左オフセット／表示高さ／`playCelebrationAudio` |
| ジャンプ位置 | ジャンプ枚目A・B／ジャンプ上げA・B（Y） |

---

## 6. 実装メモ

| 項目 | 内容 |
| --- | --- |
| スクリプト | `Assets/Scripts/komayama_craft_scene/KomayamaShipRepairCinematic.cs` |
| 発光シェーダ | `Assets/Shaders/KomayamaCraft/SpriteAdditiveUnlit.shader` ／ mat `Assets/Materials/KomayamaCraft/SpriteAdditiveUnlit.mat` |
| マウス狐 | `KCMouseFoxFollower.SetSuppressedForCinematic`（`displayMode` は触らない） |
| 呼び出し | `KomayamaQuestController`（納品完了会話の直後） |
| ズーム正本 | `KCConfigValues` の `KCWorldSettings`（ズーム下限／上限） |
| デバッグ開始 | `KomayamaQuestDebugController` の `RainLeakPreDelivery` などで納品直前から検証可 |
| 自動化プレイ | 演出中はスキップ可能。自動プレイは長押し相当でスキップ、または完了待ち |
| 関連仕様 | 描画材の強制方針 → `KomayamaCraft_描画バンド整理_詳細仕様.md` §2.4／狐セーブ → 指示差分 §7.2 |

---

## 7. 改訂履歴

| 日付 | 内容 |
| --- | --- |
| 2026-09-20 | 発光アウト前に色付き繭を無効化。アウト中の α 戻し禁止（端残り対策） |
| 2026-09-20 | マウス狐は `SetSuppressedForCinematic`（displayMode=Off 禁止・誤セーブ復旧を明記） |
| 2026-09-20 | タイミング：CLOSE→ship_2→発光イン0.6s→光った静止0.4s→OPEN |
| 2026-09-20 | 繭に加算グロー。DrawOrder の Outline 誤適用対策（Preserve）を追記 |
| 2026-09-20 | 白飛び撤去・喜びは WorldMouse・ジャンプ枚目12/13のY上げを Inspector 化 |
| 2026-09-19 | ズームを KCWorldSettings 正本に。会話中 HUD 非表示と連携 |
| 2026-09-19 | 初版確定。どくん3・effect 順送り・スキップ・カメラ復帰 |
