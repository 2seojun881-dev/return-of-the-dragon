import sys,os,json,base64
sys.path.insert(0,'/home/user/trading-bot/dragon-raid/tools/meshy');import meshy_gen as m
OUT='/home/user/trading-bot/dragon-raid/models/custom'
def transplant(rig,src,dst):
    """rigged mesh (Meshy keeps the UV layout) + the original PBR textures (base color, metal/rough, normal)."""
    rj,rb=m._glb_split(open(rig,'rb').read());sj,sb=m._glb_split(open(src,'rb').read())
    out=bytearray(rb);out+=b'\0'*(-len(out)%4)
    newimg=[]
    for im in sj['images']:
        v=sj['bufferViews'][im['bufferView']];o=v.get('byteOffset',0);data=sb[o:o+v['byteLength']]
        out+=b'\0'*(-len(out)%4);rj['bufferViews'].append({'buffer':0,'byteOffset':len(out),'byteLength':len(data)});out+=data
        newimg.append({'bufferView':len(rj['bufferViews'])-1,'mimeType':im.get('mimeType','image/jpeg')})
    rj['images']=newimg;rj['samplers']=sj.get('samplers',[{}]) or [{}]
    rj['textures']=[{'source':t['source'],'sampler':t.get('sampler',0)} for t in sj['textures']]
    mat=json.loads(json.dumps(sj['materials'][0]));mat.pop('doubleSided',None)
    rj['materials']=[mat]
    for me in rj['meshes']:
        for pr in me['primitives']:pr['material']=0
    rj['buffers']=[{'byteLength':len(out)}]
    open(dst,'wb').write(m._glb_join(rj,bytes(out)))
for key,src,h in [(a.split(':')[0],a.split(':')[1],1.6) for a in sys.argv[1:]]:
    inp=src.replace('.glb','_hq.glb')
    if not os.path.exists(inp): m.optimize(src,inp,2048,max_tris=80000)
    uri='data:application/octet-stream;base64,'+base64.b64encode(open(inp,'rb').read()).decode()
    try:
        rid=m.api('POST','/openapi/v1/rigging',{'model_url':uri,'height_meters':h})['result'];t=m.wait('/openapi/v1/rigging/'+rid,'rig '+key)
    except SystemExit as e: print('FAIL',key,e);continue
    rig=m.download(t['result']['rigged_character_glb_url'],key+'_rig.glb')
    transplant(rig,inp,key+'_final.glb')
    d=open(key+'_final.glb','rb').read()
    open(os.path.join(OUT,key+'.glb.txt'),'w').write(base64.b64encode(d).decode())
    print('OK',key,m.triangles(key+'_final.glb'),len(d)//1024,'KB',flush=True)
