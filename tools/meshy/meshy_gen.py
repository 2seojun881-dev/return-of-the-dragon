#!/usr/bin/env python3
"""
Meshy AI -> '용의 귀환' asset pipeline.

Generates a 3D model with the Meshy API (text-to-3D preview -> refine, or image-to-3D),
optionally auto-rigs humanoids (walk/run clips), shrinks textures for mobile, and
registers the result in dragon-raid/models/custom/manifest.json. The game loads that
manifest at startup and swaps the model in (props, monsters, bosses, NPCs).

API key: read from the MESHY_API_KEY environment variable (never pass it on the command line).

  python3 meshy_gen.py balance
  python3 meshy_gen.py presets                      # list ready-made game asset prompts
  python3 meshy_gen.py preset tavern                # generate a preset
  python3 meshy_gen.py gen beastWolf --kind monster --mob beastWolf --height 1.6 \
      --prompt "dark brown dire wolf, glowing red eyes"
  python3 meshy_gen.py gen hero_npc --kind npc --npc seojun --rig --height 2.3 --prompt "..."
  python3 meshy_gen.py import tavern ~/Downloads/tavern.glb --kind prop --replace building_tavern_red
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

def _key():
    k = os.environ.get('MESHY_API_KEY', '').strip()
    if not k:
        sys.exit('MESHY_API_KEY 환경 변수가 없습니다. 환경 설정에 Meshy API 키를 MESHY_API_KEY 로 추가하세요.')
    return k

def api(method, path, body=None):
    req = urllib.request.Request(API + path, method=method, data=json.dumps(body).encode() if body is not None else None,
                                 headers={'Authorization': 'Bearer ' + _key(), 'Content-Type': 'application/json'})
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
def text_to_3d(prompt, style, polycount, texture_prompt, refine=True, model='latest'):
    body = {'mode': 'preview', 'prompt': prompt[:600], 'art_style': style, 'ai_model': model,
            'topology': 'triangle', 'target_polycount': polycount, 'should_remesh': True, 'symmetry_mode': 'auto'}
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

def rig(task_id, height_m):
    rid = api('POST', '/openapi/v1/rigging', {'input_task_id': task_id, 'height_meters': height_m})['result']
    print('리깅 작업', rid)
    t = wait('/openapi/v1/rigging/' + rid, '리깅')
    return t.get('result') or t

# ---------------------------------------------------------------- local pipeline
def optimize(src, dst, tex):
    """Resize textures and clean up for mobile. Uses @gltf-transform/cli via npx when available."""
    cmd = ['npx', '-y', '@gltf-transform/cli@4', 'optimize', src, dst, '--compress', 'false', '--texture-compress', 'false',
           '--texture-size', str(tex), '--simplify', 'false', '--join', 'false', '--flatten', 'false', '--instance', 'false']
    try:
        subprocess.run(cmd, check=True, capture_output=True, timeout=600)
        return dst
    except Exception as e:
        print('  (최적화 건너뜀: %s) 원본 그대로 사용' % str(e)[:120])
        shutil.copy(src, dst)
        return dst

def store(key, glb_path, tex):
    os.makedirs(OUT, exist_ok=True)
    tmp = os.path.join(tempfile.mkdtemp(), key + '.glb')
    optimize(glb_path, tmp, tex)
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
    for k in ('replace', 'mob', 'npc'):
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
        t, tid = text_to_3d(a.prompt + (STYLE if not a.raw else ''), a.style, a.polycount, a.texture_prompt, refine=not a.no_refine)
    files = {}
    if a.rig:
        r = rig(tid, a.rig_height or max(1.0, min(3.0, a.height * 0.75)))
        glb = download(r['rigged_character_glb_url'], os.path.join(work, 'rigged.glb'))
        files['model'] = store(a.key, glb, a.texture_size)
        anims = {}
        for name, k in (('walk', 'walking_glb_url'), ('run', 'running_glb_url')):
            url = (r.get('basic_animations') or {}).get(k)
            if url:
                p = download(url, os.path.join(work, name + '.glb'))
                anims[name] = store(a.key + '.' + name, p, 64)
        files['anims'] = anims
    else:
        url = (t.get('model_urls') or {}).get('glb')
        if not url:
            sys.exit('GLB 다운로드 주소가 없습니다: ' + json.dumps(t)[:300])
        files['model'] = store(a.key, download(url, os.path.join(work, 'model.glb')), a.texture_size)
    register(a.key, a, files, {'meshy_task': tid})
    print('완료. dragon-raid/index.html 을 새로고침하면 게임에 적용됩니다.')

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
        x.add_argument('--kind', choices=['prop', 'monster', 'boss', 'npc'], required=True)
        x.add_argument('--replace', help='prop: 교체할 KayKit 소품 이름')
        x.add_argument('--mob', help='monster/boss: 몬스터 id (MOBS 키)')
        x.add_argument('--npc', help='npc: NPC id (NPCS 키)')
        x.add_argument('--height', type=float, default=2.2, help='게임 안 높이 (캐릭터 = 2.25)')
        x.add_argument('--texture-size', type=int, default=512)
    g = sp.add_parser('gen'); common(g)
    g.add_argument('--prompt'); g.add_argument('--image', help='이미지 URL로 image-to-3D')
    g.add_argument('--texture-prompt'); g.add_argument('--style', default='realistic', choices=['realistic', 'sculpture'])
    g.add_argument('--polycount', type=int, default=12000); g.add_argument('--rig', action='store_true', help='사람형 자동 리깅 (걷기·달리기)')
    g.add_argument('--rig-height', type=float); g.add_argument('--no-refine', action='store_true'); g.add_argument('--raw', action='store_true', help='게임 스타일 문구를 붙이지 않음')
    i = sp.add_parser('import'); common(i); i.add_argument('glb'); i.add_argument('--anim', action='append', help='name=path.glb (예: run=run.glb)')
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
        for f in [e and e.get('file')] + list(((e or {}).get('anims') or {}).values()):
            if f and os.path.exists(os.path.join(OUT, f)):
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
    elif a.cmd == 'import':
        a.prompt = None
        cmd_import(a)

if __name__ == '__main__':
    main()
