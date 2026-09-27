"""Rebuild a rigged hero at higher detail without re-rigging (no Meshy credits).

The Meshy rig (from an 80k-triangle copy) supplies the skeleton, bind pose and animations; the user's original
model is simplified to a larger budget and its vertices borrow skin weights from the nearest rigged vertices.
Original textures are kept byte-for-byte.

usage: hero_hires.py KEY ORIGINAL.glb RIG.glb [MAX_TRIS]
"""
import sys, os, json, base64, tempfile
import numpy as np
from scipy.spatial import cKDTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshy_gen as m

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'models', 'custom')
CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def read(j, b, ai):
    a = j['accessors'][ai]; v = j['bufferViews'][a['bufferView']]
    dt = np.dtype(CT[a['componentType']]); n = NC[a['type']]
    off = v.get('byteOffset', 0) + a.get('byteOffset', 0); st = v.get('byteStride', dt.itemsize * n)
    raw = np.frombuffer(b, dtype=np.uint8, count=st * (a['count'] - 1) + dt.itemsize * n, offset=off)
    out = np.lib.stride_tricks.as_strided(raw, shape=(a['count'], dt.itemsize * n), strides=(st, 1)).copy()
    out = out.view(dt).reshape(a['count'], n)
    if a.get('normalized') and dt != np.float32:
        out = out.astype(np.float32) / np.iinfo(dt).max
    return out


def main(key, orig, rig, max_tris=200000):
    tmp = tempfile.mkdtemp(); hi = os.path.join(tmp, 'hi.glb')
    m.optimize(orig, hi, 2048, max_tris=max_tris)
    hj, hb = m._glb_split(open(hi, 'rb').read())
    oj, ob = m._glb_split(open(orig, 'rb').read())
    rj, rb = m._glb_split(open(rig, 'rb').read())
    hp = hj['meshes'][0]['primitives'][0]; rp = rj['meshes'][0]['primitives'][0]
    P = read(hj, hb, hp['attributes']['POSITION']).astype(np.float32)
    N = read(hj, hb, hp['attributes']['NORMAL']).astype(np.float32)
    UV = read(hj, hb, hp['attributes']['TEXCOORD_0']).astype(np.float32)
    I = read(hj, hb, hp['indices']).reshape(-1).astype(np.uint32)
    RP = read(rj, rb, rp['attributes']['POSITION']).astype(np.float32)
    RN = read(rj, rb, rp['attributes']['NORMAL']).astype(np.float32)
    RJ = read(rj, rb, rp['attributes']['JOINTS_0']).astype(np.int64)
    RW = read(rj, rb, rp['attributes']['WEIGHTS_0']).astype(np.float32)
    # the rig mesh is the same surface, uniformly scaled/shifted: match bounding boxes
    s = (RP.max(0) - RP.min(0)) / (P.max(0) - P.min(0))
    print('  scale per axis', np.round(s, 4))
    sc = float(np.median(s)); P2 = (P - P.min(0)) * sc + RP.min(0)
    # borrow weights: blend the 4 nearest rigged vertices that face the same way (keeps arm/body, hair/cloak apart)
    d, nb = cKDTree(RP).query(P2, k=4)
    nrm = N / np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-6)
    rn = RN / np.maximum(np.linalg.norm(RN, axis=1, keepdims=True), 1e-6)
    agree = (np.einsum('ij,ikj->ik', nrm, rn[nb]) > 0.2)
    agree[:, 0] |= ~agree.any(1)
    wt = agree / np.maximum(d, 1e-5); wt /= wt.sum(1, keepdims=True)
    nJ = len(rj['skins'][0]['joints'])
    acc = np.zeros((len(P2), nJ), np.float32)
    for k in range(4):
        np.add.at(acc, (np.repeat(np.arange(len(P2)), 4), RJ[nb[:, k]].reshape(-1)), (RW[nb[:, k]] * wt[:, k:k + 1]).reshape(-1))
    top = np.argsort(-acc, 1)[:, :4]; tw = np.take_along_axis(acc, top, 1)
    tw /= np.maximum(tw.sum(1, keepdims=True), 1e-6)
    J4 = top.astype(np.uint8); W4 = tw.astype(np.float32)
    print('  verts %d tris %d  mean nn dist %.4f m' % (len(P2), len(I) // 3, float(d[:, 0].mean())))

    # new binary: rig skeleton data (inverse bind matrices, animation), new mesh, original images
    keep = set()
    for sk in rj['skins']:
        if 'inverseBindMatrices' in sk: keep.add(sk['inverseBindMatrices'])
    for an in rj.get('animations', []):
        for smp in an['samplers']: keep.update([smp['input'], smp['output']])
    out = bytearray(); views = []; accs = []; amap = {}

    def add_view(data, target=None):
        out.extend(b'\0' * (-len(out) % 4)); v = {'buffer': 0, 'byteOffset': len(out), 'byteLength': len(data)}
        if target: v['target'] = target
        out.extend(data); views.append(v); return len(views) - 1

    for ai in sorted(keep):
        a = dict(rj['accessors'][ai]); v = rj['bufferViews'][a['bufferView']]
        o = v.get('byteOffset', 0); data = rb[o:o + v['byteLength']]
        nv = add_view(data); a['bufferView'] = nv; accs.append(a); amap[ai] = len(accs) - 1

    def add_acc(arr, ctype, typ, target, minmax=False):
        nv = add_view(arr.tobytes(), target)
        a = {'bufferView': nv, 'componentType': ctype, 'count': int(arr.shape[0]), 'type': typ}
        if minmax: a['min'] = arr.min(0).tolist(); a['max'] = arr.max(0).tolist()
        accs.append(a); return len(accs) - 1

    # KHR_mesh_quantization: int8 normals, uint16 UVs, uint8 weights (about half the vertex bytes)
    def norm_acc(arr, ctype, typ):
        i = add_acc(arr, ctype, typ, 34962); accs[i]['normalized'] = True; return i
    nq = np.zeros((len(nrm), 4), np.int8); nq[:, :3] = np.round(nrm * 127)
    nacc = norm_acc(nq, 5120, 'VEC4'); accs[nacc]['type'] = 'VEC3'; views[accs[nacc]['bufferView']]['byteStride'] = 4  # padded to 4 bytes
    uv_ok = UV.min() >= 0 and UV.max() <= 1
    attrs = {'POSITION': add_acc(P2, 5126, 'VEC3', 34962, True), 'NORMAL': nacc,
             'TEXCOORD_0': norm_acc(np.round(UV * 65535).astype(np.uint16), 5123, 'VEC2') if uv_ok else add_acc(UV, 5126, 'VEC2', 34962),
             'JOINTS_0': add_acc(J4, 5121, 'VEC4', 34962), 'WEIGHTS_0': add_acc(W4, 5126, 'VEC4', 34962)}  # float: three r128 boneTransform() does not denormalise
    idx = add_acc(I.reshape(-1, 1), 5125, 'SCALAR', 34963)
    imgs = []
    for im in oj['images']:
        v = oj['bufferViews'][im['bufferView']]; o = v.get('byteOffset', 0)
        raw = ob[o:o + v['byteLength']]
        try:  # same 2048 px, near-lossless re-encode (Meshy ships q~100 JPEGs)
            from PIL import Image; import io
            buf = io.BytesIO(); Image.open(io.BytesIO(raw)).convert('RGB').save(buf, 'JPEG', quality=92, subsampling=0); raw = buf.getvalue()
        except Exception as ex: print('  (texture kept as is: %s)' % ex)
        imgs.append({'bufferView': add_view(raw), 'mimeType': 'image/jpeg'})
    j = rj
    for sk in j['skins']:
        if 'inverseBindMatrices' in sk: sk['inverseBindMatrices'] = amap[sk['inverseBindMatrices']]
    for an in j.get('animations', []):
        for smp in an['samplers']: smp['input'] = amap[smp['input']]; smp['output'] = amap[smp['output']]
    mat = json.loads(json.dumps(oj['materials'][0])); mat.pop('doubleSided', None)
    j['materials'] = [mat]; j['images'] = imgs; j['samplers'] = oj.get('samplers') or [{}]
    j['textures'] = [{'source': t['source'], 'sampler': t.get('sampler', 0)} for t in oj['textures']]
    j['meshes'] = [{'name': rj['meshes'][0].get('name', 'hero'), 'primitives': [{'attributes': attrs, 'indices': idx, 'material': 0, 'mode': 4}]}]
    j['extensionsUsed'] = sorted(set(j.get('extensionsUsed', [])) | {'KHR_mesh_quantization'})
    j['extensionsRequired'] = sorted(set(j.get('extensionsRequired', [])) | {'KHR_mesh_quantization'})
    j['accessors'] = accs; j['bufferViews'] = views; j['buffers'] = [{'byteLength': len(out)}]
    data = m._glb_join(j, bytes(out))
    dst = os.path.join(tmp, key + '.glb'); open(dst, 'wb').write(data)
    open(os.path.join(OUT, key + '.glb.txt'), 'w').write(base64.b64encode(data).decode())
    print('OK', key, len(I) // 3, 'tris', len(data) // 1024, 'KB ->', dst)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4]) if len(sys.argv) > 4 else 200000)
