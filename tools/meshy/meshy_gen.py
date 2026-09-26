#!/usr/bin/env python3
"""
Meshy AI -> '용의 귀환' asset pipeline.

Generates a 3D model with the Meshy API (text-to-3D preview -> refine, or image-to-3D),
optionally auto-rigs humanoids (walk/run clips), shrinks textures for mobile, and
registers the result in dragon-raid/models/custom/manifest.json. The game loads that
manifest at startup and swaps the model in (props, monsters, bosses, NPCs).

API key: configured as an environment API credential for api.meshy.ai (injected by the proxy),
or read from the MESHY_API_KEY environment variable (never pass it on the command line).

  python3 meshy_gen.py balance
  python3 meshy_gen.py presets                      # list ready-made game asset prompts
  python3 meshy_gen.py preset tavern                # generate a preset
  python3 meshy_gen.py gen beastWolf --kind monster --mob beastWolf --height 1.6 \
      --prompt "dark brown dire wolf, glowing red eyes"
  python3 meshy_gen.py gen hero_npc --kind npc --npc seojun --rig --height 2.3 --prompt "..."
  python3 meshy_gen.py import tavern ~/Downloads/tavern.glb --kind prop --replace building_tavern_red
  python3 meshy_gen.py rigfile m_CrystalGolem         # auto-rig an imported model (walk/run clips)
  python3 meshy_gen.py list | remove KEY
"""
import argparse, base64, json, os, shutil, ssl, subprocess, sys, tempfile, time, urllib.request, urllib.error

HERE = os.path.dirname(os.path.abspath(__file__))
GAME = os.path.abspath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(GAME, 'models', 'custom')
MANIFEST = os.path.join(OUT, 'manifest.json')
API = os.environ.get('MESHY_API_BASE', 'https://api.meshy.ai')
STYLE = ', stylized low poly game asset, chunky hand-painted colors, dark fantasy mobile MMORPG, clean silhouette, single object, centered, no ground plane'

# ---------------------------------------------------------------- http
def _ctx():
    for ca in (os.environ.get('SSL_CERT_FILE'), os.environ.get('REQUESTS_CA_BUNDLE'), '/root/.ccr/ca-bundle.crt'):
        if ca and os.path.exists(ca):
            return ssl.create_default_context(cafile=ca)
    return ssl.create_default_context()

def _headers():
    # 키는 환경의 'API 자격 증명'(프록시가 Authorization 헤더를 대신 붙임) 또는 MESHY_API_KEY 환경 변수로 제공
    h = {'Content-Type': 'application/json'}
    k = os.environ.get('MESHY_API_KEY', '').strip()
    if k:
        h['Authorization'] = 'Bearer ' + k
    return h

def api(method, path, body=None):
    req = urllib.request.Request(API + path, method=method, data=json.dumps(body).encode() if body is not None else None,
                                 headers=_headers())
    try:
        with urllib.request.urlopen(req, context=_ctx(), timeout=60) as r:
            return json.loads(r.read().decode() or '{}')
    except urllib.error.HTTPError as e:
        sys.exit('Meshy API 오류 %s %s: %s' % (e.code, path, e.read().decode(errors='replace')[:500]))
    except urllib.error.URLError as e:
        sys.exit('Meshy API에 연결할 수 없습니다 (%s). 네트워크 설정에서 api.meshy.ai 를 허용했는지 확인하세요.' % e.reason)

def download(url, path):
    req = urllib.request.Request(url, headers={'User-Agent': 'dragon-raid-meshy'})
    with urllib.request.urlopen(req, context=_ctx(), timeout=300) as r, open(path, 'wb') as f:
        shutil.copyfileobj(r, f)
    return path

def wait(path, label):
    last = -1
    while True:
        t = api('GET', path)
        st, pr = t.get('status'), t.get('progress', 0)
        if pr != last:
            print('  %s: %s %s%%' % (label, st, pr)); last = pr
        if st == 'SUCCEEDED':
            return t
        if st in ('FAILED', 'CANCELED', 'EXPIRED'):
            sys.exit('%s 실패: %s' % (label, (t.get('task_error') or {}).get('message', st)))
        time.sleep(5)

# ---------------------------------------------------------------- meshy tasks
def text_to_3d(prompt, style, polycount, texture_prompt, refine=True, model='latest', preview_id=None):
    body = {'mode': 'preview', 'prompt': prompt[:600], 'art_style': style, 'ai_model': model,
            'topology': 'triangle', 'target_polycount': polycount, 'should_remesh': True, 'symmetry_mode': 'auto'}
    if preview_id:  # reuse an existing preview (shape) task
        pid, t = preview_id, api('GET', '/openapi/v2/text-to-3d/' + preview_id)
    else:
        pid = api('POST', '/openapi/v2/text-to-3d', body)['result']
        print('미리보기 작업', pid)
        t = wait('/openapi/v2/text-to-3d/' + pid, '미리보기(형태)')
    if not refine:
        return t, pid
    body = {'mode': 'refine', 'preview_task_id': pid, 'enable_pbr': False}
    if texture_prompt:
        body['texture_prompt'] = texture_prompt[:600]
    rid = api('POST', '/openapi/v2/text-to-3d', body)['result']
    print('텍스처 작업', rid)
    return wait('/openapi/v2/text-to-3d/' + rid, '텍스처'), rid

def image_to_3d(image_url, polycount, model='latest'):
    body = {'image_url': image_url, 'ai_model': model, 'topology': 'triangle', 'target_polycount': polycount,
            'should_remesh': True, 'should_texture': True, 'enable_pbr': False, 'symmetry_mode': 'auto'}
    tid = api('POST', '/openapi/v1/image-to-3d', body)['result']
    print('이미지→3D 작업', tid)
    return wait('/openapi/v1/image-to-3d/' + tid, '이미지→3D'), tid

# ---------------------------------------------------------------- local pipeline
GT = ['npx', '-y', '@gltf-transform/cli@4']

def _glb_split(data):
    import struct
    jl = struct.unpack_from('<I', data, 12)[0]
    j = json.loads(data[20:20 + jl])
    b = data[20 + jl + 8:] if len(data) > 20 + jl else b''
    return j, b

def _glb_join(j, b):
    import struct
    js = json.dumps(j, separators=(',', ':')).encode()
    js += b' ' * (-len(js) % 4)
    b += b'\0' * (-len(b) % 4)
    body = struct.pack('<II', len(js), 0x4E4F534A) + js + struct.pack('<II', len(b), 0x004E4942) + b
    return struct.pack('<III', 0x46546C67, 2, 12 + len(body)) + body

def triangles(path):
    j, _ = _glb_split(open(path, 'rb').read())
    n = 0
    for m in j.get('meshes', []):
        for pr in m['primitives']:
            acc = j['accessors'][pr['indices'] if 'indices' in pr else pr['attributes']['POSITION']]
            n += acc['count'] // 3
    return n

def _accessor(j, b, i):
    import struct
    a = j['accessors'][i]; v = j['bufferViews'][a['bufferView']]
    comp = {'VEC4': 4, 'VEC3': 3, 'VEC2': 2, 'SCALAR': 1}[a['type']]
    fmt = {5126: 'f', 5123: 'H', 5125: 'I', 5121: 'B'}[a['componentType']]
    st = v.get('byteStride') or struct.calcsize(fmt) * comp
    o = v.get('byteOffset', 0) + a.get('byteOffset', 0)
    return [struct.unpack_from('<' + fmt * comp, b, o + k * st) for k in range(a['count'])]

def _uv_mask(j, b, img_index, w, h):
    """Pixels covered by the UV triangles of every primitive whose material uses this image."""
    from PIL import Image, ImageDraw
    mask = Image.new('L', (w, h), 0); dr = ImageDraw.Draw(mask)
    mats = {i for i, mt in enumerate(j.get('materials', [])) for key in ('baseColorTexture',)
            if (mt.get('pbrMetallicRoughness') or {}).get(key, {}).get('index') is not None
            and j['textures'][(mt['pbrMetallicRoughness'][key])['index']].get('source') == img_index}
    for me in j.get('meshes', []):
        for pr in me['primitives']:
            if pr.get('material') not in mats or 'TEXCOORD_0' not in pr['attributes']:
                continue
            uv = _accessor(j, b, pr['attributes']['TEXCOORD_0'])
            idx = [t[0] for t in _accessor(j, b, pr['indices'])] if 'indices' in pr else list(range(len(uv)))
            for t in range(0, len(idx) - 2, 3):
                dr.polygon([(uv[idx[t + k]][0] * w, uv[idx[t + k]][1] * h) for k in range(3)], fill=255, outline=255)
    return mask

def _pushpull(im, mask):
    """Fill texels outside the UV islands with nearby island colours so mipmaps don't bleed black in."""
    import numpy as np
    from PIL import Image
    a = np.asarray(im.convert('RGB'), dtype=np.float32); m = (np.asarray(mask) > 0).astype(np.float32)
    if m.mean() > .995 or m.sum() == 0:
        return im
    levels = [(a * m[..., None], m)]
    while min(levels[-1][1].shape) > 1:
        c, w = levels[-1]; H, W = w.shape; H2, W2 = H // 2 * 2, W // 2 * 2
        c = c[:H2, :W2].reshape(H2 // 2, 2, W2 // 2, 2, 3).sum((1, 3)); w = w[:H2, :W2].reshape(H2 // 2, 2, W2 // 2, 2).sum((1, 3))
        levels.append((c, w))
    col = levels[-1][0] / np.maximum(levels[-1][1], 1e-6)[..., None]
    for c, w in reversed(levels[:-1]):
        H, W = w.shape; up = np.repeat(np.repeat(col, 2, 0), 2, 1)
        up = np.pad(up, ((0, max(0, H - up.shape[0])), (0, max(0, W - up.shape[1])), (0, 0)), mode='edge')[:H, :W]
        known = w > 0
        col = np.where(known[..., None], c / np.maximum(w, 1e-6)[..., None], up)
    out = np.where(m[..., None] > 0, a, col)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))

def shrink_textures(path, size):
    """Resize embedded images to at most size x size (needs Pillow; gltf-transform resize needs sharp)."""
    try:
        from PIL import Image
    except ImportError:
        print('  (Pillow 없음: 텍스처 축소 건너뜀 — pip install pillow)')
        return
    import io
    j, b = _glb_split(open(path, 'rb').read())
    j0 = json.loads(json.dumps(j))  # buffer views get rewritten below; UV masks read the original layout
    views, out = j['bufferViews'], bytearray()
    remap = {}
    for i, v in enumerate(views):
        chunk = b[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']]
        img = next((im for im in j.get('images', []) if im.get('bufferView') == i), None)
        if img is not None:
            im = Image.open(io.BytesIO(chunk))
            if max(im.size) > size:
                im = im.resize((min(size, im.size[0]), min(size, im.size[1])), Image.LANCZOS)
            try:  # pad UV islands (Meshy atlases have black gaps that bleed into mipmaps -> dark props at a distance)
                if im.mode not in ('RGBA', 'LA'):
                    im = _pushpull(im, _uv_mask(j0, b, j['images'].index(img), im.size[0], im.size[1]))
            except Exception as ex:
                print('  (텍스처 여백 채우기 건너뜀: %s)' % str(ex)[:80])
            buf = io.BytesIO()
            opaque = im.mode in ('RGB', 'L', 'P') or (im.mode in ('RGBA', 'LA') and im.getextrema()[-1][0] == 255)
            if img.get('mimeType') == 'image/png' and not opaque:
                im.save(buf, 'PNG', optimize=True)
            else:  # opaque PNGs (e.g. Meshy rigging output) are much smaller as JPEG
                im.convert('RGB').save(buf, 'JPEG', quality=85)
                img['mimeType'] = 'image/jpeg'
            chunk = buf.getvalue()
        out += b'\0' * (-len(out) % 4)
        remap[i] = len(out)
        out += chunk
        v['byteOffset'] = remap[i]
        v['byteLength'] = len(chunk)
    j['buffers'] = [{'byteLength': len(out)}]
    open(path, 'wb').write(_glb_join(j, bytes(out)))

def optimize(src, dst, tex, max_tris=30000):
    """Mobile budget: simplify to ~max_tris triangles, clean up, shrink textures to tex px."""
    work = src
    try:
        n = triangles(src)
        if max_tris and n > max_tris:
            tmp = tempfile.mkdtemp()
            subprocess.run(GT + ['weld', src, os.path.join(tmp, 'w.glb')], check=True, capture_output=True, timeout=600)
            for err in ('0.005', '0.02', '0.05'):  # loosen the error bound until the budget is met
                subprocess.run(GT + ['simplify', os.path.join(tmp, 'w.glb'), os.path.join(tmp, 's.glb'),
                                     '--ratio', '%.4f' % (max_tris / n), '--error', err], check=True, capture_output=True, timeout=900)
                work = os.path.join(tmp, 's.glb')
                if triangles(work) <= max_tris * 1.3:
                    break
            cur = triangles(work)
            if cur > max_tris * 1.3:  # many UV seams block gltf-transform; gltfpack handles them better
                subprocess.run(['npx', '-y', 'gltfpack', '-i', work, '-o', os.path.join(tmp, 'p.glb'), '-noq',
                                '-si', '%.3f' % max(max_tris / cur, 0.3)], check=True, capture_output=True, timeout=900)
                work = os.path.join(tmp, 'p.glb')
            print('  폴리곤 축소: %d → %d 삼각형' % (n, triangles(work)))
    except Exception as e:
        print('  (폴리곤 축소 건너뜀: %s)' % str(e)[:120])
    cmd = GT + ['optimize', work, dst, '--compress', 'false', '--texture-compress', 'false',
                '--texture-size', str(tex), '--simplify', 'false', '--join', 'false', '--flatten', 'false', '--instance', 'false']
    try:
        subprocess.run(cmd, check=True, capture_output=True, timeout=600)
    except Exception as e:
        print('  (최적화 건너뜀: %s) 원본 그대로 사용' % str(e)[:120])
        shutil.copy(work, dst)
    shrink_textures(dst, tex)
    return dst

def store(key, glb_path, tex, max_tris=30000):
    os.makedirs(OUT, exist_ok=True)
    tmp = os.path.join(tempfile.mkdtemp(), key + '.glb')
    optimize(glb_path, tmp, tex, max_tris)
    data = open(tmp, 'rb').read()
    with open(os.path.join(OUT, key + '.glb.txt'), 'w') as f:
        f.write(base64.b64encode(data).decode())
    print('  저장: models/custom/%s.glb.txt (%.1f MB)' % (key, len(data) / 1e6))
    return key + '.glb.txt'

def load_manifest():
    if os.path.exists(MANIFEST):
        return json.load(open(MANIFEST, encoding='utf-8'))
    return {'models': {}}

def save_manifest(m):
    os.makedirs(OUT, exist_ok=True)
    json.dump(m, open(MANIFEST, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)

def register(key, a, files, extra=None):
    m = load_manifest()
    e = {'file': files['model'], 'kind': a.kind, 'height': a.height}
    for k in ('replace', 'mob', 'npc', 'player'):
        if getattr(a, k, None):
            e[k] = getattr(a, k)
    if files.get('anims'):
        e['anims'] = files['anims']
    if getattr(a, 'prompt', None):
        e['prompt'] = a.prompt
    e.update(extra or {})
    m['models'][key] = e
    save_manifest(m)
    print('manifest 등록:', key, json.dumps(e, ensure_ascii=False))

def check_target(a):
    if a.kind == 'player' and not getattr(a, 'player', None):
        sys.exit('--kind player 는 --player <직업 id> 가 필요합니다 (novice, warrior, rogue, shaman).')
    if a.kind == 'prop' and not a.replace:
        sys.exit('--kind prop 은 --replace <교체할 KayKit 소품 이름> 이 필요합니다 (예: building_tavern_red).')
    if a.kind in ('monster', 'boss') and not a.mob:
        sys.exit('--kind %s 는 --mob <몬스터 id> 가 필요합니다 (예: beastWolf, ignis).' % a.kind)
    if a.kind == 'npc' and not a.npc:
        sys.exit('--kind npc 는 --npc <NPC id> 가 필요합니다 (예: seojun).')

def cmd_gen(a):
    check_target(a)
    work = tempfile.mkdtemp()
    if a.image:
        t, tid = image_to_3d(a.image, a.polycount)
    else:
        t, tid = text_to_3d(a.prompt + (STYLE if not a.raw else ''), a.style, a.polycount, a.texture_prompt, refine=not a.no_refine, model=a.model)
    url = (t.get('model_urls') or {}).get('glb')
    if not url:
        sys.exit('GLB 다운로드 주소가 없습니다: ' + json.dumps(t)[:300])
    src = download(url, os.path.join(work, 'model.glb'))
    files = {'model': store(a.key, src, a.texture_size)}
    register(a.key, a, files, {'meshy_task': tid})
    if a.rig:  # humanoids: auto-rig + walk/run + library clips (same path as `rigfile`)
        cmd_rigfile(argparse.Namespace(file=a.key, glb=src, rig_height=a.rig_height or 1.8, texture_size=a.texture_size,
                                       max_tris=a.polycount, actions=a.actions))
    print('완료. dragon-raid/index.html 을 새로고침하면 게임에 적용됩니다.')

def anim_only(src, dst):
    """Keep only skeleton nodes + animations (drop mesh/skin/textures) so clip files stay tiny."""
    j, b = _glb_split(open(src, 'rb').read())
    for n in j.get('nodes', []):
        n.pop('mesh', None); n.pop('skin', None)
    for k in ('meshes', 'skins', 'materials', 'textures', 'images', 'samplers'):
        j.pop(k, None)
    tmp = dst + '.tmp.glb'
    open(tmp, 'wb').write(_glb_join(j, b))
    subprocess.run(GT + ['prune', tmp, dst], check=True, capture_output=True, timeout=300)
    os.remove(tmp)
    return dst

def animate(rig_id, action_id):
    """One clip from Meshy's animation library (3 credits). Returns the animation GLB url."""
    aid = api('POST', '/openapi/v1/animations', {'rig_task_id': rig_id, 'action_id': action_id})['result']
    t = wait('/openapi/v1/animations/' + aid, '애니메이션 %d' % action_id)
    return (t.get('result') or {}).get('animation_glb_url')

def _save_clip(key, name, full_glb, work):
    slim = anim_only(full_glb, os.path.join(work, name + '_anim.glb'))
    subprocess.run(GT + ['resample', slim, slim], capture_output=True, timeout=300)  # drop redundant keys
    data = open(slim, 'rb').read()
    fname = key + '.' + name + '.glb.txt'
    with open(os.path.join(OUT, fname), 'w') as f:
        f.write(base64.b64encode(data).decode())
    print('  애니메이션 %s: %.0f KB' % (name, len(data) / 1024))
    return fname

def cmd_rigfile(a):
    """Rig an already-imported model file and attach clips to every manifest entry using it."""
    fname = a.file if a.file.endswith('.glb.txt') else a.file + '.glb.txt'
    key = fname[:-8]
    work = tempfile.mkdtemp()
    src = a.glb
    if not src:
        src = os.path.join(work, 'src.glb')
        open(src, 'wb').write(base64.b64decode(open(os.path.join(OUT, fname)).read()))
    uri = 'data:application/octet-stream;base64,' + base64.b64encode(open(src, 'rb').read()).decode()
    rig_id = api('POST', '/openapi/v1/rigging', {'model_url': uri, 'height_meters': a.rig_height})['result']
    print('  리깅 작업', rig_id)
    t = wait('/openapi/v1/rigging/' + rig_id, '리깅')
    r = t.get('result') or t
    rigged = download(r['rigged_character_glb_url'], os.path.join(work, 'rigged.glb'))
    store(key, rigged, a.texture_size, a.max_tris)
    anims = {}
    ba = r.get('basic_animations') or {}
    for name, field in (('walk', 'walking_glb_url'), ('run', 'running_glb_url')):
        if ba.get(field):
            anims[name] = _save_clip(key, name, download(ba[field], os.path.join(work, name + '.glb')), work)
    for spec in (a.actions or '').split(','):
        if '=' not in spec:
            continue
        name, aid = spec.split('=')
        url = animate(rig_id, int(aid))
        if url:
            anims[name] = _save_clip(key, name, download(url, os.path.join(work, name + '.glb')), work)
    import fcntl
    with open(MANIFEST + '.lock', 'w') as lk:  # several rigfile runs may finish at once
        fcntl.flock(lk, fcntl.LOCK_EX)
        m = load_manifest()
        users = [k for k, e in m['models'].items() if e.get('file') == fname]
        for k in users:
            m['models'][k]['anims'] = anims
            m['models'][k]['rigged'] = True
        save_manifest(m)
    print('리깅 완료:', fname, '→', ', '.join(users) or '(manifest 항목 없음)')

def _manifest_update(fn):
    import fcntl
    with open(MANIFEST + '.lock', 'w') as lk:
        fcntl.flock(lk, fcntl.LOCK_EX)
        m = load_manifest(); fn(m); save_manifest(m)

def cmd_world(a):
    """Batch-replace KayKit props with Meshy models (props_world.json). meshy-5 = 5 + 10 credits per model.
    Several KayKit names can share one model; each entry lists the zones it appears in so the game loads it lazily."""
    from concurrent.futures import ThreadPoolExecutor
    spec = json.load(open(os.path.join(HERE, a.spec), encoding='utf-8'))
    zones_of = json.load(open(os.path.join(HERE, 'prop_zones.json'), encoding='utf-8'))
    done = load_manifest()['models']
    todo = [k for k in spec if (not a.only or k in a.only.split(',')) and (a.redo or k not in done)]
    print('생성할 모델 %d개 (예상 %d 크레딧)' % (len(todo), len(todo) * 15))
    def one(key):
        v = spec[key]
        try:
            t, tid = text_to_3d(v['prompt'] + STYLE, 'realistic', v.get('tris', 2000), None, model=a.model, preview_id=v.get('preview'))
            url = (t.get('model_urls') or {}).get('glb')
            src = download(url, os.path.join(tempfile.mkdtemp(), key + '.glb'))
            f = store(key, src, v.get('tex', 256), max(v.get('tris', 2000), 800) * 2)
            zs = sorted({z for r in v['replace'] for z in (zones_of.get(r) or {})})
            if v.get('ground'):  # flat tile rendered top-down into a repeating ground texture by the game
                e = {'file': f, 'kind': 'groundtex', 'ground': v['ground'], 'prompt': v['prompt'], 'meshy_task': tid}
            else:
                e = {'file': f, 'kind': 'prop', 'replace': v['replace'], 'zones': zs, 'prompt': v['prompt'], 'meshy_task': tid}
            _manifest_update(lambda m: m['models'].__setitem__(key, e))
            print('OK', key, '→', ', '.join(v['replace']), flush=True)
        except BaseException as ex:  # api() exits via SystemExit on HTTP errors
            print('FAIL', key, str(ex)[:200], flush=True)
    with ThreadPoolExecutor(a.workers) as ex:
        list(ex.map(one, todo))

def cmd_zones(a):
    """Tag monster/NPC entries with the zones they appear in (entity_zones.json) so the game loads them lazily."""
    ez = json.load(open(os.path.join(HERE, 'entity_zones.json'), encoding='utf-8'))
    def fix(m):
        n = 0
        for k, e in m['models'].items():
            z = ez['mob'].get(e.get('mob')) if e.get('mob') else ez['npc'].get(e.get('npc')) if e.get('npc') else None
            if z and e.get('zones') != z:
                e['zones'] = z; n += 1
        print('zones 갱신:', n)
    _manifest_update(fix)

def cmd_import(a):
    check_target(a)
    files = {'model': store(a.key, a.glb, a.texture_size), 'anims': {}}
    for spec in a.anim or []:
        name, path = spec.split('=', 1)
        files['anims'][name] = store(a.key + '.' + name, path, 64)
    register(a.key, a, files, {'imported': True})

def cmd_preset(a):
    P = json.load(open(os.path.join(HERE, 'presets.json'), encoding='utf-8'))
    if a.name not in P:
        sys.exit('프리셋이 없습니다. 목록: ' + ', '.join(P))
    p = P[a.name]
    ns = argparse.Namespace(key=p.get('key', a.name), prompt=p['prompt'], texture_prompt=p.get('texture_prompt'), style=p.get('style', 'realistic'),
                            kind=p['kind'], replace=p.get('replace'), mob=p.get('mob'), npc=p.get('npc'), height=p.get('height', 2.2),
                            rig=p.get('rig', False), rig_height=p.get('rig_height'), polycount=p.get('polycount', 12000),
                            texture_size=p.get('texture_size', 512), image=None, no_refine=False, raw=False)
    cmd_gen(ns)

def main():
    ap = argparse.ArgumentParser(description='Meshy AI -> 용의 귀환 에셋 생성')
    sp = ap.add_subparsers(dest='cmd', required=True)
    sp.add_parser('balance')
    sp.add_parser('presets')
    sp.add_parser('list')
    r = sp.add_parser('remove'); r.add_argument('key')
    p = sp.add_parser('preset'); p.add_argument('name')
    def common(x):
        x.add_argument('key', help='에셋 키 (파일 이름)')
        x.add_argument('--kind', choices=['prop', 'monster', 'boss', 'npc', 'player'], required=True)
        x.add_argument('--player', help='player: 직업 id (novice, warrior, rogue, shaman)')
        x.add_argument('--replace', help='prop: 교체할 KayKit 소품 이름')
        x.add_argument('--mob', help='monster/boss: 몬스터 id (MOBS 키)')
        x.add_argument('--npc', help='npc: NPC id (NPCS 키)')
        x.add_argument('--height', type=float, default=2.2, help='게임 안 높이 (캐릭터 = 2.25)')
        x.add_argument('--texture-size', type=int, default=512)
    g = sp.add_parser('gen'); common(g)
    g.add_argument('--prompt'); g.add_argument('--image', help='이미지 URL로 image-to-3D')
    g.add_argument('--texture-prompt'); g.add_argument('--style', default='realistic', choices=['realistic', 'sculpture'])
    g.add_argument('--polycount', type=int, default=12000); g.add_argument('--model', default='latest', help='latest(30크레딧) 또는 meshy-5(15크레딧)'); g.add_argument('--rig', action='store_true', help='사람형 자동 리깅 (걷기·달리기)')
    g.add_argument('--rig-height', type=float); g.add_argument('--actions', default='idle=0,attack=4,hit=178,death=8'); g.add_argument('--no-refine', action='store_true'); g.add_argument('--raw', action='store_true', help='게임 스타일 문구를 붙이지 않음')
    i = sp.add_parser('import'); common(i); i.add_argument('glb'); i.add_argument('--anim', action='append', help='name=path.glb (예: run=run.glb)')
    rf = sp.add_parser('rigfile', help='이미 넣은 모델 파일에 뼈대(걷기·달리기) 입히기')
    rf.add_argument('file', help='models/custom 안의 파일 이름 (예: m_CrystalGolem)')
    rf.add_argument('--glb', help='원본 GLB (없으면 저장된 파일 사용)')
    rf.add_argument('--rig-height', type=float, default=1.8); rf.add_argument('--texture-size', type=int, default=512)
    rf.add_argument('--max-tris', type=int, default=12000)
    rf.add_argument('--actions', default='idle=0,attack=4,hit=178,death=8',
                    help='추가 동작 (Meshy 애니메이션 라이브러리 action_id, 개당 3크레딧)')
    sp.add_parser('zones', help='몬스터·NPC 모델에 등장 구역 표시 (지연 로딩)')
    wd = sp.add_parser('world', help='props_world.json 의 소품을 한꺼번에 Meshy 모델로 교체')
    wd.add_argument('--spec', default='props_world.json'); wd.add_argument('--only'); wd.add_argument('--redo', action='store_true')
    wd.add_argument('--workers', type=int, default=5); wd.add_argument('--model', default='meshy-5')
    a = ap.parse_args()
    if a.cmd == 'balance':
        print('Meshy 크레딧:', api('GET', '/openapi/v1/balance').get('balance'))
    elif a.cmd == 'presets':
        for k, v in json.load(open(os.path.join(HERE, 'presets.json'), encoding='utf-8')).items():
            print('%-16s %-8s %s' % (k, v['kind'], v.get('replace') or v.get('mob') or v.get('npc')))
    elif a.cmd == 'list':
        for k, v in load_manifest()['models'].items():
            print('%-16s %s' % (k, json.dumps(v, ensure_ascii=False)))
    elif a.cmd == 'remove':
        m = load_manifest(); e = m['models'].pop(a.key, None); save_manifest(m)
        used = set()
        for o in m['models'].values():
            used.add(o.get('file')); used.update((o.get('anims') or {}).values())
        for f in [e and e.get('file')] + list(((e or {}).get('anims') or {}).values()):
            if f and f not in used and os.path.exists(os.path.join(OUT, f)):
                os.remove(os.path.join(OUT, f))
        print('삭제:', a.key if e else '(없음)')
    elif a.cmd == 'preset':
        if not a.name:
            sys.exit('프리셋 이름이 필요합니다')
        cmd_preset(a)
    elif a.cmd == 'gen':
        if not a.prompt and not a.image:
            sys.exit('--prompt 또는 --image 가 필요합니다')
        a.texture_prompt = a.texture_prompt
        cmd_gen(a)
    elif a.cmd == 'zones':
        cmd_zones(a)
    elif a.cmd == 'world':
        cmd_world(a)
    elif a.cmd == 'rigfile':
        cmd_rigfile(a)
    elif a.cmd == 'import':
        a.prompt = None
        cmd_import(a)

if __name__ == '__main__':
    main()
