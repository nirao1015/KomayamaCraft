# TASK-M09-05 セーブ互換と移行

## 目的

旧versionのセーブを、隣接地移行で現行へ上げられるようにする。

## 依存タスク

- TASK-M09-04
- TASK-M00-07

## 作業範囲

- `IKomayamaCraftSaveMigration`の実装をversion差の分だけ置く。
- 欠損値を補完する。
- 未知versionは安全に失敗し、上書きしない。

## 対象外

- 旧作game01〜03セーブ
- Steam Cloud

## 完了条件

- [ ] 少なくとも1世代前のサンプルを現行へ移行できる。
- [ ] 未知versionで本体セーブを壊さない。

## 確認方法

1. 古いversionのJSONを読む。
2. 現行でロードできることを確認する。
3. 未来versionを拒否することを確認する。
