"""Independently reimport LOD FBXs and check triangle budgets and skin weights."""
from pathlib import Path
import bpy,json
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Docs/CharacterMeshAudit/Stage4'
records=json.loads((REPORT/'lod-build.json').read_text(encoding='utf-8'))
for r in records:
    if r.get('skipped'):
        print('STAGE4_LOD_PENDING',r['name'],r['lod'],r['reason'],flush=True)
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/r['path']))
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    bodies=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.find_armature()]
    assert len(rigs)==1 and len(bodies)==1,r
    body=bodies[0];count=sum(len(p.vertices)-2 for p in body.data.polygons)
    if r['name']=='Horse':
        print('HORSE_IMPORT_DETAIL',[(o.name,len(o.data.vertices),len(o.vertex_groups),
              sum(bool(v.groups) for v in o.data.vertices)) for o in bodies],flush=True)
    assert 0<=r['triangles']-count<r['triangles']*.02,(r,count)
    assert count < r['source_triangles']*.6
    assert body.data.uv_layers
    for v in body.data.vertices:
        weights=[g.weight for g in v.groups if g.weight>1e-6]
        assert weights and len(weights)<=4 and abs(sum(weights)-1)<.02,(r,v.index,weights)
    r['reimport_triangles']=count;r['validated']=True
    print('PASS_STAGE4_LOD',r['name'],r['lod'],count,flush=True)
(REPORT/'lod-validation.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
