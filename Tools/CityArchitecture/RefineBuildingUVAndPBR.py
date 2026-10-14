import bpy, os

blend=os.path.abspath('Assets/CityArchitecture/Generated/EraBuildingKit.blend')
out=os.path.abspath('Assets/CityArchitecture/Generated/EraBuildingKit_PBR.fbx')
bpy.ops.wm.open_mainfile(filepath=blend)

def pbr(name,base,rough,metal=0.0):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes=True
    n=m.node_tree.nodes; l=m.node_tree.links; n.clear(); outn=n.new('ShaderNodeOutputMaterial'); bs=n.new('ShaderNodeBsdfPrincipled'); tex=n.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value=5.0; tex.inputs['Detail'].default_value=3.0; tex.inputs['Roughness'].default_value=.72
    ramp=n.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].color=(*base,1); ramp.color_ramp.elements[1].color=(min(base[0]*1.45,1),min(base[1]*1.45,1),min(base[2]*1.45,1),1)
    l.new(tex.outputs['Fac'],ramp.inputs['Fac']); l.new(ramp.outputs['Color'],bs.inputs['Base Color']); l.new(bs.outputs['BSDF'],outn.inputs['Surface']); bs.inputs['Roughness'].default_value=rough; bs.inputs['Metallic'].default_value=metal
    return m
stone=pbr('PBR_Stone',(.22,.24,.25),.88); plaster=pbr('PBR_Plaster',(.52,.38,.24),.92); wood=pbr('PBR_Wood',(.18,.055,.018),.72); roof=pbr('PBR_Roof',(.09,.045,.025),.82); trim=pbr('PBR_Trim',(.48,.25,.04),.55,.35)
for obj in bpy.context.scene.objects:
    if obj.type!='MESH': continue
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(angle_limit=1.15192,island_margin=.025); bpy.ops.object.mode_set(mode='OBJECT'); obj.select_set(False)
    for slot in obj.material_slots:
        if not slot.material: continue
        n=slot.material.name.lower()
        if 'roof' in n or 'awning' in n: slot.material=roof
        elif 'wood' in n or 'door' in n or 'post' in n: slot.material=wood
        elif 'gold' in n or 'trim' in n or 'banner' in n: slot.material=trim
        elif 'plaster' in n or 'body' in n: slot.material=plaster
        else: slot.material=stone
bpy.ops.object.select_all(action='SELECT'); bpy.context.view_layer.objects.active=bpy.context.scene.objects.get('EraBuildingKit')
bpy.ops.wm.save_as_mainfile(filepath=out.replace('.fbx','.blend'))
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=False)
print('PBR_UV_REFINEMENT_EXPORTED',out)
