"""
game02 簡易バランスシミュレーション（60分・バズ無し・ランダム期待値近似）。
work_upload / WorkMovieUploadController の式に合わせた離散 5 秒ティック。
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field
from typing import List, Tuple

# --- WorkMovie（コード準拠） ---
VIEWS_TICK = 5.0
MONEY_TICK = 10.0
INITIAL_VIEWS_MULT = 4.79
# ランダムは期待値で固定（仕様書用の再現性）
EARLY_RAND = (0.92 + 1.08) / 2
MID_RAND = (0.95 + 1.02) / 2
LATE_RAND = (0.70 + 1.00) / 2
POP_RAND = (0.95 + 1.05) / 2


def compute_sale_price(base: int, mult: float, level: int) -> int:
    level = max(0, level)
    raw = base * (mult**level)
    if raw < 0 or math.isnan(raw) or math.isinf(raw):
        return 1
    truncated = int(math.floor(raw / 100.0) * 100.0)
    if truncated < 1000:
        return 1
    return truncated


def streaming_base(n: int) -> float:
    nn = max(0, n)
    return 15.0 + 105.0 * ((nn / 19.0) ** 1.4)


def streaming_final(n: int, d: int) -> float:
    dd = max(0, d)
    return streaming_base(n) * (0.88**dd)


def target_views(elapsed: float, p: float) -> float:
    p = max(0.0, p)
    if elapsed <= 0:
        return 0.0
    if elapsed <= 100:
        t = elapsed / 100.0
        return 0.35 * p * t
    if elapsed <= 180:
        t = (elapsed - 100.0) / 80.0
        return (0.35 * p) + ((0.5 * p - 0.35 * p) * t)
    late_seconds = elapsed - 180.0
    late_ticks = late_seconds / VIEWS_TICK
    late_base = p / 100.0
    return 0.5 * p + (late_ticks * late_base)


def stage_rand(next_elapsed: float) -> float:
    if next_elapsed <= 100:
        return EARLY_RAND
    if next_elapsed <= 180:
        return MID_RAND
    return LATE_RAND


def money_rate(pop: int) -> float:
    if pop < 10_000:
        return 0.2
    if pop < 100_000:
        return 0.4
    if pop < 1_000_000:
        return 0.7
    return 1.0


@dataclass
class Movie:
    p: int
    elapsed: float = 0.0
    pending_frac: float = 0.0
    pending_money_frac: float = 0.0
    pending_monetized: int = 0
    initial_done: bool = False
    tick_count: int = 0
    money_acc: int = 0


@dataclass
class SimState:
    t: float = 0.0
    pop: int = 100
    money: int = 9900
    T_stream: int = 0
    d_stream: int = 0
    ed01: int = 0
    ed02: int = 0
    ed03: int = 0
    ed04: int = 0
    ed05: int = 0
    ed51: int = 0
    talk_lv: int = 0
    movies: List[Movie] = field(default_factory=list)
    max_slots: int = 2
    # 累計（参考）
    total_money_in: int = 0
    total_views: int = 0

    def edit_mult(self) -> float:
        return (0.9**self.ed02)

    def ed04_mult(self) -> float:
        return 1.0 + 0.2 * self.ed04

    def stream_n(self) -> int:
        return max(0, self.T_stream - self.d_stream)

    def stream_sec(self) -> float:
        return streaming_final(self.stream_n(), self.d_stream)

    def edit_sec(self) -> float:
        return max(1.0, math.floor(120.0 * self.edit_mult()))

    def num_item_editors(self) -> int:
        # 0〜2m: MailChara が配信＋編集の連結パイプライン（ne=0）
        # 2m〜5m: 配信専念＋編集枠1（ItemEditor）
        # 5m〜: 編集枠2
        if self.t < 120:
            return 0
        if self.t < 300:
            return 1
        return 2

    def talk_mult(self) -> float:
        return 1.0 + 0.2 * min(self.talk_lv, 8)

    def spawn_movie_p(self) -> int:
        base = max(0, self.pop)
        adj = int(round(base * self.talk_mult()))
        return max(0, int(round(adj * 1.0 * self.ed04_mult())))

    def apply_purchases(self, minute: int, costs: List[Tuple[str, int]]):
        for name, price in costs:
            self.money -= price


def views_tick(m: Movie) -> Tuple[int, int]:
    """delta_views, popularity_delta (approx)"""
    p = max(0, m.p)
    delta = 0
    if not m.initial_done:
        initial = int(round(p * INITIAL_VIEWS_MULT))
        initial = max(1, initial)
        m.initial_done = True
        delta += initial

    now = m.elapsed
    nxt = now + VIEWS_TICK
    base_delta = max(0.0, target_views(nxt, p) - target_views(now, p))
    tick_raw = max(0.0, base_delta * stage_rand(nxt))
    m.pending_frac += tick_raw
    tick_delta = int(math.floor(m.pending_frac))
    if tick_delta > 0:
        m.pending_frac -= tick_delta
    delta += tick_delta

    dpop = int(round(delta * 0.2 * POP_RAND))
    m.elapsed = nxt
    m.tick_count += 1
    return delta, dpop


def money_subtick(m: Movie) -> int:
    m.pending_monetized += 0  # views already added in caller
    return 0


def run_sim(
    purchase_plan: List[Tuple[float, List[Tuple[str, int]]]],
    duration_s: float = 3600.0,
) -> SimState:
    st = SimState()
    st.movies.append(Movie(p=st.spawn_movie_p()))  # t=0 で1本投入
    views_accum = 0.0
    purchase_idx = 0

    # 次の動画投入時刻（パイプライン）
    def schedule_next_movie(from_t: float) -> float:
        s = st.stream_sec()
        e = st.edit_sec()
        ne = st.num_item_editors()
        if ne <= 0:
            return from_t + s + e
        # ストリーム1本あたり e 秒で1編集が消化、同時に ne 本並列
        # 近似: 周期 max(s, e/ne) は粗い。ここではボトルネック側を採用
        per_movie = max(s, e / ne)
        return from_t + per_movie

    next_spawn = schedule_next_movie(0.0)

    while st.t < duration_s + 1e-6:
        minute = int(st.t // 60)

        while purchase_idx < len(purchase_plan) and purchase_plan[purchase_idx][0] <= minute:
            _, costs = purchase_plan[purchase_idx]
            st.apply_purchases(minute, costs)
            purchase_idx += 1

        # スロット数更新
        st.max_slots = 2 + st.ed05

        # 新規映画
        while len(st.movies) < st.max_slots and st.t + 1e-6 >= next_spawn:
            st.movies.append(Movie(p=st.spawn_movie_p()))
            next_spawn = schedule_next_movie(next_spawn)

        dpop_total = 0
        for m in st.movies:
            dv, dp = views_tick(m)
            if dv > 0:
                st.total_views += dv
            dpop_total += dp
            views_accum += VIEWS_TICK
            # 10秒ごとの金銭: tick_count が 2 回で 10 秒相当（5秒×2）
            if m.tick_count % 2 == 0:
                payable = m.pending_monetized // 100
                if payable > 0:
                    pv = payable * 100
                    m.pending_monetized -= pv
                    rate = money_rate(m.p)
                    raw = pv * rate + m.pending_money_frac
                    dm = int(math.floor(max(0.0, raw)))
                    m.pending_money_frac = max(0.0, raw - dm)
                    st.money += dm
                    st.total_money_in += dm

        st.pop = max(0, st.pop + dpop_total)
        st.t += VIEWS_TICK

    return st


def build_purchase_plan_from_multipliers(
    ed02_bp, ed02_pm,
    ed03_bp, ed03_pm,
    ed04_bp, ed04_pm,
    ed05_bp, ed05_pm,
    ed51_bp, ed51_pm,
    st01_bp, st01_pm,
    st02_bp, st02_pm,
    st03_bp, st03_pm,
    st04_bp, st04_pm,
    ed01_price: int = 8000,
) -> List[Tuple[float, List[Tuple[str, int]]]]:
    """分単位でまとめた購入（同一分はリスト）。2回目は中点補間。"""
    def ed_lv_cost(bp, pm, lv):
        return ("ed", compute_sale_price(bp, pm, lv))

    def st_lv_cost(bp, pm, lv):
        return ("st", compute_sale_price(bp, pm, lv))

    # レベルは購入回数0始まり
    events: List[Tuple[float, str, int, Tuple]] = []

    def add_stair(tag, m1, m3, bp, pm, kind):
        m2 = (m1 + m3) / 2.0
        events.append((m1, tag, 0, (kind, bp, pm)))
        events.append((m2, tag, 1, (kind, bp, pm)))
        events.append((m3, tag, 2, (kind, bp, pm)))

    # EditorBuy は固定1000想定
    events.append((2.0, "eb01", 0, ("fixed", 1000)))
    events.append((10.0, "eb02", 0, ("fixed", 1000)))
    events.append((30.0, "eb03", 0, ("fixed", 1000)))

    events.append((5.0, "ed01", 0, ("ed01", ed01_price, 1.0)))

    add_stair("ed02", 7, 38, ed02_bp, ed02_pm, "ed02")
    add_stair("ed03", 8, 39, ed03_bp, ed03_pm, "ed03")
    add_stair("ed04", 9, 40, ed04_bp, ed04_pm, "ed04")
    add_stair("ed05", 11, 45, ed05_bp, ed05_pm, "ed05")

    events.append((25.0, "ed51", 0, ("ed51", ed51_bp, ed51_pm)))

    add_stair("st01", 4, 42, st01_bp, st01_pm, "st01")
    add_stair("st02", 8, 28, st02_bp, st02_pm, "st02")
    add_stair("st03", 9, 38, st03_bp, st03_pm, "st03")
    add_stair("st04", 10, 50, st04_bp, st04_pm, "st04")

    # 分ごとに集約
    from collections import defaultdict

    by_min: dict[int, List[Tuple[str, int]]] = defaultdict(list)
    st_levels = {"st01": 0, "st02": 0, "st03": 0, "st04": 0}
    ed_levels = {"ed02": 0, "ed03": 0, "ed04": 0, "ed05": 0}

    for tmin, _tag, _idx, payload in sorted(events, key=lambda x: x[0]):
        kind = payload[0]
        if kind == "fixed":
            by_min[int(tmin)].append(("editor", payload[1]))
        elif kind == "ed01":
            by_min[int(tmin)].append(("ed01", compute_sale_price(payload[1], payload[2], 0)))
        elif kind == "ed51":
            by_min[int(tmin)].append(("ed51", compute_sale_price(payload[1], payload[2], 0)))
        elif kind in ed_levels:
            lv = ed_levels[kind]
            by_min[int(tmin)].append((kind, compute_sale_price(payload[1], payload[2], lv)))
            ed_levels[kind] += 1
        elif kind in st_levels:
            lv = st_levels[kind]
            by_min[int(tmin)].append((kind, compute_sale_price(payload[1], payload[2], lv)))
            st_levels[kind] += 1

    plan: List[Tuple[float, List[Tuple[str, int]]]]] = []
    for mn in sorted(by_min.keys()):
        plan.append((float(mn), by_min[mn]))
    return plan


def apply_upgrade_side_effects(state: SimState, name: str):
    if name == "ed01":
        state.ed01 = 1
    elif name == "ed02":
        state.ed02 = min(3, state.ed02 + 1)
    elif name == "ed03":
        state.ed03 = min(5, state.ed03 + 1)
    elif name == "ed04":
        state.ed04 = min(5, state.ed04 + 1)
    elif name == "ed05":
        state.ed05 = min(3, state.ed05 + 1)
    elif name == "ed51":
        state.ed51 = 1
    elif name == "st01":
        state.T_stream += 1
        state.talk_lv = min(8, state.talk_lv + 1)
    elif name == "st02":
        state.T_stream += 1
    elif name == "st03":
        state.T_stream += 1
    elif name == "st04":
        state.T_stream += 1
        state.d_stream = min(5, state.d_stream + 1)


# --- 改良シミュ: 購入時にアップグレード状態を更新 ---
def run_sim_with_upgrades(plan: List[Tuple[float, List[Tuple[str, int]]]]) -> SimState:
    st = SimState()
    st.movies = [Movie(p=st.spawn_movie_p())]
    purchase_idx = 0

    def schedule_next_movie(from_t: float) -> float:
        s = st.stream_sec()
        e = st.edit_sec()
        ne = st.num_item_editors()
        if ne <= 0:
            return from_t + s + e
        per_movie = max(s, e / ne)
        return from_t + per_movie

    next_spawn = schedule_next_movie(0.0)

    while st.t < 3600.0 + 1e-6:
        minute = int(st.t // 60)

        while purchase_idx < len(plan) and plan[purchase_idx][0] <= minute:
            _, pairs = plan[purchase_idx]
            for name, price in pairs:
                st.money -= price
                if not name.startswith("editor"):
                    apply_upgrade_side_effects(st, name)
            purchase_idx += 1

        st.max_slots = 2 + st.ed05

        while len(st.movies) < st.max_slots and st.t + 1e-6 >= next_spawn:
            st.movies.append(Movie(p=st.spawn_movie_p()))
            next_spawn = schedule_next_movie(next_spawn)

        dpop_total = 0
        for m in st.movies:
            dv, dp = views_tick(m)
            if dv > 0:
                st.total_views += dv
                m.pending_monetized += dv
            dpop_total += dp
            if m.tick_count % 2 == 0:
                payable = m.pending_monetized // 100
                if payable > 0:
                    pv = payable * 100
                    m.pending_monetized -= pv
                    rate = money_rate(m.p)
                    raw = pv * rate + m.pending_money_frac
                    dm = int(math.floor(max(0.0, raw)))
                    m.pending_money_frac = max(0.0, raw - dm)
                    st.money += dm
                    st.total_money_in += dm

        st.pop = max(0, st.pop + dpop_total)
        st.t += VIEWS_TICK

    return st


if __name__ == "__main__":
    # 現行コード既定の倍率・基礎価格（St は const）
    plan = build_purchase_plan_from_multipliers(
        ed02_bp=50000,
        ed02_pm=4.0,
        ed03_bp=100000,
        ed03_pm=4.0,
        ed04_bp=1000,
        ed04_pm=4.0,
        ed05_bp=150000,
        ed05_pm=4.0,
        ed51_bp=200000,
        ed51_pm=1.0,
        st01_bp=1000,
        st01_pm=5.10210,
        st02_bp=3000,
        st02_pm=4.6,
        st03_bp=80000,
        st03_pm=8.2,
        st04_bp=320000,
        st04_pm=3.2,
        ed01_price=8000,
    )
    st = run_sim_with_upgrades(plan)
    print("pop_60m", st.pop, "money", st.money, "total_in", st.total_money_in, "views", st.total_views)
