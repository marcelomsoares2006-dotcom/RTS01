import bpy, os, random

blend=os.path.abspath('Assets/CityArchitecture/Generated/EraBuildingKit_PBR.blend')
out=os.path.abspath('Assets/CityArchitecture/Generated/EraBuildingKit_Textured.fbx')
texdir=os.path.abspath('Assets/CityArchitecture/Generated/Textures'); os.makedirs(texdir,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=blend)

palette={'PBR_Stone':((74,78,78),(135,130,112)),'PBR_Plaster':((112,77,47),(205,165,104)),'PBR_Wood':((45,16,5),(120,50,16)),'PBR_Roof':((28,12,7),(90,35,18)),'PBR_Trim':((120,66,10),(235,170,44))}
def texture(matname,lo,hi):
    path=os.path.join(texdir,matname+'.png'); img=bpy.data.images.get(matname+'_Texture') or bpy.data.images.new(matname+'_Texture',width=512,height=512)
    pix=[]; random.seed(matname)
    for _ in range(512*512):
        n=random.random()*.18; pix.extend([(lo[i]*(1-n)+hi[i]*n)/255 for i in range(3)]+[1])
    img.pixels= pix; img.filepath_raw=path; img.file_format='PNG'; img.save()
    m=bpy.data.materials.get(matname)
    if not m or not m.use_nodes: return
    nodes=m.node_tree.nodes; links=m.node_tree.links; bs=next((x for x in nodes if x.type=='BSDF_PRINCIPLED'),None); tex=nodes.get('PBR_Texture') or nodes.new('ShaderNodeTexImage'); tex.name='PBR_Texture'; tex.image=img
    if bs: links.new(tex.outputs['Color'],bs.inputs['Base Color'])
for name,(lo,hi) in palette.items(): texture(name,lo,hi)
for obj in bpy.context.scene.objects:
    if obj.type!='MESH': continue
    mesh=obj.data
    if 'LightmapUV' not in mesh.uv_layers: mesh.uv_layers.new(name='LightmapUV')
    mesh.uv_layers.active_index=mesh.uv_layers.find('LightmapUV'); bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.lightmap_pack(PREF_MARGIN_DIV=0.03); bpy.ops.object.mode_set(mode='OBJECT'); obj.select_set(False)
bpy.ops.object.select_all(action='SELECT'); bpy.context.view_layer.objects.active=bpy.context.scene.objects.get('EraBuildingKit')
bpy.ops.wm.save_as_mainfile(filepath=out.replace('.fbx','.blend'))
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=False)
print('TEXTURES_UV2_EXPORTED',out)
