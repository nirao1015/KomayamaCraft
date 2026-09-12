"""
stage_01_bk.json の spawns をそのまま保持し、spawnTime のみシフトして stage_01.json を出力する。

- 元タイムライン: フェーズ 30 秒 × 6（0〜180 秒相当）、T0=0。
- フェーズ2終了〜フェーズ3開始の間に 6 秒の演出（スポーンなし）→ 60 秒以上 120 未満の spawnTime に +6。
- フェーズ4終了〜フェーズ5開始の間に 6 秒の演出 → 120 秒以上の spawnTime にさらに +6（累計 +12）。

つまり:
  t < 60   → そのまま
  60 <= t < 120 → t + 6
  t >= 120 → t + 12
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Assets", "GameData", "EnemySpawn", "stage_01_bk.json")
DST = os.path.join(ROOT, "Assets", "GameData", "EnemySpawn", "stage_01.json")


def shift_time(t: float) -> float:
    if t < 60.0:
        return t
    if t < 120.0:
        return t + 6.0
    return t + 12.0


def main():
    with open(SRC, "r", encoding="utf-8") as f:
        root = json.load(f)

    spawns = root.get("spawns") or []
    for entry in spawns:
        if "spawnTime" in entry:
            entry["spawnTime"] = shift_time(float(entry["spawnTime"]))
        elif "spawn_time_sec" in entry:
            entry["spawn_time_sec"] = shift_time(float(entry["spawn_time_sec"]))

    with open(DST, "w", encoding="utf-8") as f:
        json.dump(root, f, indent=2, ensure_ascii=False)

    times = [float(e.get("spawnTime", e.get("spawn_time_sec", -1))) for e in spawns]
    print("wrote", DST, "from", SRC, "count", len(spawns), "t_min", min(times) if times else None, "t_max", max(times) if times else None)


if __name__ == "__main__":
    main()
