// 용의 귀환 · 계정 서버 (Cloudflare Worker + D1)
// 캐릭터를 브라우저가 아니라 서버에 저장해서, 어느 폰에서든 이름과 비밀번호로 같은 캐릭터에 접속합니다.
//
// 모든 요청은 POST + text/plain JSON (브라우저 사전 요청(preflight) 없이, 페이지를 닫을 때도 보낼 수 있게)
//   /health                         → { ok }
//   /acct/exists   { name }         → { exists }
//   /acct/register { name, pw }     → { tok }                 새 캐릭터 (이름 = 아이디)
//   /acct/login    { name, pw }     → { tok, save, updated }  다른 기기의 접속은 끊어짐 (한 번에 한 기기)
//   /save/get      { tok }          → { save, updated }
//   /save/put      { tok, save }    → { ok, updated }
//   /acct/logout   { tok }          → { ok }
//   /acct/delete   { tok, pw }      → { ok }                  캐릭터와 저장을 지움
//   /idle/start · /idle/stop · /idle/peek · /idle/health      자리 비움 사냥 (hunt.js)
import { Hunt } from './hunt.js';
export { Hunt };

const MAX_SAVE = 512 * 1024;
const PBKDF2_ITER = 10000;   // Workers 무료 요금제의 CPU 시간 안에 들어가는 값
const ORIGINS = [/^https:\/\/game\.faceforking\.com$/, /^https:\/\/2seojun881-dev\.github\.io$/, /^http:\/\/localhost(:\d+)?$/, /^http:\/\/127\.0\.0\.1(:\d+)?$/];

const enc = new TextEncoder();
const hex = b => [...new Uint8Array(b)].map(x => x.toString(16).padStart(2, '0')).join('');
const sha256 = async s => hex(await crypto.subtle.digest('SHA-256', enc.encode(s)));
const randHex = n => hex(crypto.getRandomValues(new Uint8Array(n)));
async function pwHash(pw, salt) {
  const key = await crypto.subtle.importKey('raw', enc.encode(pw), 'PBKDF2', false, ['deriveBits']);
  return hex(await crypto.subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-256', salt: enc.encode(salt), iterations: PBKDF2_ITER }, key, 256));
}
function same(a, b) { if (typeof a !== 'string' || typeof b !== 'string' || a.length !== b.length) return false; let d = 0; for (let i = 0; i < a.length; i++) d |= a.charCodeAt(i) ^ b.charCodeAt(i); return d === 0; }
const okName = n => typeof n === 'string' && n.trim() === n && n.length >= 1 && n.length <= 8 && !/[<>"'&\u0000-\u001f]/.test(n);
const okPw = p => typeof p === 'string' && p.length >= 4 && p.length <= 32;

function cors(req) {
  const o = req.headers.get('Origin') || '';
  return { 'Access-Control-Allow-Origin': ORIGINS.some(r => r.test(o)) ? o : 'https://game.faceforking.com', 'Access-Control-Allow-Methods': 'POST, OPTIONS', 'Access-Control-Allow-Headers': 'Content-Type', 'Vary': 'Origin' };
}
const json = (req, code, obj) => new Response(JSON.stringify(obj), { status: code, headers: Object.assign({ 'Content-Type': 'application/json; charset=utf-8' }, cors(req)) });

async function session(env, tok) {
  if (typeof tok !== 'string' || tok.length !== 64) return null;
  const r = await env.DB.prepare('SELECT name FROM sessions WHERE tok = ?').bind(await sha256(tok)).first();
  return r ? r.name : null;
}
async function newSession(env, name) {
  const tok = randHex(32), now = Date.now();
  await env.DB.batch([
    env.DB.prepare('DELETE FROM sessions WHERE name = ?').bind(name),   // one device at a time
    env.DB.prepare('INSERT INTO sessions (tok, name, created) VALUES (?, ?, ?)').bind(await sha256(tok), name, now),
  ]);
  return tok;
}
async function checkPw(env, name, pw) {   // → 'ok' | 'bad' | 'locked' | 'none'
  const a = await env.DB.prepare('SELECT salt, hash, fails, lock_until FROM accounts WHERE name = ?').bind(name).first();
  if (!a) return 'none';
  const now = Date.now();
  if (a.lock_until > now) return 'locked';
  if (same(await pwHash(pw, a.salt), a.hash)) { if (a.fails) await env.DB.prepare('UPDATE accounts SET fails = 0 WHERE name = ?').bind(name).run(); return 'ok'; }
  const f = a.fails + 1;   // 10 wrong passwords in a row → locked for 10 minutes
  await env.DB.prepare('UPDATE accounts SET fails = ?, lock_until = ? WHERE name = ?').bind(f >= 10 ? 0 : f, f >= 10 ? now + 600000 : 0, name).run();
  return 'bad';
}
const adminOk = async (env, pw) => !!env.ADMIN_HASH && same(await sha256('ignis-admin:' + pw), env.ADMIN_HASH);

export default {
  async fetch(req, env) {
    if (req.method === 'OPTIONS') return new Response(null, { status: 204, headers: cors(req) });
    const path = new URL(req.url).pathname;
    if (path === '/health') return json(req, 200, { ok: true });
    if (path.startsWith('/idle/')) {   // 자리 비움 사냥: one Durable Object runs every hunting character
      const body = req.method === 'POST' ? await req.text() : '{}';
      if (body.length > 32768) return json(req, 413, { error: 'too large' });
      const stub = env.HUNT.get(env.HUNT.idFromName('world'));
      const r = await stub.fetch(new Request('https://hunt' + path, { method: 'POST', body }));
      return new Response(r.body, { status: r.status, headers: Object.assign({ 'Content-Type': 'application/json; charset=utf-8' }, cors(req)) });
    }
    if (req.method !== 'POST') return json(req, 405, { error: 'method' });
    let b;
    try { const t = await req.text(); if (t.length > MAX_SAVE + 4096) return json(req, 413, { error: 'too large' }); b = JSON.parse(t || '{}'); } catch (e) { return json(req, 400, { error: 'bad json' }); }
    if (!b || typeof b !== 'object') return json(req, 400, { error: 'bad json' });
    try {
      switch (path) {
        case '/acct/exists': {
          if (!okName(b.name)) return json(req, 200, { exists: false });
          const r = await env.DB.prepare('SELECT 1 FROM accounts WHERE name = ?').bind(b.name).first();
          return json(req, 200, { exists: !!r || b.name === env.ADMIN_NAME });
        }
        case '/acct/register': {
          if (!okName(b.name)) return json(req, 400, { error: 'name' });
          if (!okPw(b.pw)) return json(req, 400, { error: 'pw' });
          if (b.name === env.ADMIN_NAME && !(await adminOk(env, b.pw))) return json(req, 403, { error: 'reserved' });
          const salt = randHex(16), hash = await pwHash(b.pw, salt);
          const r = await env.DB.prepare('INSERT OR IGNORE INTO accounts (name, salt, hash, created) VALUES (?, ?, ?, ?)').bind(b.name, salt, hash, Date.now()).run();
          if (!r.meta.changes) return json(req, 409, { error: 'taken' });
          return json(req, 200, { tok: await newSession(env, b.name) });
        }
        case '/acct/login': {
          if (!okName(b.name) || !okPw(b.pw)) return json(req, 400, { error: 'bad' });
          let st = await checkPw(env, b.name, b.pw);
          if (st === 'none' && b.name === env.ADMIN_NAME && (await adminOk(env, b.pw))) {   // operator account appears on first login
            const salt = randHex(16);
            await env.DB.prepare('INSERT OR IGNORE INTO accounts (name, salt, hash, created) VALUES (?, ?, ?, ?)').bind(b.name, salt, await pwHash(b.pw, salt), Date.now()).run();
            st = 'ok';
          }
          if (st === 'none') return json(req, 404, { error: 'none' });
          if (st === 'locked') return json(req, 429, { error: 'locked' });
          if (st !== 'ok') return json(req, 403, { error: 'pw' });
          const s = await env.DB.prepare('SELECT data, updated FROM saves WHERE name = ?').bind(b.name).first();
          return json(req, 200, { tok: await newSession(env, b.name), save: s ? JSON.parse(s.data) : null, updated: s ? s.updated : 0 });
        }
        case '/save/get': {
          const name = await session(env, b.tok);
          if (!name) return json(req, 401, { error: 'session' });
          const s = await env.DB.prepare('SELECT data, updated FROM saves WHERE name = ?').bind(name).first();
          return json(req, 200, { save: s ? JSON.parse(s.data) : null, updated: s ? s.updated : 0 });
        }
        case '/save/put': {
          const name = await session(env, b.tok);
          if (!name) return json(req, 401, { error: 'session' });   // logged in on another phone
          if (!b.save || typeof b.save !== 'object' || Array.isArray(b.save)) return json(req, 400, { error: 'save' });
          const data = JSON.stringify(b.save);
          if (data.length > MAX_SAVE) return json(req, 413, { error: 'too large' });
          const now = Date.now();
          await env.DB.prepare('INSERT INTO saves (name, data, updated) VALUES (?, ?, ?) ON CONFLICT(name) DO UPDATE SET data = excluded.data, updated = excluded.updated').bind(name, data, now).run();
          return json(req, 200, { ok: true, updated: now });
        }
        case '/acct/logout': {
          if (typeof b.tok === 'string' && b.tok.length === 64) await env.DB.prepare('DELETE FROM sessions WHERE tok = ?').bind(await sha256(b.tok)).run();
          return json(req, 200, { ok: true });
        }
        case '/acct/delete': {
          const name = await session(env, b.tok);
          if (!name) return json(req, 401, { error: 'session' });
          if (!okPw(b.pw) || (await checkPw(env, name, b.pw)) !== 'ok') return json(req, 403, { error: 'pw' });
          await env.DB.batch([
            env.DB.prepare('DELETE FROM saves WHERE name = ?').bind(name),
            env.DB.prepare('DELETE FROM sessions WHERE name = ?').bind(name),
            env.DB.prepare('DELETE FROM accounts WHERE name = ?').bind(name),
          ]);
          return json(req, 200, { ok: true });
        }
      }
      return json(req, 404, { error: 'not found' });
    } catch (e) {
      return json(req, 500, { error: 'server' });
    }
  },
};
