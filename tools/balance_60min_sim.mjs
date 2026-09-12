/**
 * game02 簡易バランスシミュレーション（60分・バズ無し・ランダム期待値近似）
 */
const VIEWS_TICK = 5.0;
// シーンの initialViewsBaseMultiplier に相当（再調整用）
let INITIAL_VIEWS_MULT = 4.79;
const EARLY_RAND = (0.92 + 1.08) / 2;
const MID_RAND = (0.95 + 1.02) / 2;
const LATE_RAND = (0.70 + 1.0) / 2;
const POP_RAND = (0.95 + 1.05) / 2;

function computeSalePrice(base, mult, level) {
  level = Math.max(0, level);
  const raw = base * Math.pow(mult, level);
  const truncated = Math.floor(raw / 100) * 100;
  if (truncated < 1000) return 1;
  return truncated;
}

const SIDE_PANEL_SCALE = 0.329;

function streamingBase(n) {
  const nn = Math.max(0, n);
  return 15 + 105 * Math.pow(nn / 19, 1.4);
}

function streamingFinal(n, d) {
  const dd = Math.max(0, d);
  return streamingBase(n) * Math.pow(0.88, dd);
}

function targetViews(elapsed, p) {
  p = Math.max(0, p);
  if (elapsed <= 0) return 0;
  if (elapsed <= 100) {
    const t = elapsed / 100;
    return 0.35 * p * t;
  }
  if (elapsed <= 180) {
    const t = (elapsed - 100) / 80;
    return 0.35 * p + (0.5 * p - 0.35 * p) * t;
  }
  const lateSeconds = elapsed - 180;
  const lateTicks = lateSeconds / VIEWS_TICK;
  const lateBase = p / 100;
  return 0.5 * p + lateTicks * lateBase;
}

function stageRand(nextElapsed) {
  if (nextElapsed <= 100) return EARLY_RAND;
  if (nextElapsed <= 180) return MID_RAND;
  return LATE_RAND;
}

function moneyRate(pop) {
  if (pop < 10000) return 0.2;
  if (pop < 100000) return 0.4;
  if (pop < 1000000) return 0.7;
  return 1.0;
}

function applyUpgrade(st, name) {
  if (name === "ed01") st.ed01 = 1;
  else if (name === "ed02") st.ed02 = Math.min(3, st.ed02 + 1);
  else if (name === "ed03") st.ed03 = Math.min(5, st.ed03 + 1);
  else if (name === "ed04") st.ed04 = Math.min(5, st.ed04 + 1);
  else if (name === "ed05") st.ed05 = Math.min(3, st.ed05 + 1);
  else if (name === "ed51") st.ed51 = 1;
  else if (name === "st01") {
    st.T_stream += 1;
    st.talk_lv = Math.min(8, st.talk_lv + 1);
  } else if (name === "st02" || name === "st03") {
    st.T_stream += 1;
  } else if (name === "st04") {
    st.T_stream += 1;
    st.d_stream = Math.min(5, st.d_stream + 1);
  }
}

function buildPlan(mult) {
  const {
    ed02_bp, ed02_pm,
    ed03_bp, ed03_pm,
    ed04_bp, ed04_pm,
    ed05_bp, ed05_pm,
    ed51_bp, ed51_pm,
    st01_bp, st01_pm,
    st02_bp, st02_pm,
    st03_bp, st03_pm,
    st04_bp, st04_pm,
    ed01_price = 8000,
  } = mult;

  const events = [];
  const addStair = (tag, m1, m3, bp, pm, kind) => {
    const m2 = (m1 + m3) / 2;
    events.push([m1, kind, bp, pm, 0]);
    events.push([m2, kind, bp, pm, 1]);
    events.push([m3, kind, bp, pm, 2]);
  };

  events.push([2, "eb", 1000, 1, 0]);
  events.push([10, "eb", 1000, 1, 0]);
  events.push([30, "eb", 1000, 1, 0]);
  events.push([5, "ed01", ed01_price, 1, 0]);
  addStair("ed02", 7, 38, ed02_bp, ed02_pm, "ed02");
  addStair("ed03", 8, 39, ed03_bp, ed03_pm, "ed03");
  addStair("ed04", 9, 40, ed04_bp, ed04_pm, "ed04");
  addStair("ed05", 11, 45, ed05_bp, ed05_pm, "ed05");
  events.push([25, "ed51", ed51_bp, ed51_pm, 0]);
  addStair("st01", 4, 42, st01_bp, st01_pm, "st01");
  addStair("st02", 8, 28, st02_bp, st02_pm, "st02");
  addStair("st03", 9, 38, st03_bp, st03_pm, "st03");
  addStair("st04", 10, 50, st04_bp, st04_pm, "st04");

  events.sort((a, b) => a[0] - b[0]);

  const edLv = { ed02: 0, ed03: 0, ed04: 0, ed05: 0 };
  const stLv = { st01: 0, st02: 0, st03: 0, st04: 0 };
  const byMin = new Map();

  for (const [tmin, kind, bp, pm, lvOr0] of events) {
    const mn = Math.floor(tmin);
    if (!byMin.has(mn)) byMin.set(mn, []);
    if (kind === "eb") {
      byMin.get(mn).push(["editor", 1000]);
    } else if (kind === "ed01") {
      byMin.get(mn).push(["ed01", computeSalePrice(bp, pm, 0)]);
    } else if (kind === "ed51") {
      byMin.get(mn).push(["ed51", computeSalePrice(bp, pm, 0)]);
    } else if (edLv[kind] !== undefined) {
      const lv = edLv[kind];
      byMin.get(mn).push([kind, computeSalePrice(bp, pm, lv)]);
      edLv[kind]++;
    } else if (stLv[kind] !== undefined) {
      const lv = stLv[kind];
      byMin.get(mn).push([kind, computeSalePrice(bp, pm, lv)]);
      stLv[kind]++;
    }
  }

  return [...byMin.entries()].sort((a, b) => a[0] - b[0]);
}

function viewsTick(m, pOverride) {
  const p = Math.max(0, pOverride ?? m.p);
  let delta = 0;
  if (!m.initialDone) {
    let initial = Math.round(p * INITIAL_VIEWS_MULT);
    initial = Math.max(1, initial);
    m.initialDone = true;
    delta += initial;
  }
  const now = m.elapsed;
  const nxt = now + VIEWS_TICK;
  const baseDelta = Math.max(0, targetViews(nxt, p) - targetViews(now, p));
  const tickRaw = Math.max(0, baseDelta * stageRand(nxt));
  m.pendingFrac += tickRaw;
  let tickDelta = Math.floor(m.pendingFrac);
  if (tickDelta > 0) m.pendingFrac -= tickDelta;
  delta += tickDelta;
  const dpop = Math.round(delta * 0.2 * POP_RAND);
  m.elapsed = nxt;
  m.tickCount += 1;
  return { delta, dpop };
}

function runSim(plan, options = {}) {
  INITIAL_VIEWS_MULT = options.initialViewsMult ?? 4.79;
  const priceScale = options.priceScale ?? 1.0; // 1.0（スケールは buildPlan 側で反映済み）
  const st = {
    t: 0,
    pop: 100,
    money: 9900,
    minMoney: 9900,
    T_stream: 0,
    d_stream: 0,
    ed01: 0,
    ed02: 0,
    ed03: 0,
    ed04: 0,
    ed05: 0,
    ed51: 0,
    talk_lv: 0,
    total_money_in: 0,
    total_views: 0,
  };

  const spawnP = () => {
    const base = Math.max(0, st.pop);
    const adj = Math.round(base * (1 + 0.2 * Math.min(st.talk_lv, 8)));
    return Math.max(0, Math.round(adj * 1.0 * (1 + 0.2 * st.ed04)));
  };

  const streamSec = () => streamingFinal(Math.max(0, st.T_stream - st.d_stream), st.d_stream);
  const editSec = () => Math.max(1, Math.floor(120 * Math.pow(0.9, st.ed02)));
  const numEditors = () => {
    if (st.t < 120) return 0;
    if (st.t < 300) return 1;
    return 2;
  };

  const scheduleNext = (fromT) => {
    const s = streamSec();
    const e = editSec();
    const ne = numEditors();
    if (ne <= 0) return fromT + s + e;
    return fromT + Math.max(s, e / ne);
  };

  let movies = [{ p: spawnP(), elapsed: 0, pendingFrac: 0, pendingMoneyFrac: 0, pendingMonetized: 0, initialDone: false, tickCount: 0 }];
  let nextSpawn = scheduleNext(0);
  let purchaseIdx = 0;

  const maxSlots = () => 2 + st.ed05;

  const snapshots = [];
  for (let m = 0; m <= 60; m++) snapshots.push({ min: m, pop: 0, money: 0, totalIn: 0 });

  function spawnMovie() {
    const nm = { p: spawnP(), elapsed: 0, pendingFrac: 0, pendingMoneyFrac: 0, pendingMonetized: 0, initialDone: false, tickCount: 0 };
    movies.unshift(nm);
    while (movies.length > maxSlots()) movies.pop();
  }

  while (st.t < 3600 + 1e-6) {
    const minute = Math.floor(st.t / 60);
    while (purchaseIdx < plan.length && plan[purchaseIdx][0] <= minute) {
      const [, pairs] = plan[purchaseIdx];
      for (const [name, price] of pairs) {
        const scaled =
          name === "editor"
            ? price
            : Math.max(1, Math.floor(price * SIDE_PANEL_SCALE * priceScale));
        st.money -= scaled;
        st.minMoney = Math.min(st.minMoney, st.money);
        if (name !== "editor") applyUpgrade(st, name);
      }
      purchaseIdx++;
    }

    while (st.t + 1e-6 >= nextSpawn) {
      spawnMovie();
      nextSpawn = scheduleNext(nextSpawn);
    }

    let dpopTotal = 0;
    for (const m of movies) {
      const { delta, dpop } = viewsTick(m, m.p);
      if (delta > 0) {
        st.total_views += delta;
        m.pendingMonetized += delta;
      }
      dpopTotal += dpop;
      if (m.tickCount % 2 === 0) {
        const payable = Math.floor(m.pendingMonetized / 100);
        if (payable > 0) {
          const pv = payable * 100;
          m.pendingMonetized -= pv;
          const rate = moneyRate(m.p);
          const raw = pv * rate + m.pendingMoneyFrac;
          const dm = Math.floor(Math.max(0, raw));
          m.pendingMoneyFrac = Math.max(0, raw - dm);
          st.money += dm;
          st.total_money_in += dm;
          st.minMoney = Math.min(st.minMoney, st.money);
        }
      }
    }
    st.pop = Math.max(0, st.pop + dpopTotal);

    const snapMin = Math.floor(st.t / 60);
    if (snapMin <= 60 && snapshots[snapMin]) {
      snapshots[snapMin].pop = st.pop;
      snapshots[snapMin].money = st.money;
      snapshots[snapMin].totalIn = st.total_money_in;
    }

    st.t += VIEWS_TICK;
  }

  return { st, snapshots };
}

// --- main ---
const currentMult = {
  ed02_bp: 1000, ed02_pm: 1.35,
  ed03_bp: 1000, ed03_pm: 1.35,
  ed04_bp: 1000, ed04_pm: 1.32,
  ed05_bp: 1200, ed05_pm: 1.35,
  ed51_bp: 1500, ed51_pm: 1.0,
  st01_bp: 1000, st01_pm: 1.22,
  st02_bp: 1000, st02_pm: 1.22,
  st03_bp: 1200, st03_pm: 1.28,
  st04_bp: 1500, st04_pm: 1.25,
  ed01_price: 5000,
};

const plan = buildPlan(currentMult);

function findPriceScale(ivm) {
  let lo = 0;
  let hi = 1.0;
  for (let i = 0; i < 45; i++) {
    const mid = (lo + hi) / 2;
    const { st } = runSim(plan, { priceScale: mid, initialViewsMult: ivm });
    if (st.minMoney >= 0 && st.money >= 0) lo = mid;
    else hi = mid;
  }
  return lo;
}

const ivmTarget = 0.0188;
const ps = findPriceScale(ivmTarget);
const { st, snapshots } = runSim(plan, { priceScale: ps, initialViewsMult: ivmTarget });
let totalSpend = 0;
for (const [, pairs] of plan) {
  for (const [n, pr] of pairs) {
    totalSpend += n === "editor" ? pr : Math.max(1, Math.floor(pr * SIDE_PANEL_SCALE * ps));
  }
}
console.log("initialViewsMult", ivmTarget, "PRICE_SCALE", ps, "SCALED_TOTAL_SPEND", totalSpend);
console.log("FINAL pop", Math.round(st.pop), "minMoney", Math.round(st.minMoney), "money", Math.round(st.money));
console.log("SNAPSHOTS every 10m (cumulative_gross = 初期9900+累計売上):");
for (let m = 0; m <= 60; m += 10) {
  const s = snapshots[m];
  const cumulative = 9900 + s.totalIn;
  console.log(`${m}m pop=${Math.round(s.pop)} money=${Math.round(s.money)} cumulative_gross=${Math.round(cumulative)}`);
}
