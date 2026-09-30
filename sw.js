// 용의 귀환 service worker
// - 게임 페이지(html)와 manifest(json)는 항상 네트워크 먼저 → 업데이트가 바로 반영되고, 끊기면 저장본으로 실행
// - 3D 모델 · 그림 · 음악은 저장본을 먼저 쓰고 뒤에서 새로 받아 둠 → 두 번째 접속부터 빠르게 로딩
const CACHE = 'rotd-v1';
self.addEventListener('install', e => { self.skipWaiting(); });
self.addEventListener('activate', e => {
  e.waitUntil(caches.keys().then(ks => Promise.all(ks.filter(k => k !== CACHE).map(k => caches.delete(k)))).then(() => self.clients.claim()));
});
function fresh(req) {  // network first
  return fetch(req).then(r => { if (r.ok) { const c = r.clone(); caches.open(CACHE).then(ca => ca.put(req, c)); } return r; })
    .catch(() => caches.match(req, { ignoreSearch: true }));
}
function swr(req, e) {  // stale while revalidate
  return caches.open(CACHE).then(ca => ca.match(req).then(hit => {
    const net = fetch(req).then(r => { if (r.ok) ca.put(req, r.clone()); return r; });
    if (hit) { e.waitUntil(net.catch(() => {})); return hit; }
    return net;
  }));
}
self.addEventListener('fetch', e => {
  const req = e.request;
  if (req.method !== 'GET') return;
  const u = new URL(req.url);
  if (u.searchParams.has('chk')) return;  // 새 버전 확인은 그대로 통과
  if (u.origin === location.origin) {
    if (req.mode === 'navigate' || /\.(html|json|webmanifest)$/.test(u.pathname) || u.pathname.endsWith('/')) return e.respondWith(fresh(req));
    if (/\/(models|img|music)\//.test(u.pathname) || /\.(png|svg|mp3)$/.test(u.pathname)) return e.respondWith(swr(req, e));
  }
});
