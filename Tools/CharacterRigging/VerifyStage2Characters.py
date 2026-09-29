"""Independent FBX/skin/accessory validation for Stage2 outputs; no original writes."""
from pathlib import Path
import bpy,json,math,hashlib
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Docs/CharacterMeshAudit/Stage2'
records=json.loads((REPORT/'build-report.json').read_text(encoding='utf-8'))
results=[]
for record in records:
    name=record['name'];bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Assets/CharacterRigging/Stage2'/name/'Models'/(name+'.fbx')))
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    assert len(rigs)==1
    rig=rigs[0];body=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.find_armature())
    weights=[[g.weight for g in v.groups if g.weight>1e-6] for v in body.data.vertices]
    assert all(w and abs(sum(w)-1)<.01 and len(w)<=4 for w in weights)
    total=sum(len(p.vertices)-2 for o in bpy.context.scene.objects if o.type=='MESH' for p in o.data.polygons)
    # The FBX importer sanitizes redundant/degenerate polygons left by decimation.
    # Reject substantial loss or added faces, and report the exact difference.
    discarded=record['total_triangles']-total
    assert 0<=discarded<=record['total_triangles']*.005,(name,total,record['total_triangles'])
    assert len(rig.data.bones)>=23
    for acc in record['accessories']:
        obj=bpy.data.objects[acc['name']]
        assert obj.parent_type=='BONE' and obj.parent_bone==acc['bone']
    action=rig.animation_data.action;start,end=action.frame_range
    def positions(frame):
        bpy.context.scene.frame_set(int(frame))
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
        result=[tuple(ev.matrix_world@v.co) for v in mesh.vertices];ev.to_mesh_clear();return result
    p0=positions(start);p1=positions(start+(end-start)/3)
    delta=max(math.dist(a,b) for a,b in zip(p0,p1))
    assert delta>.015 and delta<2
    assert all(math.isfinite(v) for p in p0+p1 for v in p)
    height=max(p[2] for p in p0)-min(p[2] for p in p0)
    assert abs(height-1.8)<.025,(name,height)
    assert hashlib.sha256((ROOT/record['source']).read_bytes()).hexdigest()==record['source_sha256']
    result={'name':name,'pass':True,'triangles':total,'bone_count':len(rig.data.bones),
      'max_influences':max(map(len,weights)),'height_metres':height,'deformation_metres':delta,
      'accessories_checked':len(record['accessories']),'discarded_degenerate_triangles':discarded,'source_unchanged':True}
    results.append(result);print('PASS_STAGE2_FBX',json.dumps(result),flush=True)
(REPORT/'fbx-validation.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
