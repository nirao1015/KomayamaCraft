# KomayamaCraft 生産レシピ・施設データ 詳細仕様

## 0. 文書の位置づけ

| 項目 | 内容 |
| --- | --- |
| 原本（人間用・**改変禁止**） | `spec/宇宙船は現地改修_星間跳躍機関_生産設計.xlsx` |
| 取り込み正本（ゲーム用コピー） | 「新レシピ」「選別設定」「名称対応」を CSV／JSON 化した資産（§4） |
| ゲーム側定義 | `ItemDefinition` / `FacilityDefinition` / `RecipeDefinition`（ScriptableObject） |
| 方針 | 既存の施設・レシピ・アイテム（加工系）を**この表で一新**。チュートリアルの鱗圧延も**基礎加工台**へ変更 |
| 非投入 | 「原素材総量」「中間品総量」「展開明細」「概要」「対応表」は集計・説明用。ゲームデータに入れない |
| 値の書き方 | **確定**／**仮値**／**提案** |

---

## 1. 確定した設計方針

| # | 項目 | 内容 | 区分 |
| --- | --- | --- | --- |
| 1 | 置き換え | 既存カタログを全面置換（近い旧名の流用・並存はしない） | 確定 |
| 2 | チュートリアル | 鱗圧延作業台 → **基礎加工台**。関連会話・クエスト文言も追従 | 確定 |
| 3 | 複数入力行 | 同一「施設＋完成品」の連続行は **1 レシピ・複数入力** にまとめる | 確定 |
| 4 | 「方式」列 | データ種別としては**一旦無視**（将来用にメモ可） | 確定 |
| 5 | 熱嚢選別 | `WeightedSingle`。既定出現率 **嚢皮 95 / 芯種 5 / 熱糸 0**。レア率上昇はスキル等で後続 | 確定 |
| 6 | 加工時間・燃料 | **レシピごと**に持つ。未記載は仮値で埋める。燃料は全加工施設で **蓄電触腕**（`item.volt_jelly`） | 確定 |
| 7 | 施設メタ | 建設コスト・フットプリント・スロット容量をデータとして持てる | 確定 |
| 8 | 跳躍機関架台 | 新規施設として追加 | 確定 |
| 9 | 永続 ID | 英語ベースの安定 ID（例 `facility.prep_bench`）。**表示名変更で ID を変えない** | 確定 |
| 10 | 表示名 | 日本語（名称対応シート）を正 | 確定 |
| 11 | 同期構成 | 原本 xlsx 非改変 → ゲーム用 CSV/JSON → Editor 取り込みで SO 生成・更新 | 確定 |
| 12 | 未開放レシピ | `requiredUnlockId` 枠は空で用意。未開放は施設メニューで**パネル閉じたまま** | 確定 |

---

## 2. 二段データ構成

```
[原本 xlsx] ──触らない──► 人間が編集
                │
                ▼ 手作業または差分コピー（エージェント／ツール）
[GameData 用 CSV/JSON] ──取り込み正本──► Editor インポータ
                │
                ▼
[ScriptableObject]
  Items / Facilities / Recipes / Catalogs
```

### 2.1 取り込み正本ファイル（提案パス）

| ファイル | 元シート | 役割 |
| --- | --- | --- |
| `Assets/GameData/KomayamaCraft/Source/names.csv` | 名称対応 | 区分・english・japanese・（任意メモ） |
| `Assets/GameData/KomayamaCraft/Source/recipes.csv` | 新レシピ（行まとめ済み） | 施設・完成品・入出力・仮時間・燃料フラグ |
| `Assets/GameData/KomayamaCraft/Source/sorter.csv` | 選別設定 | 入力・重み付き出力 |

原本を更新したら **Source CSV を直し → インポータ再実行** でゲーム側 SO が追従する。

### 2.2 インポータ要件（確定）

- メニュー例：`KomayamaCraft/Game Data/Import Production Source`
- 動作：
  1. Source CSV を読む
  2. 永続 ID で既存 SO を検索し、無ければ生成、あればフィールド更新
  3. Catalog（Item / Facility / Recipe）を洗い替え登録
  4. 旧 ID で残った未使用加工系 SO は **削除または `_Obsolete` フォルダへ退避**（一新方針）
- 原本 xlsx はインポータが直接読まない（依存・差分事故防止）

---

## 3. 永続 ID と表示名

| 種別 | ID 接頭辞 | 例 | 表示名 |
| --- | --- | --- | --- |
| 施設 | `facility.` | `facility.prep_bench` | 基礎加工台 |
| アイテム | `item.` | `item.scale_plate` | 鱗鉄板 |
| レシピ | `recipe.` | `recipe.scale_plate` | 鱗鉄板（または「鱗鉄板の加工」等・表示は Inspector 調整可） |
| 解放 | `unlock.` | （今回空） | — |

- English 列は **初期の ID スラッグ生成用**。後から English 表示を変えても ID は維持。
- 日本語表示名は名称対応を正とする。

### 3.1 施設 ID（確定案）

| 日本語 | English | definitionId |
| --- | --- | --- |
| 基礎加工台 | Prep Bench | `facility.prep_bench` |
| 熱嚢選別機 | Sac Sorter | `facility.sac_sorter` |
| 共生組立槽 | Symbio Vat | `facility.symbio_vat` |
| 位相工作機 | Phase Mill | `facility.phase_mill` |
| 跳躍機関架台 | Jump Rig | `facility.jump_rig` |

### 3.2 主要アイテム ID（確定案）

名称対応に従う。例：

| 日本語 | definitionId |
| --- | --- |
| 鉄鱗 | `item.iron_scale` |
| 熱嚢 | `item.heat_sac` |
| 蓄電触腕 | `item.volt_jelly`（燃料。素材集計対象外。ワールド物体「蓄電クラゲ」から自動ドロップ） |
| 磁角 | `item.flux_horn` |
| 星紋晶 | `item.star_crystal` |
| 位相片 | `item.phase_shard` |
| 軌道絹 | `item.orbit_silk` |
| 嚢皮 / 芯種 / 熱糸 | `item.sac_skin` / `item.core_seed` / `item.heat_filament` |
| 鱗鉄板 | `item.scale_plate` |
| 磁束線 | `item.flux_wire` |
| 磁芯 | `item.flux_core` |
| 晶片 | `item.star_chip` |
| 位相粉 | `item.phase_dust` |
| 軌道布 | `item.orbit_cloth` |
| 生体配線 | `item.bio_wire` |
| 磁束コイル | `item.flux_coil` |
| 星紋回路 | `item.star_circuit` |
| 位相織膜 | `item.phase_weave` |
| 蓄電胞 | `item.charge_cell` |
| 骨格梁 | `item.frame_beam` |
| 航法核 | `item.nav_core` |
| 位相環 | `item.phase_ring` |
| 励起炉 | `item.exciter` |
| 界面制御器 | `item.boundary_unit` |
| 固定架 | `item.mount_frame` |
| 星間跳躍機関 | `item.starjump_drive` |

---

## 4. レシピ規則

### 4.1 行のまとめ（確定）

新レシピシートで、同一 Tier・施設・完成品が連続し、先頭行だけ「出力数」があり後続行は空の場合：

- **1 つの `RecipeDefinition`**
- `inputs` に全行の（入力素材, 入力数）を列挙
- `outputs` は先頭行の完成品 × 出力数（通常 1）

### 4.2 熱嚢選別（確定）

| 項目 | 値 |
| --- | --- |
| 施設 | `facility.sac_sorter` |
| 入力 | 熱嚢 × 1 |
| outputMode | `WeightedSingle` |
| 出力候補（既定重み） | 嚢皮 **95**、芯種 **5**、熱糸 **0** |
| 備考 | 重み合計 100。スキル等で希少側を上げるのは後続（データ駆動の余地を残す） |

### 4.3 新レシピ一覧（行まとめ後・確定）

#### Tier 1　基礎加工台（レシピ切替・複数レシピ）

| 完成品 | 入力 |
| --- | --- |
| 鱗鉄板 ×1 | 鉄鱗 ×3 |
| 磁束線 ×1 | 磁角 ×2、軌道絹 ×2 |
| 磁芯 ×1 | 磁角 ×2 |
| 晶片 ×1 | 星紋晶 ×2 |
| 位相粉 ×1 | 位相片 ×2 |
| 軌道布 ×1 | 軌道絹 ×3 |

#### Tier 1　熱嚢選別機

| 完成品 | 入力 |
| --- | --- |
| （確率1種） | 熱嚢 ×1 → §4.2 |

#### Tier 2　共生組立槽

| 完成品 | 入力 |
| --- | --- |
| 生体配線 ×1 | 磁束線 ×3、嚢皮 ×1 |
| 磁束コイル ×1 | 生体配線 ×2、磁芯 ×1 |
| 星紋回路 ×1 | 晶片 ×2、生体配線 ×2 |
| 位相織膜 ×1 | 位相粉 ×2、軌道布 ×2、熱糸 ×1 |
| 蓄電胞 ×1 | 磁束線 ×4、晶片 ×1、芯種 ×1 |
| 骨格梁 ×1 | 鱗鉄板 ×4、軌道布 ×1 |

#### Tier 3　位相工作機

| 完成品 | 入力 |
| --- | --- |
| 航法核 ×1 | 星紋回路 ×3、磁束コイル ×2、位相織膜 ×1 |
| 位相環 ×1 | 骨格梁 ×3、位相織膜 ×4、磁束コイル ×2 |
| 励起炉 ×1 | 蓄電胞 ×4、星紋回路 ×2、嚢皮 ×5 |
| 界面制御器 ×1 | 位相織膜 ×3、星紋回路 ×4、磁束コイル ×3 |
| 固定架 ×1 | 骨格梁 ×4、鱗鉄板 ×6 |

#### Tier 4　跳躍機関架台

| 完成品 | 入力 |
| --- | --- |
| 星間跳躍機関 ×1 | 航法核 ×1、位相環 ×2、励起炉 ×1、界面制御器 ×1、固定架 ×1 |

### 4.4 加工時間・燃料（仮値）

CSV に列を持つ。未調整時の仮値：

| 列 | 仮値 | 区分 |
| --- | --- | --- |
| `processingSeconds` | Tier1: 2 / Tier2: 4 / Tier3: 6 / Tier4: 10 / 選別: 1.5 | 仮値 |
| `usesFuel` | `true`（全加工レシピ） | 確定 |
| 施設 `acceptedFuelItems` | 蓄電触腕（`item.volt_jelly`）のみ | 確定 |

---

## 5. 施設データ項目

`FacilityDefinition` が持てること（既存フィールド＋不足は拡張）：

| フィールド | 内容 | 区分 |
| --- | --- | --- |
| definitionId / displayName | §3 | 確定 |
| supportedRecipes | 当該施設のレシピ参照 | 確定 |
| constructionCost | アイテム×個数（**仮値可・後で調整**） | 確定（枠） |
| footprintWidth/Height | ブロック数（仮値可） | 確定（枠） |
| input/output/fuel Capacity | スロット容量（仮値可） | 確定（枠） |
| usesFuel / acceptedFuelItems | 蓄電触腕（`item.volt_jelly`） | 確定 |
| requiredUnlockId | 施設解放（今回空でも可） | 確定（枠） |

建設コストの具体数は原本に無いため、インポート時は仮（例: Tier1 鉄鱗×5 等）を入れ、後で Source CSV で直す。

---

## 6. 未開放レシピ UI

| 項目 | 内容 | 区分 |
| --- | --- | --- |
| データ | `RecipeDefinition.requiredUnlockId`（空＝解放扱いでよいかは実装時に「空＝解放」と明記） | 確定 |
| 表示 | 未開放レシピの選択パネルは**閉じたまま**（中身を見せない） | 確定 |
| 今回の表 | 開放条件なし → 全レシピ `requiredUnlockId` 空＝初期開放で運用してよい | 確定 |

---

## 7. 既存資産の一新手順（実装時）

1. Source CSV を作成（名称対応・新レシピまとめ・選別 95/5/0）
2. インポータで Items / Facilities / Recipes を生成
3. 旧加工系 SO・旧 Catalog 参照を除去
4. シーンの建設メニュー・施設プレハブ参照を新 `facility.*` へ付け替え
5. チュートリアル会話・クエスト（鱗圧延／鉄鱗板等）を基礎加工台／鱗鉄板へ更新
6. プレイで Tier1 加工〜選別が通ることを確認

---

## 8. 受け入れ条件

- [x] 原本 xlsx がリポジトリ上で変更されていない
- [x] Source CSV から SO を再生成できる（`KomayamaCraft/Game Data/Import Production Source`）
- [x] 施設5種・§4.3 の全レシピ・選別重み 95/5/0 が入っている
- [x] 表示名が日本語、ID が安定している
- [x] 燃料は蓄電触腕（`item.volt_jelly`）、時間はレシピごと（仮値可）
- [x] 施設に建設コスト・サイズ・容量フィールドがある
- [x] 未開放枠（requiredUnlockId）があり、空は開放扱い
- [x] チュートリアルが基礎加工台表記に更新されている
- [x] 旧「鱗圧延作業台」等の加工カタログが残っていない（旧施設プレハブ／_Obsolete 施設 SO は削除済み）

---

## 9. 改訂履歴

| 日付 | 内容 |
| --- | --- |
| 2026-09-22 | 実装同期：Import 済み・基礎加工台チュートリアル・旧施設プレハブ削除・受け入れチェック反映 |
| 2026-09-22 | 初版。xlsx 非改変・CSV 二段・全面置換・選別 95/5/0・燃料・未開放枠を確定 |
