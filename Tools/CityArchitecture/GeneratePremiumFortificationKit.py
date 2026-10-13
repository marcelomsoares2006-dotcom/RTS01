import bpy, os, math
from mathutils import Vector

OUT = os.path.abspath('Assets/CityArchitecture/Generated/PremiumFortificationKit.fbx')
os.makedirs(os.path.dirname(OUT),exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def mat(name, color, rough=.78):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.roughness=rough; return m
stone=mat('Fortification_Stone',(0.25,.28,.30)); stone2=mat('Fortification_Stone_Variation',(.38,.39,.37)); wood=mat('Gate_Wood',(.18,.07,.025)); metal=mat('Gate_Metal',(.08,.10,.12),.38)

def cube(name, loc, scale, material, bevel=.06):
    bpy.ops.mesh.primitive_cube_add(location=loc); o=bpy.context.object; o.name=name; o.scale=scale; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    o.data.materials.append(material)
    if bevel:
        mod=o.modifiers.new('Edge softening','BEVEL'); mod.width=bevel; mod.segments=2
    return o

def stone_wall(root, x=0, z=0, length=8, height=3.2, yaw=0):
    body=cube('WallBody',(x,height/2,z),(length/2,.5,height/2),stone,.10); body.rotation_euler[2]=yaw; body.parent=root
    for row in range(4):
        for col in range(8):
            px=-length/2+.5+col*(length-1)/(7); pz=-.52
            if row%2: px += .28
            block=cube(f'Stone_{row}_{col}',(x+px*math.cos(yaw)-pz*math.sin(yaw),.55+row*.68,z+px*math.sin(yaw)+pz*math.cos(yaw)),(.42,.28,.06),stone2,.035); block.parent=root
    for i in range(7):
        px=-length/2+.55+i*(length-1.1)/6
        mer=cube('Merlon_'+str(i),(x+px*math.cos(yaw),height+.35,z+px*math.sin(yaw)),(.32,.35,.38),stone,.06); mer.parent=root
    return root

def tower(root, x=0,z=0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=1.35,depth=5.2,location=(x,2.6,z)); o=bpy.context.object; o.name='CornerTower'; o.data.materials.append(stone); o.parent=root
    for i in range(8):
        a=i*math.tau/8; m=cube('TowerMerlon', (x+1.05*math.cos(a),5.45,z+1.05*math.sin(a)),(.28,.35,.28),stone2,.05); m.parent=root

def gate(root):
    cube('GateLeft',(-2.2,2.6,0),(1.0,2.6,1.25),stone,.12).parent=root; cube('GateRight',(2.2,2.6,0),(1.0,2.6,1.25),stone,.12).parent=root
    cube('GateArch',(0,4.9,0),(3.2,.65,1.25),stone,.12).parent=root
    cube('WoodenGate',(0,2.3,-1.32),(1.65,2.1,.12),wood,.04).parent=root
    for x in (-1.1,-.55,0,.55,1.1): cube('GateBand',(x,2.3,-1.48),(.08,2.15,.06),metal,.02).parent=root

root=bpy.data.objects.new('PremiumFortificationKit',None); bpy.context.collection.objects.link(root)
wall=bpy.data.objects.new('WallStraight_Premium',None); bpy.context.collection.objects.link(wall); stone_wall(wall,0,0,8,3.2); wall.parent=root
corner=bpy.data.objects.new('WallCorner_Premium',None); bpy.context.collection.objects.link(corner); stone_wall(corner,0,0,6,3.2,0); stone_wall(corner,0,0,6,3.2,math.pi/2); tower(corner,0,0); corner.parent=root
gate_root=bpy.data.objects.new('Gatehouse_Premium',None); bpy.context.collection.objects.link(gate_root); gate(gate_root); gate_root.parent=root
bpy.ops.wm.save_as_mainfile(filepath=OUT.replace('.fbx','.blend'))
bpy.ops.object.select_all(action='SELECT'); bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=False)
print('PREMIUM_FORTIFICATION_EXPORTED', OUT)
