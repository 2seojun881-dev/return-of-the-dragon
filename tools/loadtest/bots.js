// 용의 귀환 · 헤드리스 봇 부하 테스트 (그래픽 없이 게임과 같은 MQTT 패킷만 주고받음)
// 사용: node bots.js <봇 수> <초당 패킷 K> <초> [broker url]
//   예) node bots.js 100 10 30 ws://127.0.0.1:8080
// 각 봇: 게임처럼 rotd_0_<id> 로 접속 → 천명 성채 구역 토픽 구독 → 광장 근처를 돌며 초당 K번 상태/스킬 패킷 전송.
// 받은 패킷마다 보낸 시각을 비교해 지연(ms)과 수신율을 잽니다. 결과는 JSON 한 줄로 출력.
const mqtt = require(process.env.MQTT_PATH || 'mqtt');
const N = +process.argv[2] || 50, K = +process.argv[3] || 10, DUR = (+process.argv[4] || 30) * 1000;
const URL = process.argv[5] || 'ws://127.0.0.1:8080', SRV = 0, BASE = 'rotd1/' + SRV, ZONE = BASE + '/z/citadel';
// OPT=1 → same rules as the optimized client: 24 m AOI cells (3x3 subscribe), send rate by crowd size, slim packets
const OPT = +process.env.OPT || 0, SPREAD = +process.env.SPREAD || 20, TOTAL = +process.env.TOTAL || N, C = 24;
const cellOf = (x, z) => [Math.floor(x / C) + 50, Math.floor(z / C) + 50], topicOf = (cx, cz) => ZONE + '/' + cx + '_' + cz;
const nearN = () => Math.round(Math.min(TOTAL, TOTAL * Math.min(1, (60 * 60) / (SPREAD * SPREAD))));   // players within ~30 m
const rateOf = n => n < 20 ? 5 : n < 40 ? 3 : n < 70 ? 2 : 1;
const rid = () => Math.random().toString(36).slice(2, 12);
let recv = 0, bytes = 0, lat = [], sent = 0, connected = 0, warm = true;
const bots = [];
for (let i = 0; i < N; i++) {
  const id = rid(), c = mqtt.connect(URL, { clientId: 'rotd_' + SRV + '_' + id, reconnectPeriod: 0, connectTimeout: 20000, keepalive: 30 });
  const b = { id, c, x: (Math.random() - .5) * SPREAD, z: (Math.random() - .5) * SPREAD, f: 0, n: 0 };
  c.on('connect', () => { connected++; const t = [ 'rotd1/+/hb', BASE + '/chat'];
    if (OPT) { const [cx, cz] = cellOf(b.x, b.z); for (let i = -1; i <= 1; i++) for (let j = -1; j <= 1; j++) t.push(topicOf(cx + i, cz + j)); } else t.push(ZONE);
    c.subscribe(t, { qos: 0 }); });
  c.on('message', (t, buf) => { if (warm) return; bytes += buf.length + t.length + 5; if (!t.startsWith(ZONE)) return; recv++; if (recv % 10) return; try { const m = JSON.parse(buf); if (m.ts) lat.push(Date.now() - m.ts); } catch (e) {} });
  c.on('error', () => {});
  bots.push(b);
}
const tickOne = (b) => { { if (!b.c.connected) return;
  b.f += .3; b.x += Math.sin(b.f) * .15; b.z += Math.cos(b.f) * .15;
  if (OPT) { b.n++; const slim = b.n % 6 !== 1;
    if (slim) { b.c.publish(topicOf(...cellOf(b.x, b.z)), JSON.stringify({ t: 's', id: b.id, z: 'citadel', x: +b.x.toFixed(1), y: 0, zz: +b.z.toFixed(1), f: +b.f.toFixed(2), m: 1, hp: 30000, dead: 0, ts: Date.now() }), { qos: 0 }); sent++; return; } }
  // same shape as the game's 'st' state packet (+ a skill flag), ~260 bytes
  const m = { t: 'st', id: b.id, n: '봇' + b.id.slice(0, 4), c: 'warrior', a: 0, lv: 99, z: 'citadel', x: +b.x.toFixed(2), y: 0, zz: +b.z.toFixed(2), f: +b.f.toFixed(2), m: 1, hp: 30000, mh: 32000, dead: 0, rk: 0, g: '', pk: 0, ti: 3, sk: (sent % 3 === 0) ? 's1' : '', ts: Date.now() };
  b.c.publish(OPT ? topicOf(...cellOf(b.x, b.z)) : ZONE, JSON.stringify(m), { qos: 0 }); sent++; } };
const ivs = [];
const hb = () => bots.forEach(b => b.c.connected && b.c.publish(BASE + '/hb', JSON.stringify({ t: 'hb', id: b.id, n: 'bot', c: 'warrior', lv: 99, z: 'citadel', ts: Date.now() })));
const t0 = Date.now();
const waitConn = setInterval(() => {
  if (connected >= N || Date.now() - t0 > 60000) { clearInterval(waitConn);
    const KK = OPT ? rateOf(nearN()) : K; process.env.KEFF = KK; bots.forEach(b => setTimeout(() => ivs.push(setInterval(() => tickOne(b), 1000 / KK)), Math.random() * 1000 / KK)); const iv = null, hv = setInterval(hb, 5000);
    setTimeout(() => { warm = false; }, 3000);              // 3 s warm-up excluded
    setTimeout(() => { ivs.forEach(clearInterval); clearInterval(hv);
      lat.sort((a, b) => a - b); const q = p => lat.length ? lat[Math.min(lat.length - 1, Math.floor(lat.length * p))] : null;
      const secs = (DUR - 3000) / 1000, KE = +process.env.KEFF || K, expect = connected * (OPT ? nearN() : TOTAL) * KE * secs;  // each bot should receive every packet from the players it listens to  // each bot should receive every bot's packets (incl. its own, like the game)
      console.log(JSON.stringify({ bots: N, total: TOTAL, connected, K: +process.env.KEFF || K, near: OPT ? nearN() : TOTAL, secs, expectedPerSec: Math.round(expect / secs), deliveredPerSec: Math.round(recv / secs), deliveryPct: +(100 * recv / Math.max(1, expect)).toFixed(1),
        p50: q(.5), p95: q(.95), p99: q(.99), max: lat.length ? lat[lat.length - 1] : null, outMbps: +((bytes * 8 / secs) / 1e6).toFixed(2) }));
      bots.forEach(b => b.c.end(true)); setTimeout(() => process.exit(0), 500); }, DUR); } }, 200);
