import bpy, os, math

OUT=os.path.abspath('Assets/CityArchitecture/Generated/EraImperialCityBlock.fbx')
os.makedirs(os.path.dirname(OUT),exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(name,color):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); return m
stone=mat('City_Stone',(.24,.27,.29)); road=mat('City_Road',(.08,.09,.10)); lot=mat('City_Lot',(.16,.13,.09)); wood=mat('City_GateWood',(.22,.09,.035))

def cube(name,loc,scale,material,bev=.08,parent=None):
    bpy.ops.mesh.primitive_cube_add(location=loc); o=bpy.context.object; o.name=name; o.scale=scale; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(material)
    if bev:
        b=o.modifiers.new('SoftEdges','BEVEL'); b.width=bev; b.segments=2
    if parent: o.parent=parent
    return o

root=bpy.data.objects.new('EraImperialCityBlock',None); bpy.context.collection.objects.link(root)
cube('Ground',(60,-.15,40),(60,.15,40),road,.02,root)
for x,z,sx,sz in [(60,0,60,1),(60,80,60,1),(0,40,1,40),(120,40,1,40)]: cube('OuterWall',(x,2.5,z),(sx,2.5,sz),stone,.12,root)
for x,z in [(60,0),(60,80),(0,40),(120,40)]:
    cube('GateTower',(x-3 if x in (60,) else x,3,z-3 if z in (40,) else z),(1.5,3,1.5),stone,.12,root)
    cube('GateTower',(x+3 if x in (60,) else x,3,z+3 if z in (40,) else z),(1.5,3,1.5),stone,.12,root)
    if z in (0,80): cube('GateLintel',(x,5,z),(4,.6,1),stone,.1,root); cube('GateDoor',(x,2.4,z+(-1.1 if z==0 else 1.1)),(2.4,2.3,.12),wood,.04,root)
    else: cube('GateLintel',(x,5,z),(1,.6,4),stone,.1,root); cube('GateDoor',(x+(-1.1 if x==0 else 1.1),2.4,z),(.12,2.3,2.4),wood,.04,root)
for x in (0,120):
    for z in (0,80):
        bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=3,depth=7,location=(x,3.5,z)); o=bpy.context.object; o.name='CornerTower'; o.data.materials.append(stone); o.parent=root
cube('NorthSouthRoad',(60,.03,40),(3,.04,34),road,.01,root); cube('EastWestRoad',(60,.04,40),(54,.04,3),road,.01,root)
for i,(x,z,sx,sz) in enumerate([(28,26,8,6),(92,26,8,6),(28,54,8,6),(92,54,8,6),(50,33,5,5),(70,33,5,5),(50,47,5,5),(70,47,5,5)]): cube('BuildingLot_%02d'%i,(x,.12,z),(sx,.12,sz),lot,.03,root)
bpy.ops.object.select_all(action='DESELECT'); root.select_set(True); bpy.context.view_layer.objects.active=root
bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.fbx','.blend'))
bpy.ops.export_scene.fbx(filepath=OUT,use_selection=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y')
print('CITY_BLOCK_EXPORTED',OUT)
