"""Check exported mounted-idle pose remains skinned and seated after FBX reimport."""
from pathlib import Path
import bpy,json,math
ROOT=Path(__file__).resolve().parents[2]
path=ROOT/'Assets/CharacterRigging/Stage5/Mounted/ArmoredKnight_MountedIdle.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(path))
rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
bodies=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.find_armature()]
assert len(rigs)==1 and len(bodies)==1
rig=rigs[0];body=bodies[0]
assert len(rig.data.bones)==23
assert all(v.groups and len(v.groups)<=4 and abs(sum(g.weight for g in v.groups)-1)<.01
           for v in body.data.vertices)
action=rig.animation_data.action
assert action and action.frame_range[1]-action.frame_range[0]>=20
for frame in [action.frame_range[0],action.frame_range[1]]:
    bpy.context.scene.frame_set(int(frame))
    ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
    assert all(math.isfinite(c) for v in mesh.vertices for c in v.co)
    ev.to_mesh_clear()
result={'name':'ArmoredKnight_MountedIdle','pass':True,'bones':len(rig.data.bones),
        'vertices':len(body.data.vertices),'frames':list(action.frame_range),
        'note':'Seated hold pose only; horse movement/mounted attacks not validated'}
report=ROOT/'Docs/CharacterMeshAudit/Stage5/mounted-idle-validation.json'
report.write_text(json.dumps(result,indent=2),encoding='utf-8')
print('PASS_MOUNTED_IDLE',json.dumps(result),flush=True)
