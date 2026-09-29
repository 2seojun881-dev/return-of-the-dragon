// 용의 귀환 · 게임 서버 (MQTT over WebSocket)
// 서버(채널)마다 최대 200명. 게임은 ?broker=wss://<이 서버 주소> 로 접속하면 이 서버를 씁니다.
// 실행: npm install && PORT=8080 node broker.js
const aedes = require('aedes')();
const { createServer } = require('aedes-server-factory');

const PORT = +process.env.PORT || 8080;
const HOST = process.env.HOST || '0.0.0.0';
const MAX_PER_SERVER = +process.env.MAX_PER_SERVER || 200;
const MAX_PAYLOAD = 2048;                       // 상태/채팅 메시지는 몇백 바이트면 충분
const perServer = new Map();                    // 서버 번호 → 접속자 수

// 게임 클라이언트 ID 형식: rotd_<서버번호>_<무작위>
const srvOf = id => { const m = /^rotd_(\d{1,2})_[a-z0-9]{4,16}$/.exec(id || ''); return m ? m[1] : null; };

aedes.authenticate = (client, username, password, done) => {
  const s = srvOf(client.id);
  if (s === null) return done(Object.assign(new Error('bad client id'), { returnCode: 2 }), false);
  if ((perServer.get(s) || 0) >= MAX_PER_SERVER) return done(Object.assign(new Error('server full'), { returnCode: 3 }), false);
  perServer.set(s, (perServer.get(s) || 0) + 1); client.counted = true;   // reserve the slot now so simultaneous logins can't overshoot the cap
  done(null, true);
};
aedes.on('clientDisconnect', c => { const s = srvOf(c.id); if (s !== null && c.counted) { c.counted = false; perServer.set(s, Math.max(0, (perServer.get(s) || 1) - 1)); } });

// 클라이언트마다 초당 메시지 수 제한 (토큰 버킷: 초당 25개, 순간 최대 50개)
const RATE = 25, BURST = 50;
function rateOk(client) {
  const now = Date.now(), b = client.bucket || (client.bucket = { t: now, n: BURST });
  b.n = Math.min(BURST, b.n + (now - b.t) / 1000 * RATE); b.t = now;
  if (b.n < 1) return false; b.n -= 1; return true;
}

// 게임 토픽만, 작은 메시지만 허용 (다른 용도 악용 방지)
// 실시간 토픽(위치·채팅·귓속말·길드)은 보관하지 않고, 거래소 등록(mk)·판매 대금(pay)·공지(notice)만 보관(retain)합니다
const LIVE = /^rotd1\/\d{1,2}\/(hb|chat|bye|z\/[a-z]+(\/\d{1,3}_\d{1,3})?|w\/[a-z0-9]{4,16}|g\/[a-z0-9]{1,24})$/;
const KEPT = /^rotd1\/(\d{1,2}\/(mk\/[a-z0-9]{6,20}|pay\/[a-z0-9]{1,24}\/[a-z0-9]{6,20})|notice)$/;
aedes.authorizePublish = (client, packet, done) => {
  if (client && !rateOk(client)) return done(new Error('rate'));
  const kept = KEPT.test(packet.topic);
  if (!kept && !LIVE.test(packet.topic)) return done(new Error('topic'));
  if (packet.payload && packet.payload.length > (kept ? 8192 : MAX_PAYLOAD)) return done(new Error('too large'));
  if (!kept) packet.retain = false;
  done(null);
};
// 구독 허용 목록: 전체 채널 · 위치 칸 · 거래소 · 내 판매 대금 · 내 귓속말 · 길드. '#' 같은 전체 도청 구독은 막습니다.
const SUB = /^rotd1\/(notice|(\d{1,2}|\+)\/(hb|chat|bye|mk\/\+|pay\/[a-z0-9]{1,24}\/\+|g\/[a-z0-9]{1,24}|z\/[a-z]+\/\d{1,3}_\d{1,3}|w\/([a-z0-9]{4,16})))$/;
aedes.authorizeSubscribe = (client, sub, done) => {
  const m = SUB.exec(sub.topic), own = (/^rotd_\d{1,2}_([a-z0-9]{4,16})$/.exec(client.id) || [])[1];
  done(null, m && (!m[4] || m[4] === own) ? sub : null);   // whispers: only your own inbox
};

createServer(aedes, { ws: true }).listen(PORT, HOST, () =>
  console.log(`game server on ws://${HOST}:${PORT} · max ${MAX_PER_SERVER} per server`));
