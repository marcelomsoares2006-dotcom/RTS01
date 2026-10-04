"""Reimport and validate Stage5 armoured character FBXs independently."""
from pathlib import Path
import bpy,hashlib,json,math
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Docs/CharacterMeshAudit/Stage5'
records=json.loads((REPORT/'build-report.json').read_text(encoding='utf-8'))
results=[]
for r in records:
    name=r['name'];bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/CharacterRigging/Stage5'/name/'Models'/(name+'.fbx')))
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(rigs)==1,(name,'rig')
    rig=rigs[0];bodies=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.find_armature()]
    assert len(bodies)==1,(name,'body')
    body=bodies[0];weights=[[g.weight for g in v.groups if g.weight>1e-6] for v in body.data.vertices]
    assert all(w and len(w)<=4 and abs(sum(w)-1)<.01 for w in weights),(name,'weights')
    count=sum(len(p.vertices)-2 for p in body.data.polygons)
    assert 0<=r['total_triangles']-count<r['total_triangles']*.01,(name,count)
    assert len(rig.data.bones)>=23 and body.data.uv_layers
    action=rig.animation_data.action;start,end=action.frame_range
    def positions(frame):
        bpy.context.scene.frame_set(int(frame))
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
        result=[tuple(ev.matrix_world@v.co) for v in mesh.vertices];ev.to_mesh_clear();return result
    a=positions(start);b=positions(start+(end-start)/3)
    delta=max(math.dist(u,v) for u,v in zip(a,b))
    assert .015<delta<2 and all(math.isfinite(v) for p in a+b for v in p),(name,delta)
    height=max(p[2] for p in a)-min(p[2] for p in a)
    assert abs(height-1.8)<.03,(name,height)
    assert hashlib.sha256((ROOT/r['source']).read_bytes()).hexdigest()==r['source_sha256']
    result={'name':name,'pass':True,'triangles':count,'bones':len(rig.data.bones),
            'max_influences':max(map(len,weights)),'height_metres':height,
            'deformation_metres':delta,'source_unchanged':True}
    results.append(result);print('PASS_STAGE5_FBX',json.dumps(result),flush=True)
(REPORT/'fbx-validation.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
