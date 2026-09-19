# KomayamaCraft 描画バンド（Canvas / Sorting）整理 設計草案

> **状態**：描画帯の分離は実装済み。`Layer_Objects` 既定アウトライン（§2.3）も実装済み。  
> **動機**：HUD（`HudCanvas`）がワールドに埋もれる問題が再発している。game02 のように「帯同士が絶対に被らない」構造へ寄せる。

---

## 0. いま起きていること（原因）

`Screen Space - Camera` の Canvas は、**Camera に対して SpriteRenderer と同じ Sorting Layer / Order で競合**する。

| 対象 | sortingLayer | order | 結果 |
| --- | --- | --- | --- |
| `HudCanvas`（ガイド・メッセージ・**DebugOverlay/ButtonSpeed**） | **Default（最下位）** | 200 | 海・地形・オブジェクトより**後ろ**に描画される |
| ワールド各 Sprite | `WorldSea` … `WorldMouse` | 各層 | Hud より前 |
| `SystemCanvas`（メニュー等） | `WorldSystem`（最上位付近） | 300 | 見える |
| 施設ワールド HUD（World Space Canvas） | `WorldOverlay` | 40 | ワールド内オーバーレイとしては妥当 |

つまり **Hud だけ Default に落ちている**のが「埋もれ」の直接原因。  
`FixedAspectCanvasFitter` は `sortingOrder` 下限を触るが、**Sorting Layer は直さない**ため再発する。

---

## 1. game02 との対応関係（方針）

game02 は `BgCanvas` / `FieldCanvas` / `ItemCanvas` / `UnitCanvas` / `PanelCanvas` など **Canvas 単位で帯を分け、帯同士を被らせない**。

KomayamaCraft はフィールド本体が **Tilemap / SpriteRenderer** 中心なので、海・地形・採集物を全部 UI Canvas に載せ替えるのは不適切（実装コスト大・2D ワールド向きでない）。

**取る方針（提案・確定候補）**

| game02 の考え方 | KomayamaCraft での実現 |
| --- | --- |
| 帯ごとにオブジェクトを分ける | **ワールド帯＝Sorting Layer ＋ ヒエラルキー親**／**画面 UI 帯＝シーン直下の専用 Canvas** |
| 帯同士は絶対に被らない | **1 帯＝1 Sorting Layer（または完全に上の UI 専用 Layer）**。共有禁止 |
| Canvas 間の order 争いはしない | 画面 UI Canvas は **互いに別 Sorting Layer**。同一 Layer で order だけ並べない |

→ 提案の「全部 Canvas」ではなく、**「全部が帯を持つ」＋「画面 UI は Canvas を帯ごとに分ける」**。

---

## 2. 描画バンド一覧（下 → 上）

### 2.1 ワールド帯（Sprite / Tilemap / World Space UI）

親の正本は引き続き `World`。中身は Sprite 中心。

| 順 | Sorting Layer（案） | ヒエラルキー親（案） | 載せるもの | 備考 |
| --- | --- | --- | --- | --- |
| W0 | `WorldBackground`（新規・任意） | `World/FieldBackground` | 遠い背景 | 未使用なら省略可 |
| W1 | `WorldSea`（既存） | `World/WorldLayers/Layer_Sea` | 海 | 現状維持 |
| W2 | `WorldContinent`（既存） | `…/Layer_Continent` | 地形 | 現状維持 |
| W3 | `WorldObject`（既存） | `…/Layer_Objects` | 採集発生点・原生・納入ゴミ箱・宇宙船など（非施設） | **施設は載せない**。配下 Sprite の既定材は `SpriteOutline`（§2.3） |
| W4 | `WorldFacility`（**新規**） | `World/Layer_Facilities` | 仮組・完成施設・保管 | 現状 `WorldObject` 共有をやめる |
| W5 | `WorldEffect`（既存） | `…/Layer_Effects` | エフェクト | 現状維持 |
| W6 | `WorldDrop`（**新規**） | `World/Layer_Drops`（新設） | 地面ドロップ | 現状 `WorldOverlay` 共有をやめる |
| W7 | `WorldOverlay`（既存） | `World/Layer_WorldOverlay`（整理） | 施設ワールド HUD、建設プレビュー、禁止塗りデバッグ | 「ワールドに張り付く補助表示」専用 |
| W8 | `WorldMouse`（既存） | `World/Layer_Mouse` | 手持ちアイコン・狐 | 現状維持 |

**エリア制御（NoDrop / NoBuild）**は画面固定 UI ではなくワールド整列の Tilemap なので、**Screen Canvas にはしない**。  
表示するときは W7 `WorldOverlay`（デバッグ表示時のみ）。通常プレイ非表示は現状どおり。

### 2.3 `Layer_Objects` の既定アウトライン（確定）

絵ファイルを焼き直さず、**マテリアル／シェーダー**で外周アウトラインを付ける。

| 項目 | 内容 |
| --- | --- |
| シェーダー | `KomayamaCraft/Sprite-Unlit-Outline` |
| マテリアル | `Assets/Materials/KomayamaCraft/SpriteOutline.mat` |
| 適用先 | `World/WorldLayers/Layer_Objects` 配下の全 `SpriteRenderer` |
| 適用タイミング | `KomayamaWorldLayerDrawOrder` が編集中（Hierarchy 変更）・Play 中に強制。Inspector の `Layer Objects Outline Material` を参照 |
| 他帯 | `Layer_Facilities` など Objects 以外は従来どおり URP `Sprite-Unlit-Default`（ランタイム `KC_WorldSpriteUnlit`） |
| メッシュ | アウトラインが切れないよう、対象スプライトは **Full Rect** を推奨 |
| 調整 | 厚み・色・Body Alpha は共通マテリアルの Inspector（Objects 全体で共有） |

### 2.2 画面 UI 帯（Screen Space - Camera・シーン直下 Canvas）

16:9 追従のため Overlay ではなく **Screen Space - Camera** を維持。  
**すべて World\* より上の専用 Sorting Layer** に置く（Default 禁止）。

| 順 | Sorting Layer（新規） | シーン直下 Canvas | 載せるもの |
| --- | --- | --- | --- |
| U1 | `UiHud` | `HudCanvas` | **`MessageText`（トースト）のみ** |
| U2 | `UiSystem` | `SystemCanvas` | 建設メニュー・設定・スキル・**クエストログ**・施設メニュー |
| U3 | `UiDebug` | `DebugCanvas` | Guide/State/Hand（開発表示）・DebugOverlay・倍速。本番／デバッグ表示 OFF で Canvas ごと非表示 |

`EndingOverlay` は `Screen Space Overlay` のまま最前面扱い（帯表の外。エンディング専用）。

#### Hud の扱い（確定）

| 項目 | 内容 |
| --- | --- |
| `HudCanvas` | プレイヤー向けトースト専用 |
| 旧 Guide/State/Hand | **DebugCanvas** へ移す（本番 UI が整うまでの開発表示） |
| Debug | Hud と統合しない。`DebugCanvas` に分離 |

---

## 3. ヒエラルキー案（シーン直下）

```text
World                          … ワールド帯のみ（Screen Canvas を置かない）
  WorldLayers/Layer_Sea|Continent|Objects|Effects
  Layer_Facilities
  Layer_Drops                  … 新設（FieldDropArea のドロップをここへ）
  Layer_WorldOverlay           … 施設ワールドHUD・プレビュー・制限グリッド
  Layer_Mouse
  （その他ロジック用オブジェクト：船・地域トリガ等。描画物は必ず上記親のどれかへ）

HudCanvas                      … U1 UiHud
SystemCanvas                   … U2 UiSystem（World/Layer_System から引き上げ）
DebugCanvas                    … U3 UiDebug（DebugOverlay をここへ）
Main Camera / EventSystem / Managers / KCConfigValues / EndingOverlay …
```

**`World/Layer_System` は廃止または空の互換シェル**。Screen UI を World の子に置かない（カメラ追従・ソート規則が混ざるため）。

---

## 4. 絶対ルール（再発防止）

1. **画面 UI Canvas の sortingLayer に `Default` を使わない。**
2. **帯をまたいで同じ Sorting Layer を共有しない。**  
   （例：施設と採集物で `WorldObject` 共有しない／ドロップと施設 HUD で `WorldOverlay` 共有しない）
3. **同一帯の中だけ** `sortingOrder` で前後を決める（帯をまたぐ order 勝負を禁止）。
4. **World Space Canvas**（施設上 HUD）は必ず W7 `WorldOverlay`。生成コードの定数を正本にし、Default に落ちない。
5. **`KomayamaWorldLayerDrawOrder`** が全帯を強制適用。`HudCanvas` / `SystemCanvas` / `DebugCanvas` もルールに入れる。
6. **`FixedAspectCanvasFitter`** は order だけでなく、**担当 Sorting Layer を設定／監視**する（または DrawOrder 側に一本化して Fitter からソート責任を外す）。
7. 新規 UI を足すときは「どの帯か」を先に決め、帯なしのシーン直下バラ置きを禁止。

---

## 5. 提案いただいた Canvas 分割との対応

| 提案 | 本設計での扱い |
| --- | --- |
| 背景キャンバス | W0 Sprite 帯（Canvas にしない） |
| 海キャンバス | W1 |
| 地形キャンバス | W2 |
| アイテムキャンバス | W3（発生点）＋ W6（ドロップ） |
| 施設キャンバス | W4（**Layer 分離**） |
| マウスキャンバス | W8 |
| システムキャンバス | U2 `SystemCanvas` |
| デバッグキャンバス | U3 `DebugCanvas`（Hud から分離） |
| エリア制御キャンバス | W7 のデバッグ表示（Screen Canvas にはしない） |

問題があればこの対応表を優先して修正する。

---

## 6. 実装フェーズ（承認後）

| 段階 | 内容 |
| --- | --- |
| A | Sorting Layer 追加（`WorldFacility` / `WorldDrop` / `UiHud` / `UiSystem` / `UiDebug`） |
| B | `HudCanvas`→`UiHud`、`SystemCanvas` をシーン直下＋`UiSystem`、`DebugCanvas` 新設＋倍速ボタン移設 |
| C | ドロップ親・施設 Sorting・WorldOverlay 用途をコード／仕様どおりに固定 |
| D | `KomayamaWorldLayerDrawOrder` と仕様 §5.2 を更新。`FixedAspectCanvasFitter` のソート責任を整理 |
| E | Play で「地形 ＜ ドロップ ＜ 施設ワールドHUD ＜ マウス ＜ HUD ＜ メニュー ＜ デバッグ」を目視確認 |

---

## 7. レビュー回答（確定）

| 質問 | 回答 |
| --- | --- |
| ワールド Sorting／UI 複数 Canvas | 可 |
| 施設・ドロップ Layer 新設 | 可 |
| DebugCanvas 分離 | 可 |
| クエストログ | **System** |
| エリア制御 | ワールド Overlay のまま |
| Hud の扱い | MessageText のみ残す。Guide/State/Hand は Debug へ |

### 後続で残すもの

- 本番向け常時 HUD（トースト以外）の本デザイン
- `WorldSystem` レイヤーの完全廃止（互換で残置）

---

## 8. 改訂履歴

| 日付 | 内容 |
| --- | --- |
| 2026-09-19 | `Layer_Objects` 既定アウトライン（`SpriteOutline`）を §2.3 として追記。W3 備考を更新 |
| 2026-09-16 | レビュー確定を反映。Hud=トーストのみ、開発テキストは Debug。実装着手 |
| 2026-09-16 | 草案。Hud が Default で埋もれる原因、game02 相当の帯分離、ハイブリッド案を提示 |
