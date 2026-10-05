// 용의 귀환 · 자리 비움 사냥 (Cloudflare Durable Object)
// AI를 켠 채 게임을 끄면 캐릭터가 여기서 계속 사냥합니다. 다시 접속하면 그동안 얻은 경험치·골드·잡템을 받습니다.
// 사냥하는 동안 게임과 같은 MQTT 브로커에 위치·공격을 보내서, 다른 플레이어에게 「이름·AI」로 보입니다.
// 사냥 중인 캐릭터가 있을 때만 2초마다 깨어나고, 없으면 잠듭니다.
import { DurableObject } from 'cloudflare:workers';
import { connect } from 'cloudflare:sockets';

const MAX_SEC = 24 * 3600, MAX_SESSIONS = 300, TICK_MS = 2000;
const CLASSES = ['novice', 'warrior', 'rogue', 'shaman'];
const SKILL_MUL = 1.6;   // AI also uses class skills; a basic-attack-only estimate undershoots

// ---- game formulas (same as the client) ----
const needExp = L => Math.floor(90 * Math.pow(L, 1.55));
const mobStats = (lv, role, bm) => ({
  lv, hp: Math.round((7 * Math.pow(lv, 1.35) * role + 30) * bm.hp), atk: Math.round((4 + 1.6 * Math.pow(lv, 1.3)) * role),
  exp: Math.round(9 * Math.pow(lv, 1.5) * role * bm.exp), gold: [lv * 6 * bm.gold, lv * 11 * bm.gold],
});
const rand = (a, b) => a + Math.random() * (b - a);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const num = (v, a, b, d) => (typeof v === 'number' && isFinite(v) ? clamp(v, a, b) : d);
const str = (v, n) => (typeof v === 'string' ? v.slice(0, n) : '');
const hex = b => [...new Uint8Array(b)].map(x => x.toString(16).padStart(2, '0')).join('');
const hashTok = async t => hex(await crypto.subtle.digest('SHA-256', new TextEncoder().encode('rotd-idle:' + t)));
const tokOk = t => typeof t === 'string' && t.length >= 16 && t.length <= 64;

// what the game sends when it is closed with AI on (all values are clamped)
function cleanStart(b) {
  const name = str(b.name, 12).trim();
  if (!name || !CLASSES.includes(b.cls) || ['__proto__', 'constructor', 'prototype'].includes(name)) return null;
  const st = b.stats || {}, gr = b.grow || {};
  const mobs = (Array.isArray(b.mobs) ? b.mobs : []).slice(0, 12).map(m => ({
    type: str(m.type, 24), name: str(m.name, 24), lo: num(m.lo, 1, 99, 1), hi: num(m.hi, 1, 99, 1), role: num(m.role, .2, 4, 1),
    bm: { hp: num(m.bm && m.bm.hp, .2, 5, 1), exp: num(m.bm && m.bm.exp, .2, 5, 1), gold: num(m.bm && m.bm.gold, .2, 5, 1) },
    junk: str(m.junk, 24), x: num(m.x, -400, 400, 0), z: num(m.z, -400, 400, 0),
  })).filter(m => m.type && m.hi >= m.lo);
  if (!mobs.length) return null;
  return {
    name, cls: b.cls, srv: num(b.srv, 0, 9, 0) | 0, zone: str(b.zone, 16).replace(/[^a-z]/g, ''), awk: b.awk ? 1 : 0, ti: num(b.ti, 0, 3, 0) | 0,
    level: num(b.level, 1, 99, 1) | 0, exp: num(b.exp, 0, 1e12, 0),
    stats: { atk: num(st.atk, 1, 1e7, 10), def: num(st.def, 0, 1e6, 0), maxHp: num(st.maxHp, 10, 1e8, 100), aspd: num(st.aspd, .25, 3, 1), crit: num(st.crit, 0, 1, .1), critX: num(st.critX, 1, 6, 1.5) },
    grow: { atk: num(gr.atk, 0, 1e5, 0), def: num(gr.def, 0, 1e4, 0), maxHp: num(gr.maxHp, 0, 1e6, 0) },
    potions: num(b.potions, 0, 9999, 0) | 0, guildMul: num(b.guildMul, 1, 1.5, 1), mobs,
  };
}

// ---- the fight, one step per second ----
function pickMob(s) { let best = null, bs = -1e9; for (const m of s.mobs) { const sc = m.lo <= s.level + 1 ? m.lo : -m.lo; if (sc > bs) { bs = sc; best = m; } } return best; }
function nextMob(s) {
  const m = pickMob(s); s.spot = { x: m.x, z: m.z };
  s.mob = Object.assign(mobStats(Math.floor(rand(m.lo, m.hi + 1)), m.role, m.bm), { type: m.type, junk: m.junk });
  s.phase = 'walk'; s.wait = rand(2, 4);
}
function step(s) {
  if (s.ended) return;
  s.sec++;
  if (s.sec >= MAX_SEC) { s.ended = true; s.log.push('최대 사냥 시간(24시간)이 지나 마을로 돌아왔습니다'); return; }
  const P = s.stats;
  if (s.phase === 'dead') { if (--s.wait <= 0) { s.hp = P.maxHp; nextMob(s); } return; }
  if (s.phase === 'walk') { if (--s.wait <= 0) s.phase = 'fight'; return; }
  const M = s.mob, hit = M.lv > s.level ? Math.max(.7, 1 - (M.lv - s.level) * .03) : 1;
  M.hp -= P.atk / P.aspd * (1 + P.crit * (P.critX - 1)) * hit * SKILL_MUL;
  s.hp -= M.atk * 500 / (500 + P.def) * .8;
  s.potCd = Math.max(0, (s.potCd || 0) - 1);
  if (s.hp < P.maxHp * .45 && s.potions > 0 && s.potCd <= 0) { s.potions--; s.potUsed++; s.potCd = 3; s.hp = Math.min(P.maxHp, s.hp + P.maxHp * .4); }
  if (s.hp <= 0) {
    s.deaths++; s.phase = 'dead'; s.wait = 90; s.hp = 0;
    if (s.deaths <= 20) s.log.push('Lv.' + M.lv + ' 몬스터에게 쓰러져 마을에서 다시 출발했습니다');
    return;
  }
  if (M.hp <= 0) {
    const x = Math.round(M.exp * s.guildMul), g = Math.round(rand(M.gold[0], M.gold[1]));
    s.kills++; s.gotExp += x; s.gold += g; s.exp += x;
    if (M.junk && Math.random() < .6) s.junk[M.junk] = (s.junk[M.junk] || 0) + 1;
    while (s.level < 99 && s.exp >= needExp(s.level)) {
      s.exp -= needExp(s.level); s.level++;
      P.atk += s.grow.atk; P.def += s.grow.def; P.maxHp += s.grow.maxHp; s.hp = P.maxHp;
      if (s.log.length < 60) s.log.push('Lv.' + s.level + ' 달성');
    }
    nextMob(s);
  }
}
const result = s => ({ sec: s.sec, kills: s.kills, exp: s.gotExp, gold: s.gold, junk: s.junk, potUsed: s.potUsed, deaths: s.deaths, level: s.level, startLevel: s.startLevel, ended: !!s.ended, log: s.log.slice(-8), zone: s.zone });

// ---- a tiny MQTT 3.1.1 publisher over a TCP socket (QoS 0 only) ----
const te = new TextEncoder();
function varLen(n) { const o = []; do { let b = n % 128; n = Math.floor(n / 128); if (n > 0) b |= 128; o.push(b); } while (n > 0); return o; }
function mqStr(s) { const b = te.encode(s); return [b.length >> 8, b.length & 255, ...b]; }
function packet(type, body) { return new Uint8Array([type, ...varLen(body.length), ...body]); }
const mqConnect = (id, user, pass) => packet(0x10, [...mqStr('MQTT'), 4, 0x02 | (user ? 0x80 : 0) | (pass ? 0x40 : 0), 0, 60, ...mqStr(id), ...(user ? mqStr(user) : []), ...(pass ? mqStr(pass) : [])]);
const mqPublish = (topic, payload) => packet(0x30, [...mqStr(topic), ...te.encode(payload)]);
const MQ_PING = new Uint8Array([0xc0, 0]);

export class Hunt extends DurableObject {
  constructor(ctx, env) {
    super(ctx, env);
    this.env = env; this.S = null; this.mq = null; this.mqAt = 0; this.tick = 0; this.saveAt = 0;
  }
  async load() { if (!this.S) { this.S = (await this.ctx.storage.get('sessions')) || {}; } return this.S; }
  async persist() { this.saveAt = Date.now(); await this.ctx.storage.put('sessions', this.S); }

  // ---- presence ----
  async mqOpen() {
    if (this.mq || this.env.MQTT_HOST === 'off' || Date.now() - this.mqAt < 10000) return;
    this.mqAt = Date.now();
    try {
      const sock = connect({ hostname: this.env.MQTT_HOST || 'broker.emqx.io', port: +(this.env.MQTT_PORT || 8883) }, { secureTransport: this.env.MQTT_TLS === '0' ? 'off' : 'on' });
      const w = sock.writable.getWriter();
      await w.write(mqConnect('rotdidle_' + hex(crypto.getRandomValues(new Uint8Array(5))), this.env.IDLE_KEY ? 'idle' : '', this.env.IDLE_KEY || ''));
      const r = sock.readable.getReader();
      (async () => { try { while (!(await r.read()).done); } catch (e) {} if (this.mq && this.mq.sock === sock) this.mq = null; })();   // drain CONNACK / PINGRESP
      sock.closed.catch(() => {}).finally(() => { if (this.mq && this.mq.sock === sock) this.mq = null; });
      this.mq = { sock, w, pingAt: Date.now() };
    } catch (e) { this.mq = null; }
  }
  mqClose() { if (this.mq) { try { this.mq.sock.close(); } catch (e) {} this.mq = null; } }
  pub(topic, m) { if (!this.mq) return; this.mq.w.write(mqPublish(topic, JSON.stringify(m))).catch(() => { this.mq = null; }); }
  base(s) { return 'rotd1/' + String(s.srv + 1).padStart(2, '0'); }
  presence(s, full) {
    if (s.ended || !s.zone) return;
    const sp = s.spot || { x: 0, z: 0 };
    if (s.phase !== 'dead' && (s.phase === 'walk' || !s.pos)) s.pos = { x: sp.x + rand(-5, 5), z: sp.z + rand(-5, 5) };   // wander around the spot, stand while swinging
    const p = s.pos || sp, topic = this.base(s) + '/z/' + s.zone + '/' + (Math.floor(p.x / 24) + 50) + '_' + (Math.floor(p.z / 24) + 50);
    const m = { t: full ? 'st' : 's', id: s.pid, z: s.zone, x: +p.x.toFixed(1), y: 0, zz: +p.z.toFixed(1), f: +rand(0, 6.28).toFixed(2), m: s.phase === 'walk' ? 1 : 0, hp: Math.round(s.hp), dead: s.phase === 'dead' ? 1 : 0 };
    if (full) Object.assign(m, { n: (s.name + '·AI').slice(0, 12), c: s.cls, a: s.awk, lv: s.level, mh: Math.round(s.stats.maxHp), rk: 0, g: '', pk: 0, ti: s.ti });
    this.pub(topic, m);
    if (s.phase === 'fight') this.pub(topic, { t: 'fx', id: s.pid, k: 'atk' });
  }

  // ---- every 2 s while anyone is hunting ----
  async alarm() {
    const S = await this.load(), names = Object.keys(S);
    if (!names.length) { this.mqClose(); return; }
    await this.mqOpen();
    const now = Date.now();
    if (this.mq && now - this.mq.pingAt > 30000) { this.mq.pingAt = now; this.mq.w.write(MQ_PING).catch(() => { this.mq = null; }); }
    this.tick++;
    for (const k of names) {
      const s = S[k], due = Math.min(3600, Math.floor((now - s.last) / 1000));   // catch up after any pause (bounded per wake)
      for (let i = 0; i < due; i++) step(s);
      s.last += due * 1000;
      this.presence(s, this.tick % 3 === 0);
      if (this.tick % 5 === 0 && !s.ended) this.pub(this.base(s) + '/hb', { t: 'hb', id: s.pid, n: (s.name + '·AI').slice(0, 12), c: s.cls, a: s.awk, lv: s.level, z: s.zone });
    }
    if (now - this.saveAt > 30000) await this.persist();
    await this.ctx.storage.setAlarm(Date.now() + TICK_MS);
  }

  // ---- API (called by the Worker) ----
  async fetch(req) {
    const path = new URL(req.url).pathname, b = await req.json().catch(() => ({})), S = await this.load();
    const res = (code, o) => new Response(JSON.stringify(o), { status: code, headers: { 'Content-Type': 'application/json' } });
    if (path === '/idle/start') {
      const c = cleanStart(b);
      if (!c) return res(400, { error: 'bad character' });
      if (!tokOk(b.token)) return res(403, { error: 'token' });
      const old = S[c.name], tok = await hashTok(b.token);
      if (old && old.tok !== tok) return res(403, { error: 'busy' });   // someone else's hunt under this name
      if (!old && Object.keys(S).length >= MAX_SESSIONS) return res(503, { error: 'full' });
      if (old) this.pub(this.base(old) + '/bye', { t: 'bye', id: old.pid });
      const s = S[c.name] = Object.assign(c, { pid: hex(crypto.getRandomValues(new Uint8Array(5))), tok, started: Date.now(), last: Date.now(), sec: 0, startLevel: c.level,
        hp: c.stats.maxHp, kills: 0, gotExp: 0, gold: 0, junk: {}, potUsed: 0, deaths: 0, log: [], phase: 'walk', wait: 2 });
      nextMob(s); await this.persist();
      if (!(await this.ctx.storage.getAlarm())) await this.ctx.storage.setAlarm(Date.now() + TICK_MS);
      return res(200, { ok: true });
    }
    if (path === '/idle/peek' || path === '/idle/stop') {
      const name = str(b.name, 12).trim(), s = Object.hasOwn(S, name) ? S[name] : null;
      if (!s) return res(200, { none: true });
      if (s.tok !== (await hashTok(String(b.token || '')))) return res(403, { error: 'token' });
      const due = Math.min(MAX_SEC, Math.floor((Date.now() - s.last) / 1000));   // bring it up to date before answering
      for (let i = 0; i < due; i++) step(s);
      s.last += due * 1000;
      if (path === '/idle/peek') return res(200, result(s));
      this.pub(this.base(s) + '/bye', { t: 'bye', id: s.pid });
      delete S[name]; await this.persist();
      return res(200, result(s));
    }
    if (path === '/idle/health') return res(200, { ok: true, hunting: Object.keys(S).length, broker: !!this.mq });
    return res(404, { error: 'not found' });
  }
}
