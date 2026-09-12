# TASK-M01-01 新作フィールドシーン

## 目的

旧作固有処理を持ち込まず、M1を操作確認できる新作専用シーンを作る。

## 参照仕様

- `spec/KomayamaCraft_ゲーム基本仕様.md`
- `spec/Unity_初期プロジェクト設定.md`

## 依存タスク

- M0完了

## 作業範囲

- `komayama_craft_scene`、16:9固定描画、WASDカメラ、Input Systemを設定する。
- BGM、SE、デバッグ表示を新作専用Managerとして配置する。
- 固定参照はInspectorへ設定し、実行時の名前探索・`AddComponent`を使わない。

## 対象外

- タイトル遷移、セーブ、製品用演出

## 完了条件

- [x] 16:9以外でもゲーム領域が歪まない。
- [x] シーンにCamera、Light相当の2D描画、Canvas、EventSystemがある。
- [x] BGM、SE、デバッグ、入力がConsole Errorなしで起動する。

## 確認方法

1. シーンを16:9と非16:9で再生する。
2. WASD移動、BGM、HUDを確認する。
