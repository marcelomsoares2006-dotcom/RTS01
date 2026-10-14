import bpy, os, random, math

blend=os.path.abspath('Assets/CityArchitecture/Generated/EraBuildingKit_Textured.blend')
out=os.path.abspath('Assets/CityArchitecture/Generated/EraBuildingKit_Detailed.fbx')
texdir=os.path.abspath('Assets/CityArchitecture/Generated/Textures'); os.makedirs(texdir,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=blend)
N=512
def make(name,kind,lo,hi):
    path=os.path.join(texdir,name+'.png'); img=bpy.data.images.get(name+'_Texture') or bpy.data.images.new(name+'_Texture',width=N,height=N)
    random.seed(name+'-detail'); px=[]
    for y in range(N):
        for x in range(N):
            nx=x/N; ny=y/N; n=random.random()-.5
            if kind=='stone':
                mortar=((x//64+y//48)%2==0 and y%48<3) or (x%64<3)
                f=.18 if mortar else .72+n*.12
            elif kind=='roof': f=.35+.35*(math.sin((x+y*.35)/12)**2)+n*.08
            elif kind=='wood': f=.35+.45*(math.sin(x/18+y/70)**2)+n*.12
            elif kind=='plaster': f=.72+n*.16+.12*math.sin(x/37)*math.sin(y/53)
            else: f=.72+n*.08
            px += [max(0,min(1,(lo[i]*(1-f)+hi[i]*f)/255)) for i in range(3)]+[1]
    img.pixels=px; img.filepath_raw=path; img.file_format='PNG'; img.save()
    return img
maps={'PBR_Stone':('stone',(34,36,35),(166,158,132)),'PBR_Roof':('roof',(28,10,6),(125,48,22)),'PBR_Wood':('wood',(32,8,2),(145,63,17)),'PBR_Plaster':('plaster',(92,60,35),(220,182,119)),'PBR_Trim':('metal',(65,28,3),(245,170,38))}
for name,(kind,lo,hi) in maps.items():
    img=make(name,kind,lo,hi); m=bpy.data.materials.get(name)
    if m and m.use_nodes:
        tex=m.node_tree.nodes.get('PBR_Texture')
        if tex: tex.image=img
bpy.ops.object.select_all(action='SELECT'); bpy.context.view_layer.objects.active=bpy.context.scene.objects.get('EraBuildingKit')
bpy.ops.wm.save_as_mainfile(filepath=out.replace('.fbx','.blend'))
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=False)
print('DETAILED_TEXTURES_EXPORTED',out)
