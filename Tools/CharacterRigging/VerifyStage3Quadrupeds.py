"""Independent Stage3 FBX import and deformation audit in Blender."""
from pathlib import Path
import bpy, hashlib, json, math
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Docs/CharacterMeshAudit/Stage3'
records=json.loads((REPORT/'build-report.json').read_text(encoding='utf-8'))
results=[]
for record in records:
    name=record['name'];bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/CharacterRigging/Stage3'/name/'Models'/(name+'.fbx')))
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(rigs)==1,(name,'rig count',len(rigs))
    rig=rigs[0];body=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.find_armature())
    assert len(rig.data.bones)==record['bone_count']
    assert body.data.uv_layers
    weights=[[g.weight for g in v.groups if g.weight>1e-6] for v in body.data.vertices]
    assert all(w and len(w)<=4 and abs(sum(w)-1)<.01 for w in weights),(name,'weights')
    count=sum(len(p.vertices)-2 for p in body.data.polygons)
    # FBX importer drops thin/redundant faces in generated decimated geometry.
    assert 0 <= record['triangles']-count < record['triangles']*.01,(name,count)
    action=rig.animation_data.action;start,end=action.frame_range
    def sample(frame):
        bpy.context.scene.frame_set(int(frame))
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
        pos=[tuple(ev.matrix_world@v.co) for v in mesh.vertices];ev.to_mesh_clear();return pos
    first=sample(start);bent=sample(start+(end-start)/3)
    assert len(first)==len(bent)
    delta=max(math.dist(a,b) for a,b in zip(first,bent))
    assert .015<delta<2,(name,delta)
    assert all(math.isfinite(v) for p in first+bent for v in p)
    height=max(p[2] for p in first)-min(p[2] for p in first)
    assert abs(height-record['height_metres'])<.025,(name,height)
    assert hashlib.sha256((ROOT/record['source']).read_bytes()).hexdigest()==record['source_sha256']
    result={'name':name,'pass':True,'source_unchanged':True,'triangles':count,'discarded_faces':record['triangles']-count,
            'bones':len(rig.data.bones),'max_influences':max(map(len,weights)),'height_metres':height,
            'deformation_metres':delta,'uv_layers':len(body.data.uv_layers)}
    results.append(result);print('PASS_STAGE3_FBX',json.dumps(result),flush=True)
(REPORT/'fbx-validation.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
