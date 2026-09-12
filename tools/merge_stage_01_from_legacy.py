"""
stage_01-enemy01.csv / stage_01-enemy11.csv / stage_01-enemy12.json を読み、
演出時間（60〜66 秒・126〜132 秒の無スポーン）を反映した spawnTime で stage_01.json を生成する。

spawnTime 変換（元時刻 t）:
  t < 60     → t
  60 <= t < 120 → t + 6
  t >= 120   → t + 12
"""
import csv
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(ROOT, "Assets", "GameData", "EnemySpawn")
CSV01 = os.path.join(DIR, "stage_01-enemy01.csv")
CSV11 = os.path.join(DIR, "stage_01-enemy11.csv")
JSON12 = os.path.join(DIR, "stage_01-enemy12.json")
OUT = os.path.join(DIR, "stage_01.json")


def shift_spawn_time(t: float) -> float:
    if t < 60.0:
        return t
    if t < 120.0:
        return t + 6.0
    return t + 12.0


def load_enemy01():
    rows = []
    with open(CSV01, "r", encoding="utf-8") as f:
        r = csv.DictReader(f)
        for row in r:
            t = float(row["spawn_time_sec"])
            rows.append(
                {
                    "enemyType": "Enemy01",
                    "spawnTime": shift_spawn_time(t),
                    "initialExpandTime": float(row["initial_expand_time"]),
                    "initialStopTime": float(row["initial_stop_time"]),
                    "moveSpeed": float(row["move_speed"]),
                    "objectDirection": float(row["object_direction"]),
                    "objectAngle": float(row["object_angle"]),
                    "objectThickness": float(row["object_thickness"]),
                    "scaleRate": float(row["scale_rate"]),
                }
            )
    return rows


def load_enemy11():
    rows = []
    with open(CSV11, "r", encoding="utf-8") as f:
        r = csv.DictReader(f)
        for row in r:
            t = float(row["spawn_time_sec"])
            rows.append(
                {
                    "enemyType": "Enemy11",
                    "spawnTime": shift_spawn_time(t),
                    "spawnX": float(row["spawn_x"]),
                    "spawnY": float(row["spawn_y"]),
                    "moveSpeed": float(row["speed"]),
                    "directionDeg": float(row["direction_deg"]),
                    "size": float(row["size"]),
                }
            )
    return rows


def load_enemy12():
    with open(JSON12, "r", encoding="utf-8") as f:
        root = json.load(f)
    spawns = root.get("spawns") or []
    rows = []
    for s in spawns:
        t = float(s.get("spawn_time_sec", s.get("spawnTime", 0)))
        rows.append(
            {
                "enemyType": "Enemy12",
                "spawnTime": shift_spawn_time(t),
                "spawnX": float(s["spawn_x"]),
                "spawnY": float(s["spawn_y"]),
                "moveDirectionDeg": float(s["move_direction_deg"]),
                "baseSpeed": float(s["base_speed"]),
                "playerHomingStrength": float(s["player_homing_strength"]),
                "maxTurnRateDegPerSec": float(s["max_turn_rate_deg_per_sec"]),
                "size": float(s["size"]),
                "spriteName": str(s["sprite_name"]),
            }
        )
    return rows


def main():
    spawns = []
    spawns.extend(load_enemy01())
    spawns.extend(load_enemy11())
    spawns.extend(load_enemy12())
    spawns.sort(key=lambda e: (e["spawnTime"], e["enemyType"]))

    root = {"version": 1, "stageName": "stage_01", "spawns": spawns}
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(root, f, indent=2, ensure_ascii=False)

    tmax = max(e["spawnTime"] for e in spawns) if spawns else None
    print("wrote", OUT, "entries", len(spawns), "t_max", tmax)


if __name__ == "__main__":
    main()
