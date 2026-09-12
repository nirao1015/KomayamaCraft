"""Generate Assets/GameData/EnemySpawn/stage_01.json — run from project root: python tools/gen_stage_01_spawn.py"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "GameData", "EnemySpawn", "stage_01.json")


def e01(t, direction, angle, thick=0.33, speed=2.4, expand=0.95, stop=0.5, scale=0.32):
    return {
        "enemyType": "Enemy01",
        "spawnTime": float(t),
        "initialExpandTime": expand,
        "initialStopTime": stop,
        "moveSpeed": speed,
        "objectDirection": float(direction),
        "objectAngle": float(angle),
        "objectThickness": thick,
        "scaleRate": scale,
    }


def e11(t, x, y, deg, spd, sz=1.0):
    return {
        "enemyType": "Enemy11",
        "spawnTime": float(t),
        "spawnX": float(x),
        "spawnY": float(y),
        "moveSpeed": float(spd),
        "directionDeg": float(deg),
        "size": float(sz),
    }


def e12(t, x, y, md, bs=2.0, hs=1.2, tr=160.0, sz=1.0, sn="enemy_08.png"):
    return {
        "enemyType": "Enemy12",
        "spawnTime": float(t),
        "spawnX": x,
        "spawnY": y,
        "moveDirectionDeg": float(md),
        "baseSpeed": bs,
        "playerHomingStrength": hs,
        "maxTurnRateDegPerSec": tr,
        "size": sz,
        "spriteName": sn,
    }


def main():
    # 6 phases x 30s; 6s no-spawn after phase2 and phase4 (before P3 and P5)
    phase_starts = [0, 30, 66, 96, 132, 162]
    wave_offsets = [0, 5, 10, 15, 20, 25]

    phase_cfg = [
        {"e01_speed": 2.1, "e01_ang": (82, 98), "e01_scale": 0.28, "e11_spd": 3.8, "e11_sz": 0.95, "e11_n": 3},
        {"e01_speed": 2.35, "e01_ang": (95, 115), "e01_scale": 0.30, "e11_spd": 4.2, "e11_sz": 1.0, "e11_n": 3},
        {"e01_speed": 2.55, "e01_ang": (110, 135), "e01_scale": 0.33, "e11_spd": 4.6, "e11_sz": 1.08, "e11_n": 4},
        {"e01_speed": 2.75, "e01_ang": (125, 155), "e01_scale": 0.36, "e11_spd": 5.0, "e11_sz": 1.14, "e11_n": 4},
        {"e01_speed": 2.95, "e01_ang": (145, 175), "e01_scale": 0.39, "e11_spd": 5.5, "e11_sz": 1.22, "e11_n": 5},
        {"e01_speed": 3.15, "e01_ang": (165, 195), "e01_scale": 0.42, "e11_spd": 6.0, "e11_sz": 1.30, "e11_n": 5},
    ]

    # (y, directionDeg) — 左端生成は約180台、右端生成は約0台で画面内へ侵入
    rows_l3 = [(4.0, 208.0), (0.0, 188.0), (-4.0, 168.0)]
    rows_l4 = [(4.2, 212.0), (1.4, 198.0), (-1.4, 178.0), (-4.2, 158.0)]
    rows_l5 = [(5.0, 218.0), (2.5, 200.0), (0.0, 185.0), (-2.5, 168.0), (-5.0, 152.0)]
    rows_r3 = [(4.0, 332.0), (0.0, 352.0), (-4.0, 12.0)]
    rows_r4 = [(4.2, 328.0), (1.4, 342.0), (-1.4, 8.0), (-4.2, 22.0)]
    rows_r5 = [(5.0, 325.0), (2.5, 340.0), (0.0, 355.0), (-2.5, 18.0), (-5.0, 32.0)]

    spawns = []

    for pi, pstart in enumerate(phase_starts):
        cfg = phase_cfg[pi]
        for wi, off in enumerate(wave_offsets):
            t = pstart + off
            direction = 0 if (pi + wi) % 2 == 0 else 180
            ang_lo, ang_hi = cfg["e01_ang"]
            angle = ang_lo + (ang_hi - ang_lo) * (wi / 5.0)
            spawns.append(
                e01(
                    t + 0.05,
                    direction,
                    angle,
                    speed=cfg["e01_speed"],
                    scale=cfg["e01_scale"],
                )
            )
            if pi >= 2 and wi in (1, 4):
                direction2 = 90 if wi == 1 else 270
                spawns.append(
                    e01(
                        t + 0.15,
                        direction2,
                        angle * 0.85,
                        speed=cfg["e01_speed"] * 0.92,
                        scale=cfg["e01_scale"] * 0.9,
                    )
                )

            n = cfg["e11_n"]
            from_left = (pi + wi) % 2 == 0
            x = 11.52 if from_left else -11.52
            if from_left:
                rows = {3: rows_l3, 4: rows_l4, 5: rows_l5}[n]
            else:
                rows = {3: rows_r3, 4: rows_r4, 5: rows_r5}[n]
            spd = cfg["e11_spd"] + wi * 0.08
            sz = cfg["e11_sz"] + wi * 0.02
            for yi, (yy, deg) in enumerate(rows[:n]):
                spawns.append(e11(t + 0.02 * yi, x, yy, deg, spd, sz))

            if pi >= 2 and wi in (2, 5):
                spawns.append(
                    e12(
                        t + 0.3,
                        11.52 if wi == 2 else -11.52,
                        2.8,
                        135.0 if wi == 2 else 45.0,
                    )
                )

    spawns.sort(key=lambda s: (s["spawnTime"], s["enemyType"]))

    root = {"version": 1, "stageName": "stage_01", "spawns": spawns}
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(root, f, indent=4, ensure_ascii=False)
    print("wrote", OUT, "entries", len(spawns), "t_max", max(s["spawnTime"] for s in spawns))


if __name__ == "__main__":
    main()
