# KomayamaCraft クエスト／会話の書き方 詳細仕様（素案）

> **状態**：フェーズ2 素案。実装は別指示まで。雨漏り1本をテンプレとして、**データ形とフック**だけ先に正本化する。
> **非目的**：シナリオ文面の量産、汎用クエストエンジンの一括実装、MainOnly／All 自動プレイの拡張。

---

## 0. 文書の位置づけ

| 項目 | 内容 |
| --- | --- |
| 目的 | クエスト追加時に迷わない **ID・CSV・完了条件・デバッグ開始** の型を決める |
| テンプレ正本 | 現行 `KomayamaQuestController` の雨漏り（`main_rain_leak`）＋現状把握（`main_situation_survey`） |
| 会話 CSV 列 | 既存 `DialogueCsvParser`／`spec/作成前資料/dialogue_scene.md` §7 を流用（列は増やさない） |
| セーブ形 | `QuestProgressSaveDto`（次フェーズ「セーブ契約」で凍結）。本書は**何を ID として載せるか**まで |
| 値の書き方 | **確定案**／**提案**／**要確認** |

関連：自動化プレイ P0、会話オーバーレイ、クエストログ表示、永続ID規約、保存データ契約。

---

## 1. 方針（確定）

| # | 方針 |
| --- | --- |
| A | **会話本文は CSV**。進行ロジックはクエスト種別に応じて C#（専用 or 汎用） |
| B | **専用コントローラ**：雨漏り／燃料補給チュートリアル／スキル開放／レベルアップ開放 |
| C | **汎用化**：それ以降の「作って納品」系（共通フック＋データ） |
| D | セーブに載せるのは **questId／status／completedObjectiveIds／counters** のみ（現行 DTO）。表示名は載せない |
| E | ログ文言は **当面ベタ書き**。汎用クエスト台帳では **文言 ID 指定**（§1.1） |
| F | 新規クエストを足す前に、本書のチェックリストを満たす |

### 1.1 ログ文言（確定）

| 段階 | 方針 |
| --- | --- |
| 専用（現状把握〜レベルアップ開放） | コード内日本語ベタ書きでよい |
| 汎用（作って納品以降） | 台帳に **文言 ID** を持ち、表示時に解決（多言語仕様の `quest.` 系） |

セーブには文言を載せない。自動プレイは文言を見ない。

---

## 2. ID 規約

### 2.1 クエスト ID（確定）

最終形（永続ID規約に合わせる）：

```text
quest.<main|sub>_<snake_name>
```

| 最終 ID | 意味 | 現行（移行前） |
| --- | --- | --- |
| `quest.main_situation_survey` | 現状把握 | `main_situation_survey` |
| `quest.main_rain_leak` | 雨漏りを直そう | `main_rain_leak` |
| `quest.main_refuel_bench` | 燃料補給チュートリアル | `main_refuel_bench`（定数・枠のみ） |

| 規則 | 内容 | 区分 |
| --- | --- | --- |
| 文字 | ASCII 小文字・数字・`_`・接頭辞直後の `.` のみ。Ordinal 比較 | 確定 |
| 接頭 | `quest.` ＋ `main_`／`sub_` | 確定 |
| 移行時期 | **燃料クエスト実装直前**に version 7 で一括リネーム＋セーブマイグレーション。それまでは現行 `main_*` のまま | 確定 |
| 永続 | 移行後の ID はリネームしない（表示名だけ変える） | 確定 |

### 2.2 オブジェクティブ ID（確定案）

クエスト内の「チェック1個」に対応する。セーブの `completedObjectiveIds` に入る。

```text
<quest_short>_<snake_action>
```

雨漏り例：

| ID | 意味 |
| --- | --- |
| `leak_iron_scale_drops` | 鉄鱗ドロップ集め |
| `leak_place_rolling_bench` | 圧延台を配置 |
| `leak_pickup_iron_scale` | 鉄鱗を拾う |
| `leak_deposit_iron_scale` | 作業台へ投入 |
| `leak_select_plate_recipe` | レシピ選択 |
| `leak_produce_plate` | 鱗鉄板を生産 |
| `leak_deliver_plate` | NPC へ納品 |

現状把握は短いキー（`wasd_w` 等）をそのまま使う。

| 規則 | 内容 | 区分 |
| --- | --- | --- |
| スコープ | クエスト横断で一意（セーブ配列がフラット） | 確定案 |
| 接頭 | 同一クエスト内は共通短接頭（`leak_`） | 提案 |
| ドット区切り | 使わない（`main_rain_leak.xxx` は採用しない） | 確定案 |

### 2.3 カウンタ ID（確定案）

進捗数値。セーブの `counters[].counterId`。

| 規則 | 内容 | 区分 |
| --- | --- | --- |
| 既定 | **オブジェクティブ ID と同じ文字列**をカウンタ ID にする（雨漏り現行） | 確定案 |
| 例外 | 1 オブジェクティブに複数カウンタが必要なときだけ別 ID（要文書化） | 提案 |

### 2.4 会話ステージキー（確定案）

オーバーレイ／ランナーに渡す `stageKey`。ファイル名は次。

```text
Assets/GameData/Dialogue/{sceneName}-{stageKey}.csv
```

KomayamaCraft では `sceneName = komayama_craft_scene`。

| 種別 | 命名 | 例 |
| --- | --- | --- |
| クエスト進行 | `craft_quest_<short>_<beat>` | `craft_quest_leak_intro_a` |
| NPC 常時 | `craft_npc_<npcId>_default` | `craft_npc_unit05_default` |
| OP／導入 | `craft_op_*` / `craft_intro_*` | `craft_op_01` |

| 規則 | 内容 | 区分 |
| --- | --- | --- |
| 文字 | 小文字・数字・`_`。ハイフンはファイル結合用のみ（キー本体に使わない） | 確定案 |
| 1 CSV = 1 再生単位 | 分岐は CSV 内に持たず、コントローラが次キーを選ぶ | 確定案 |
| カタログ | 追加後 `Tools/GameData/Sync All Catalogs` | 確定案 |

### 2.5 参照する定義 ID（確定案）

クエスト条件が指すアイテム／施設／レシピは **永続ID規約の定義 ID** を使う。

| 例 | ID |
| --- | --- |
| 鉄鱗 | `item.iron_scale` |
| 鱗鉄板 | `item.scale_plate` |
| 基礎加工台 | `facility.prep_bench` |
| 鱗鉄板レシピ | `recipe.scale_plate` |

---

## 3. 会話 CSV（確定案）

列は既存のまま。増やさない。

```text
type,speaker,text,slot,sprite_key,mode,audio_key
```

| type | 用途（ゲーム内オーバーレイでも可） |
| --- | --- |
| `line` | 本文 |
| `stand` / `dim` / `bg` / `se` / `bgm` / `bgm_stop` / `fx_*` | 演出 |
| `end` | 終端 |

| 規則 | 内容 | 区分 |
| --- | --- | --- |
| 分岐・条件 | CSV に書かない。再生前にコントローラがキーを選ぶ | 確定案 |
| プレースホルダ | `{count}` 等の実行時置換は **当面なし**（必要なら別フェーズ） | 提案 |
| スキップ | オートプレイでは会話スキップ API を使わない（自動化仕様 §2.1.1）。人手の UI スキップは別 | 確定案 |

---

## 4. 完了条件の型（確定案）

雨漏りから抽出した **フック種別**。新規クエストはこの集合から選ぶ。

| 種別 | 説明 | 雨漏りでの例 | セーブへの載せ方 |
| --- | --- | --- | --- |
| `counter_reach` | カウンタが必要数以上 | 採集 3／拾う 3／投入 3／生産 3／納品 3 | `counters`＋到達で `completedObjectiveIds` |
| `objective_flag` | 回数なしの達成 | 作業台配置、レシピ選択 | `completedObjectiveIds` のみ |
| `input_hold` | キー長押し等のチュートリアル | WASD 各キー | オブジェクティブ完了 |
| `dialogue_gate` | 指定 stage 再生後に次監視へ | intro／ask_deliver 等 | セーブしない（再開はオブジェクティブ進捗から復元） |
| `cinematic_on_complete` | クエスト完了時に演出 | 宇宙船修理 | 完了 status。演出の二重再生はコントローラ側で抑止 |
| `unlock_side_effect` | 完了／到達でメニュー解禁等 | 建設・設定解禁 | フラグはクエスト進捗から導出可ならセーブしない |

**提案**：上記以外（例：地域解放フラグ専用）が必要になったら、フェーズ1で `worldFlags` を増やすか、オブジェクティブに吸収するかを決める。素案では **クエスト進捗から導出できるものは別フラグを増やさない**。

---

## 5. クエスト1本の書き方テンプレ（雨漏り）

新規メインクエストを足すときの型。

### 5.1 定義チェックリスト

1. `questId` を決める（§2.1）
2. オブジェクティブ列を順に並べ、各 ID を決める（§2.2）
3. 必要数がカウンタなら必要数を定数／Inspector に（セーブは現在値のみ）
4. 各ビートの `stageKey` と CSV を用意（§2.4／§3）
5. 監視開始・完了時の副作用（カメラ／解禁／演出）を列挙
6. ログタイトル／本文の組み立て箇所を決める（表示専用）
7. デバッグ開始シナリオを最低1つ（§6）
8. P0 常緑に影響するなら自動化仕様 §11 を通す

### 5.2 進行ビート表（雨漏り写経）

| # | トリガ | 会話 | 監視／完了 |
| --- | --- | --- | --- |
| 1 | OP 終了後、現状把握完了 | `leak_intro_a` → `leak_intro_b` | Active 化 |
| 2 | 鉄鱗集め | （なし） | `counter_reach` `leak_iron_scale_drops` |
| 3 | 集め完了 | `leak_make_bench` | 配置監視 |
| 4 | 配置完了 | `leak_after_place` → `leak_feed` | 拾う＋投入 |
| 5 | 投入完了 | `leak_pick_recipe` | レシピ選択 |
| 6 | レシピ選択 | `leak_after_recipe` | 生産 |
| 7 | 生産完了 | `leak_ask_deliver`（＋ヒント可） | 納品 |
| 8 | 納品完了 | `leak_deliver_done` | 修理演出 → Completed |

### 5.3 再開ルール（確定案）

ロード時は **status＋completedObjectiveIds＋counters** から、次に監視すべきフェーズを復元する（雨漏り `HandleOpeningPhaseEndedInternal` と同型）。

- `dialogue_gate` の「会話中だった」はセーブしない。ロード後は **次の監視**から再開してよい。
- 完了済みクエストの演出は再実行しない。

---

## 6. デバッグ開始シナリオの型（確定案）

`KomayamaQuestDebugController` の Scenario をテンプレ化する。

| フィールド | 内容 |
| --- | --- |
| 名前 | `RainLeakIntro` / `RainLeakPreDelivery` のように **QuestShort＋地点** |
| 効果 | セーブ相当の進捗をセットし、その地点の監視を開始する |
| 禁止 | 本番パス以外の「完了フラグだけ立てて演出スキップ」を常態化しない（診断用は可） |

最低セット（提案）：

| シナリオ | 目的 |
| --- | --- |
| `NewGame` | OP から（既存ブート） |
| `<Quest>Intro` | そのクエスト導入直後 |
| `<Quest>PreFinish` | 最終条件直前（P0／手通し用） |

---

## 7. 実装境界（確定）

| 段階 | やり方 |
| --- | --- |
| 現状把握・雨漏り | 専用（現行 `KomayamaQuestController`） |
| 燃料補給チュートリアル | **専用**（次に実装） |
| スキル開放・レベルアップ開放 | **専用** |
| それ以降（作って納品など） | **汎用**（共通フック＋データ台帳。SO 等はここから） |

| やる（本書の範囲） | やらない（今） |
| --- | --- |
| ID／CSV／完了条件型／再開／デバッグ型の文書化 | 汎用エンジンの先行実装 |
| 専用→汎用の切り替え地点の明記 | シナリオ文面上書き |
| `quest.` 移行はフェーズ1で実施 | 多言語本実装 |

---

## 8. `quest.main_refuel_bench`（旧 `main_refuel_bench`）について

コードに既にある定数 `FuelRefillQuestId = "main_refuel_bench"` の意味：

- 雨漏り完了後、NPC にメインクエストマーカーを出す処理（`ApplyFuelQuestAcceptIcon`）用に **スロットだけ確保**している
- **進行ロジック・ログ文言・完了条件は未実装**（Status は Inactive のまま）
- 「ID 予約」＝この文字列を捨てて別名を付け直すのではなく、**燃料チュートリアル実装時に同じ ID を正式採用する**、という意味だった

| 判断 | 内容 | 区分 |
| --- | --- | --- |
| 残す | 燃料チュートリアルの正式 questId。移行前 `main_refuel_bench`／移行後 `quest.main_refuel_bench` | 確定 |
| 中身 | 専用コントローラで後から実装 | 確定 |

別名にしたい場合だけ、実装前に言い切ること（セーブに一度でも書くと変更コストが上がる）。

---

## 9. 改訂履歴

| 日付 | 内容 |
| --- | --- |
| 2026-09-20 | ログ：専用ベタ／汎用は文言ID。refuel ID は `main_refuel_bench` 確定 |
| 2026-09-20 | 判断反映：`quest.` 確定／ログベタ書きの影響／専用→汎用の境界／refuel ID 説明 |
| 2026-09-20 | フェーズ2 素案。雨漏りをテンプレに ID／CSV／完了条件／デバッグ型を整理 |
