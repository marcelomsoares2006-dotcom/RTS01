"""Generate two skinned LOD FBXs per prepared character; no source models modified."""
from pathlib import Path
import bpy,json
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Docs/CharacterMeshAudit/Stage4';REPORT.mkdir(parents=True,exist_ok=True)
SOURCES={
 'HeraldicKnight':ROOT/'Assets/CharacterRigging/HeraldicKnight/Models/HeraldicKnight.fbx',
 'Farmhand':ROOT/'Assets/CharacterRigging/Stage2/Farmhand/Models/Farmhand.fbx',
 'Spearman':ROOT/'Assets/CharacterRigging/Stage2/Spearman/Models/Spearman.fbx',
 'Horse':ROOT/'Assets/CharacterRigging/Stage3/Horse/Models/Horse.fbx',
 'RedDeer':ROOT/'Assets/CharacterRigging/Stage3/RedDeer/Models/RedDeer.fbx',
}
records=[]
for name,src in SOURCES.items():
    if not src.exists():raise FileNotFoundError(src)
    for level,ratio in [(1,.5),(2,.25)]:
        if name=='Spearman' and level==2:ratio=.4
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(src))
        rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
        bodies=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.find_armature()]
        assert len(rigs)==1 and len(bodies)==1,(name,len(rigs),len(bodies))
        body=bodies[0];rig=rigs[0]
        source_tris=sum(len(p.vertices)-2 for p in body.data.polygons)
        bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
        total=source_tris;target=source_tris*ratio
        for attempt in range(6):
            if total<=target*1.1:break
            dec=body.modifiers.new('RTS_LOD_Decimate_'+str(attempt),'DECIMATE')
            dec.ratio=max(.1,min(.95,target/total));dec.use_collapse_triangulate=True
            bpy.ops.object.modifier_apply(modifier=dec.name)
            new_total=sum(len(p.vertices)-2 for p in body.data.polygons)
            if new_total>=total:break
            total=new_total
        if not (source_tris*.18<total<source_tris*.6):
            records.append({'name':name,'lod':level,'source_triangles':source_tris,'triangles':total,
                            'skipped':True,'reason':'Decimation could not meet LOD budget without rebuilding topology'})
            (REPORT/'lod-build.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
            print('STAGE4_LOD_SKIPPED',name,level,total,flush=True)
            continue
        bpy.ops.object.vertex_group_limit_total(limit=4)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
        folder=ROOT/'Assets/CharacterRigging/LODs'/name;folder.mkdir(parents=True,exist_ok=True)
        dst=folder/(name+'_LOD'+str(level)+'.fbx')
        bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);body.select_set(True)
        bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=str(dst),use_selection=True,object_types={'ARMATURE','MESH'},
            add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,
            axis_forward='-Z',axis_up='Y',path_mode='STRIP')
        record={'name':name,'lod':level,'source_triangles':source_tris,'triangles':total,
                'path':str(dst.relative_to(ROOT))}
        records.append(record);print('STAGE4_LOD',json.dumps(record),flush=True)
        (REPORT/'lod-build.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
