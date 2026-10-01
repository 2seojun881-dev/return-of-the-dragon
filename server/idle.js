// 용의 귀환 · 자리 비움 사냥 서버
// 게임을 끄면 캐릭터가 이 서버에서 계속 사냥합니다. 다시 접속하면 그동안 얻은 경험치·골드·잡템을 받습니다.
// 사냥 중인 캐릭터는 게임 속 다른 플레이어에게도 보입니다 (게임과 같은 MQTT 브로커에 위치·공격을 보냄).
//
// 실행: npm install && PORT=8090 node idle.js
// 환경 변수
//   PORT          HTTP 포트 (기본 8090)
//   BROKER        게임이 쓰는 MQTT 브로커 (기본 wss://broker.emqx.io:8084/mqtt, 끄려면 off)
//   DATA          진행 상황 저장 파일 (기본 ./data/idle.json, 서버를 다시 켜도 이어서 사냥)
//   MAX_HOURS     한 번에 사냥하는 최대 시간 (기본 24)
//   MAX_SESSIONS  동시에 사냥하는 최대 캐릭터 수 (기본 500)
//   IDLE_KEY      직접 띄운 broker.js 를 쓸 때 두 서버에 같은 값(16자 이상)을 넣습니다
const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const PORT = +process.env.PORT || 8090;
const BROKER = process.env.BROKER || 'wss://broker.emqx.io:8084/mqtt';
const DATA = process.env.DATA || path.join(__dirname, 'data', 'idle.json');
const MAX_SEC = (+process.env.MAX_HOURS || 24) * 3600;
const MAX_SESSIONS = +process.env.MAX_SESSIONS || 500;
const CLASSES = ['novice', 'warrior', 'rogue', 'shaman'];

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

// ---- sessions: one per character name. The game keeps a random secret; a running hunt can only be
// replaced or collected with the same secret (a name is not locked forever, so a new phone still works) ----
let DB = { sessions: {} };
try { DB = JSON.parse(fs.readFileSync(DATA, 'utf8')); DB.sessions = DB.sessions || {}; delete DB.tokens; } catch (e) {}
DB.sessions = Object.assign(Object.create(null), DB.sessions);   // names are keys: no prototype ("__proto__" etc. stay plain keys)
const hashTok = t => crypto.createHash('sha256').update('rotd-idle:' + t).digest('hex');
const tokOk = t => typeof t === 'string' && t.length >= 16 && t.length <= 64;
function persist() {
  try { fs.mkdirSync(path.dirname(DATA), { recursive: true }); fs.writeFileSync(DATA + '.tmp', JSON.stringify(DB)); fs.renameSync(DATA + '.tmp', DATA); } catch (e) { console.error('save failed', e.message); }
}
setInterval(persist, 30000);
process.on('SIGTERM', () => { persist(); process.exit(0); });
process.on('SIGINT', () => { persist(); process.exit(0); });

// what the game sends when it is closed with AI on (all values are clamped)
function cleanStart(b) {
  const name = str(b.name, 12).trim();
  if (!name || !CLASSES.includes(b.cls)) return null;
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

// ---- the fight, one step per second for every character ----
const SKILL_MUL = 1.6;   // AI also uses class skills; a basic-attack-only estimate undershoots
function pickMob(s) {
  let best = null, bs = -1e9;
  for (const m of s.mobs) { const sc = m.lo <= s.level + 1 ? m.lo : -m.lo; if (sc > bs) { bs = sc; best = m; } }
  return best;
}
function nextMob(s) {
  const m = pickMob(s); s.spot = { x: m.x, z: m.z };
  s.mob = Object.assign(mobStats(Math.floor(rand(m.lo, m.hi + 1)), m.role, m.bm), { type: m.type, junk: m.junk });
  s.mob.maxHp = s.mob.hp; s.phase = 'walk'; s.wait = rand(2, 4);
}
function step(s) {
  if (s.ended) return;
  s.sec++;
  if (s.sec >= MAX_SEC) { s.ended = true; s.log.push('최대 사냥 시간이 지나 마을로 돌아왔습니다'); return; }
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
      s.log.push('Lv.' + s.level + ' 달성');
    }
    nextMob(s);
  }
}

// ---- other players see the character hunting (same messages the game sends) ----
let mq = null;
if (BROKER !== 'off') {
  try {
    const mqtt = require('mqtt');
    mq = mqtt.connect(BROKER, Object.assign({ clientId: 'rotdidle_' + crypto.randomBytes(5).toString('hex'), clean: true, keepalive: 30, reconnectPeriod: 5000 },
      process.env.IDLE_KEY ? { username: 'idle', password: process.env.IDLE_KEY } : {}));   // IDLE_KEY: only needed with our own broker.js
    mq.on('connect', () => console.log('presence broker connected', BROKER));
    mq.on('error', e => console.error('broker', e.message));
  } catch (e) { console.error('mqtt module missing, presence off'); mq = null; }
}
const base = s => 'rotd1/' + String(s.srv + 1).padStart(2, '0');
function pub(topic, m) { if (mq && mq.connected) try { mq.publish(topic, JSON.stringify(m), { qos: 0 }); } catch (e) {} }
function presence(s, full) {
  if (s.ended || !s.zone) return;
  const sp = s.spot || { x: 0, z: 0 };
  if (s.phase !== 'dead') {   // wander around the hunting spot; stand still while swinging
    if (s.phase === 'walk' || !s.pos) s.pos = { x: sp.x + rand(-5, 5), z: sp.z + rand(-5, 5) };
  }
  const p = s.pos || sp, cell = (Math.floor(p.x / 24) + 50) + '_' + (Math.floor(p.z / 24) + 50), topic = base(s) + '/z/' + s.zone + '/' + cell;
  const m = { t: full ? 'st' : 's', id: s.pid, z: s.zone, x: +p.x.toFixed(1), y: 0, zz: +p.z.toFixed(1), f: +rand(0, 6.28).toFixed(2), m: s.phase === 'walk' ? 1 : 0, hp: Math.round(s.hp), dead: s.phase === 'dead' ? 1 : 0 };
  if (full) Object.assign(m, { n: (s.name + '·AI').slice(0, 12), c: s.cls, a: s.awk, lv: s.level, mh: Math.round(s.stats.maxHp), rk: 0, g: '', pk: 0, ti: s.ti });
  pub(topic, m);
  if (s.phase === 'fight') pub(topic, { t: 'fx', id: s.pid, k: 'atk' });
}
function bye(s) { pub(base(s) + '/bye', { t: 'bye', id: s.pid }); }

let tick = 0;
setInterval(() => {
  tick++;
  for (const k in DB.sessions) {
    const s = DB.sessions[k];
    step(s);
    if (tick % 2 === 0) presence(s, tick % 6 === 0);
    if (tick % 10 === 0 && !s.ended) pub(base(s) + '/hb', { t: 'hb', id: s.pid, n: (s.name + '·AI').slice(0, 12), c: s.cls, a: s.awk, lv: s.level, z: s.zone });
  }
}, 1000);

function result(s) {
  return { sec: s.sec, kills: s.kills, exp: s.gotExp, gold: s.gold, junk: s.junk, potUsed: s.potUsed, deaths: s.deaths, level: s.level, startLevel: s.startLevel, ended: !!s.ended, log: s.log.slice(-8), zone: s.zone };
}

// ---- HTTP API ----
const CORS = { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Methods': 'GET,POST,OPTIONS', 'Access-Control-Allow-Headers': 'Content-Type' };
const hits = new Map();   // per-IP request budget: 30 per minute
function rateOk(ip) { const now = Date.now(), b = hits.get(ip) || { t: now, n: 0 }; if (now - b.t > 60000) { b.t = now; b.n = 0; } b.n++; hits.set(ip, b); return b.n <= 30; }
setInterval(() => hits.clear(), 600000);
function send(res, code, obj) { res.writeHead(code, Object.assign({ 'Content-Type': 'application/json; charset=utf-8' }, CORS)); res.end(JSON.stringify(obj)); }
function body(req) {
  return new Promise((ok, no) => { let d = ''; req.on('data', c => { d += c; if (d.length > 16384) { no(new Error('too large')); req.destroy(); } }); req.on('end', () => { try { ok(JSON.parse(d || '{}')); } catch (e) { no(e); } }); });
}

http.createServer(async (req, res) => {
  if (req.method === 'OPTIONS') { res.writeHead(204, CORS); return res.end(); }
  const url = new URL(req.url, 'http://x'), ip = (req.headers['x-forwarded-for'] || req.socket.remoteAddress || '').split(',')[0].trim();
  if (url.pathname === '/health') return send(res, 200, { ok: true, hunting: Object.keys(DB.sessions).length });
  if (!rateOk(ip)) return send(res, 429, { error: 'rate' });
  try {
    if (req.method === 'POST' && url.pathname === '/idle/start') {   // sent with navigator.sendBeacon when the game closes
      const b = await body(req), c = cleanStart(b);
      if (!c) return send(res, 400, { error: 'bad character' });
      if (!tokOk(b.token)) return send(res, 403, { error: 'token' });
      const old = DB.sessions[c.name];
      if (old && old.tok !== hashTok(b.token)) return send(res, 403, { error: 'busy' });   // someone else's hunt under this name
      if (!DB.sessions[c.name] && Object.keys(DB.sessions).length >= MAX_SESSIONS) return send(res, 503, { error: 'full' });
      if (DB.sessions[c.name]) bye(DB.sessions[c.name]);
      const s = DB.sessions[c.name] = Object.assign(c, {
        pid: crypto.randomBytes(6).toString('hex').slice(0, 10), tok: hashTok(b.token), started: Date.now(), sec: 0, startLevel: c.level, hp: c.stats.maxHp,
        kills: 0, gotExp: 0, gold: 0, junk: {}, potUsed: 0, deaths: 0, log: [], phase: 'walk', wait: 2,
      });
      nextMob(s); persist();
      console.log('hunt start', s.name, 'Lv.' + s.level, s.zone);
      return send(res, 200, { ok: true });
    }
    if (url.pathname === '/idle/peek' || url.pathname === '/idle/stop') {
      const b = req.method === 'POST' ? await body(req) : { name: url.searchParams.get('name'), token: url.searchParams.get('token') };
      const name = str(b.name, 12).trim(), s = DB.sessions[name];
      if (!s) return send(res, 200, { none: true });
      if (s.tok !== hashTok(String(b.token || ''))) return send(res, 403, { error: 'token' });
      if (url.pathname === '/idle/peek') return send(res, 200, result(s));
      bye(s); delete DB.sessions[name]; persist();
      console.log('hunt stop', name, s.kills, 'kills', s.sec, 's');
      return send(res, 200, result(s));
    }
    send(res, 404, { error: 'not found' });
  } catch (e) { send(res, 400, { error: 'bad request' }); }
}).listen(PORT, () => console.log('idle hunting server on :' + PORT + ' · ' + Object.keys(DB.sessions).length + ' characters hunting'));
