"""Make a Meshy auto-rigged chibi head move as one piece.

Meshy's rigger often binds the face only 45-90% to the Head bone and the rest to shoulders/arms, so any arm or
shoulder motion drags the chin and mouth sideways. Above the shoulders, blend those weights to 100% Head.

usage: rigid_head.py models/custom/p_rogue.glb.txt [...]
"""
import sys, os, base64
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshy_gen as m
from hero_hires import read


def fix(path):
    txt = path.endswith('.txt')
    data = base64.b64decode(open(path).read()) if txt else open(path, 'rb').read()
    j, b = m._glb_split(data); b = bytearray(b)
    sk = j['skins'][0]; names = [j['nodes'][i]['name'] for i in sk['joints']]
    ibm = read(j, bytes(b), sk['inverseBindMatrices']).reshape(-1, 4, 4)
    jy = {n: np.linalg.inv(M.T)[1, 3] for n, M in zip(names, ibm)}
    hi = names.index('Head')
    t0 = max(jy[n] for n in names if 'Shoulder' in n or n.endswith('Arm')) + 0.03
    pr = j['meshes'][0]['primitives'][0]
    P = read(j, bytes(b), pr['attributes']['POSITION'])
    J = read(j, bytes(b), pr['attributes']['JOINTS_0']).astype(np.int64)
    W = read(j, bytes(b), pr['attributes']['WEIGHTS_0']).astype(np.float32)
    hw = (W * (J == hi)).sum(1)
    s = np.clip((P[:, 1] - t0) / 0.08, 0, 1) * (hw >= 0.25)   # only what already leans on the head (not a sword on the back)
    acc = np.zeros((len(P), len(names)), np.float32)
    for k in range(4): np.add.at(acc, (np.arange(len(P)), J[:, k]), W[:, k] * (1 - s))
    acc[:, hi] += s
    top = np.argsort(-acc, 1)[:, :4]; tw = np.take_along_axis(acc, top, 1); tw /= np.maximum(tw.sum(1, keepdims=True), 1e-6)
    for key, arr in (('JOINTS_0', top), ('WEIGHTS_0', tw)):
        a = j['accessors'][pr['attributes'][key]]; v = j['bufferViews'][a['bufferView']]
        dt = {5121: np.uint8, 5123: np.uint16, 5126: np.float32}[a['componentType']]
        raw = arr.astype(dt).tobytes(); st = v.get('byteStride'); off = v.get('byteOffset', 0) + a.get('byteOffset', 0)
        assert not st or st == len(raw) // len(P), 'interleaved buffers not supported'
        b[off:off + len(raw)] = raw
    out = m._glb_join(j, bytes(b))
    open(path, 'w' if txt else 'wb').write(base64.b64encode(out).decode() if txt else out)
    print('%s: shoulders at y=%.3f, %d vertices made rigid to Head (%d partly)' % (os.path.basename(path), t0, int((s >= 1).sum()), int(((s > 0) & (s < 1)).sum())))


if __name__ == '__main__':
    for p in sys.argv[1:]: fix(p)
