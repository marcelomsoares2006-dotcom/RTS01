import bpy, os, math

OUT=os.path.abspath('Assets/CityArchitecture/Generated/EraBuildingKit.fbx')
os.makedirs(os.path.dirname(OUT),exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(n,c):
    m=bpy.data.materials.new(n); m.diffuse_color=(*c,1); return m
stone=mat('Building_Stone',(.32,.30,.26)); plaster=mat('Building_Plaster',(.55,.43,.30)); wood=mat('Building_Wood',(.22,.08,.03)); roof=mat('Building_Roof',(.12,.08,.05)); gold=mat('Building_Trim',(.55,.35,.08))
def cube(n,loc,sc,m,b=.08,p=None):
    bpy.ops.mesh.primitive_cube_add(location=loc); o=bpy.context.object; o.name=n; o.scale=sc; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(m)
    if b: x=o.modifiers.new('SoftEdges','BEVEL'); x.width=b; x.segments=2
    if p:o.parent=p
    return o
def roofpiece(n,loc,sc,p): return cube(n,loc,sc,roof,.06,p)
root=bpy.data.objects.new('EraBuildingKit',None); bpy.context.collection.objects.link(root)
def house(x,z):
    r=bpy.data.objects.new('House_Medieval',None); bpy.context.collection.objects.link(r); r.parent=root
    cube('HouseBody',(x,2,z),(4,2,3),plaster,.12,r); roofpiece('HouseRoof',(x,4.5,z),(4.5,.35,3.5),r); cube('HouseDoor',(x,1.3,z-3.03),(1,1.3,.12),wood,.03,r)
def barracks(x,z):
    r=bpy.data.objects.new('Barracks_Medieval',None); bpy.context.collection.objects.link(r); r.parent=root
    cube('BarracksBody',(x,2.4,z),(6,2.4,3.5),stone,.12,r); roofpiece('BarracksRoof',(x,5.1,z),(6.5,.4,4),r); cube('BarracksBanner',(x,6,z-3.7),(.4,1.5,.08),gold,.02,r)
def temple(x,z):
    r=bpy.data.objects.new('Temple_Imperial',None); bpy.context.collection.objects.link(r); r.parent=root
    cube('TempleBody',(x,3,z),(5,3,4),stone,.14,r)
    for dx in (-4,4):
        bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=.65,depth=6.5,location=(x+dx,3,z-3.5)); o=bpy.context.object; o.name='TempleColumn'; o.data.materials.append(stone); o.parent=r
    cube('TemplePediment',(x,6.7,z-3.5),(5.5,.7,.8),gold,.08,r)
def market(x,z):
    r=bpy.data.objects.new('Market_Classical',None); bpy.context.collection.objects.link(r); r.parent=root
    cube('MarketPlatform',(x,.4,z),(5,.4,4),stone,.08,r)
    for dx in (-4,4):
        for dz in (-3,3):
            bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=.35,depth=5.5,location=(x+dx,3,z+dz)); o=bpy.context.object; o.name='MarketPost'; o.data.materials.append(wood); o.parent=r
    cube('MarketAwning',(x,5.8,z),(5.5,.25,4.5),roof,.05,r)
house(-14,0); barracks(14,0); temple(-14,18); market(14,18)
bpy.ops.object.select_all(action='DESELECT'); root.select_set(True); bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.fbx','.blend'))
bpy.ops.export_scene.fbx(filepath=OUT,use_selection=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y')
# Export each building separately so Unity receives a stable prefab root per asset.
individual=os.path.join(os.path.dirname(OUT),'Buildings'); os.makedirs(individual,exist_ok=True)
for child in list(root.children):
    bpy.ops.object.select_all(action='DESELECT'); child.select_set(True)
    for descendant in child.children_recursive: descendant.select_set(True)
    bpy.context.view_layer.objects.active=child
    bpy.ops.export_scene.fbx(filepath=os.path.join(individual,child.name+'.fbx'),use_selection=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y')
print('ERA_BUILDING_KIT_EXPORTED',OUT)
