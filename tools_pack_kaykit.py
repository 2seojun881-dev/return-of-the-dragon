import json,struct,os,hashlib,base64,glob
def read_any(path):
    if path.endswith('.glb'):
        f=open(path,'rb').read();jl=struct.unpack('<I',f[12:16])[0];j=json.loads(f[20:20+jl]);o=20+jl
        bufs=[]
        if o<len(f):
            bl=struct.unpack('<I',f[o:o+4])[0];bufs=[f[o+8:o+8+bl]]
        return j,bufs,os.path.dirname(path)
    j=json.load(open(path));d=os.path.dirname(path)
    bufs=[open(os.path.join(d,b['uri']),'rb').read() for b in j.get('buffers',[])]
    return j,bufs,d
def write_glb(j,out,path):
    while len(out)%4: out.append(0)
    j['buffers']=[{'byteLength':len(out)}] if len(out) else []
    js=json.dumps(j,separators=(',',':')).encode()
    while len(js)%4: js+=b' '
    chunks=struct.pack('<II',len(js),0x4E4F534A)+js
    if len(out): chunks+=struct.pack('<II',len(out),0x004E4942)+bytes(out)
    g=struct.pack('<III',0x46546C67,2,12+len(chunks))+chunks
    open(path,'wb').write(g); open(path+'.txt','w').write(base64.b64encode(g).decode())
    return len(g)

class Merger:
    def __init__(s):
        s.j={'asset':{'version':'2.0'},'scene':0,'scenes':[{'nodes':[]}],'nodes':[],'meshes':[],'accessors':[],'bufferViews':[],'materials':[],'textures':[],'images':[],'samplers':[],'skins':[],'animations':[]}
        s.out=bytearray();s.imgh={};s.samh={};s.texh={};s.math={}
    def bv(s,data,target=None):
        while len(s.out)%4: s.out.append(0)
        v={'buffer':0,'byteOffset':len(s.out),'byteLength':len(data)}
        if target: v['target']=target
        s.out+=data; s.j['bufferViews'].append(v); return len(s.j['bufferViews'])-1
    def add(s,name,path,anims=None,meshes=True,root_name=None):
        j,bufs,d=read_any(path)
        bvm={}
        def getbv(i):
            if i not in bvm:
                v=j['bufferViews'][i];b=bufs[v.get('buffer',0)];o=v.get('byteOffset',0)
                nv=s.bv(b[o:o+v['byteLength']],v.get('target'))
                if 'byteStride' in v: s.j['bufferViews'][nv]['byteStride']=v['byteStride']
                bvm[i]=nv
            return bvm[i]
        acm={}
        def getacc(i):
            if i not in acm:
                a=dict(j['accessors'][i])
                if 'bufferView' in a: a['bufferView']=getbv(a['bufferView'])
                s.j['accessors'].append(a);acm[i]=len(s.j['accessors'])-1
            return acm[i]
        imm={}
        def getimg(i):
            if i in imm: return imm[i]
            im=j['images'][i]
            if 'uri' in im: data=open(os.path.join(d,im['uri']),'rb').read();mime=im.get('mimeType','image/png')
            else:
                v=j['bufferViews'][im['bufferView']];b=bufs[v.get('buffer',0)];o=v.get('byteOffset',0);data=b[o:o+v['byteLength']];mime=im.get('mimeType','image/png')
            h=hashlib.sha1(data).hexdigest()
            if h not in s.imgh:
                s.j['images'].append({'bufferView':s.bv(data),'mimeType':mime,'name':im.get('name','')});s.imgh[h]=len(s.j['images'])-1
            imm[i]=s.imgh[h];return imm[i]
        def gettex(i):
            t=j['textures'][i];smp=j['samplers'][t['sampler']] if 'sampler' in t else {}
            k=json.dumps(smp,sort_keys=True)
            if k not in s.samh: s.j['samplers'].append(smp);s.samh[k]=len(s.j['samplers'])-1
            key=(s.samh[k],getimg(t['source']))
            if key not in s.texh: s.j['textures'].append({'sampler':key[0],'source':key[1]});s.texh[key]=len(s.j['textures'])-1
            return s.texh[key]
        def fix_tex(o):
            if isinstance(o,dict):
                for k,v in list(o.items()):
                    if k.endswith('Texture') and isinstance(v,dict) and 'index' in v: v=dict(v);v['index']=gettex(v['index']);o[k]=v
                    else: fix_tex(v)
        mm={}
        def getmat(i):
            if i not in mm:
                m=json.loads(json.dumps(j['materials'][i]));fix_tex(m);k=json.dumps(m,sort_keys=True)
                if k not in s.math: s.j['materials'].append(m);s.math[k]=len(s.j['materials'])-1
                mm[i]=s.math[k]
            return mm[i]
        base=len(s.j['nodes']);mesh_map={}
        for ni,n in enumerate(j['nodes']):
            nn={k:v for k,v in n.items() if k not in('mesh','skin','children')}
            if 'children' in n: nn['children']=[c+base for c in n['children']]
            if meshes and 'mesh' in n:
                mi=n['mesh']
                if mi not in mesh_map:
                    me=json.loads(json.dumps(j['meshes'][mi]))
                    for p in me['primitives']:
                        p['attributes']={k:getacc(v) for k,v in p['attributes'].items()}
                        if 'indices' in p: p['indices']=getacc(p['indices'])
                        if 'material' in p: p['material']=getmat(p['material'])
                        p.pop('targets',None)
                    s.j['meshes'].append(me);mesh_map[mi]=len(s.j['meshes'])-1
                nn['mesh']=mesh_map[mi]
            s.j['nodes'].append(nn)
        if meshes:
            for ni,n in enumerate(j['nodes']):
                if 'skin' in n:
                    sk=j['skins'][n['skin']];nsk={'joints':[x+base for x in sk['joints']]}
                    if 'inverseBindMatrices' in sk: nsk['inverseBindMatrices']=getacc(sk['inverseBindMatrices'])
                    if 'skeleton' in sk: nsk['skeleton']=sk['skeleton']+base
                    s.j['skins'].append(nsk);s.j['nodes'][base+ni]['skin']=len(s.j['skins'])-1
        for a in j.get('animations',[]):
            if anims is None or a['name'] not in anims: continue
            na={'name':a['name'],'samplers':[],'channels':[]}
            for sm in a['samplers']: na['samplers'].append({'input':getacc(sm['input']),'output':getacc(sm['output']),'interpolation':sm.get('interpolation','LINEAR')})
            for ch in a['channels']: na['channels'].append({'sampler':ch['sampler'],'target':{'node':ch['target']['node']+base,'path':ch['target']['path']}})
            s.j['animations'].append(na)
        rn=root_name or name
        for k in range(base,len(s.j['nodes'])):
            if s.j['nodes'][k].get('name')==rn: s.j['nodes'][k]['name']=rn+'_mesh'
        roots=[r+base for r in j['scenes'][j.get('scene',0)]['nodes']]
        s.j['nodes'].append({'name':root_name or name,'children':roots});s.j['scenes'][0]['nodes'].append(len(s.j['nodes'])-1)
    def save(s,path):
        for k in ['skins','animations','images','textures','samplers','materials','meshes']:
            if not s.j[k]: del s.j[k]
        return write_glb(s.j,s.out,path)

ANIMS=['Idle','Idle_B','Idle_Combat','2H_Melee_Idle','Running_A','Running_B','Walking_A','Walking_B','2H_Melee_Attack_Chop','2H_Melee_Attack_Stab','2H_Melee_Attack_Spin','2H_Melee_Attack_Spinning','1H_Melee_Attack_Chop','1H_Melee_Attack_Slice_Diagonal','Block','2H_Ranged_Shoot','2H_Ranged_Aiming','1H_Ranged_Shoot','Dodge_Backward','Throw','Spellcast_Shoot','Spellcast_Raise','Spellcast_Long','Spellcasting','Spellcast_Summon','Hit_A','Hit_B','Death_A','Death_B','Death_C_Skeletons','Lie_Idle','Lie_StandUp','Cheer','Interact','Spawn_Ground_Skeletons','Sit_Floor_Idle','Skeletons_Inactive_Floor_Pose','Skeletons_Awaken_Floor','Jump_Full_Short','1H_Melee_Attack_Stab','Running_C','Dualwield_Melee_Attack_Chop','Dualwield_Melee_Attack_Slice','Dualwield_Melee_Attack_Stab','Dodge_Forward','1H_Melee_Attack_Slice_Horizontal','1H_Melee_Attack_Jump_Chop','Taunt','Use_Item','Blocking']
A='kk/addons/kaykit_character_pack_adventures/Characters/gltf/'
S='kkp/KayKit-Character-Pack-Skeletons-1.0/addons/kaykit_character_pack_skeletons/'
M='kkp/KayKit-Medieval-Hexagon-Pack-1.0/addons/kaykit_medieval_hexagon_pack/Assets/gltf/'
H='kkp/KayKit-Halloween-Bits-1.0/addons/kaykit_halloween_bits/Assets/gltf/'
Dd='kkp/KayKit-Dungeon-Remastered-1.0/addons/kaykit_dungeon_remastered/Assets/gltf/'
os.makedirs('kmodels',exist_ok=True)
sizes={}
# animations (no meshes)
m=Merger();m.add('anims',S+'Characters/gltf/Skeleton_Minion.glb',anims=ANIMS,meshes=False);sizes['anims']=m.save('kmodels/anims.glb')
# characters (no animations)
for n in ['Knight','Rogue_Hooded','Mage','Barbarian','Rogue']:
    m=Merger();m.add(n,A+n+'.glb',anims=[]);sizes[n]=m.save('kmodels/'+n+'.glb')
for n in ['Skeleton_Minion','Skeleton_Warrior','Skeleton_Rogue','Skeleton_Mage']:
    m=Merger();m.add(n,S+'Characters/gltf/'+n+'.glb',anims=[]);sizes[n]=m.save('kmodels/'+n+'.glb')
def find(root,name):
    r=glob.glob(root+'**/'+name+'.gltf',recursive=True)+glob.glob(root+'**/'+name+'.gltf.glb',recursive=True)+glob.glob(root+'**/'+name+'.glb',recursive=True)
    r=[x for x in r if 'fbx' not in x]
    assert r,(root,name); return r[0]
TOWN=['building_home_A_red','building_home_B_red','building_tavern_red','building_blacksmith_red','building_well_red','building_windmill_red','building_market_blue','building_castle_blue','building_tower_A_blue','building_tower_B_blue','building_church_blue','building_destroyed',
 'barrel','crate_A_big','crate_B_small','crate_long_A','sack','tent','weaponrack','wheelbarrow','target','fence_wood_straight','fence_wood_straight_gate','fence_stone_straight','wall_straight','wall_straight_gate','wall_corner_A_outside','flag_blue','flag_red','trees_A_large','trees_A_medium','trees_B_large','trees_B_medium','tree_single_A','tree_single_B','rock_single_A','rock_single_B','rock_single_C','rock_single_D','rock_single_E','mountain_A_grass_trees','mountain_B_grass_trees','mountain_C','mountain_A','hills_A_trees','hill_single_A','resource_lumber','bucket_arrows','hex_grass']
m=Merger()
for n in TOWN: m.add(n,find(M,n))
sizes['town']=m.save('kmodels/town.glb')
DARK_H=['tree_dead_large','tree_dead_medium','tree_dead_small','grave_A','gravestone','gravemarker_A','lantern_standing','post_lantern','post_skull','fence','fence_broken','skull','ribcage','bone_A','shrine_candles','arch_gate']
DARK_D=['banner_blue','banner_patternA_blue','banner_red','banner_shield_blue','barrier','barrier_column','column','pillar_decorated','table_long','table_long_decorated_A','chair','bed_decorated','chest','box_large','barrel_large','keg','torch_lit','torch_mounted','candle_triple','sword_shield','rubble_large','crates_stacked','box_stacked','shelf_large','coin','coin_stack_small','coin_stack_large','chest_gold','candle_lit']
DARK_S=['Skeleton_Blade','Skeleton_Axe','Skeleton_Crossbow','Skeleton_Staff','Skeleton_Shield_Small_A','Skeleton_Shield_Large_A','Skeleton_Shield_Large_B']
m=Merger()
for n in DARK_H: m.add(n,find(H,n))
for n in DARK_D: m.add(n,find(Dd,n))
for n in DARK_S: m.add(n,find(S,n))
sizes['dark']=m.save('kmodels/dark.glb')
tot=0
for k,v in sizes.items(): print(k,v);tot+=v
print('total',tot)
