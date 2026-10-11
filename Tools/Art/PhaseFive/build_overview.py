"""Render the real delivered meshes together and at one-cell adjacency."""
import sys
import math
import json
from pathlib import Path
import bpy
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import PROJECT, ART_ROOT, PREVIEW_ROOT, PALETTE, reset_scene, materials, box, aim

NAMES = ['Reactor','Relay','RapidTurret','HeavyTurret','AreaTurret','Core','StandardEnemy','FastEnemy','HeavyEnemy','RockLowA','RockLowB','IndustrialFloor','EntranceMarker']
LABELS = ['反应堆','中继器','连射炮','重击炮','范围炮','核心','普通四足机械','快速滑行体','履带重装','低矮岩块 A','低矮岩块 B','工业地面','入口标识']

def load_asset(index, location):
    name = NAMES[index]
    category = 'Buildings' if index < 6 else 'Enemies' if index < 9 else 'Environment'
    with bpy.data.libraries.load(str(ART_ROOT/category/(name+'.blend')), link=False) as (source, target):
        target.objects = [n for n in source.objects if not n.startswith('Studio') and n != 'PreviewCamera']
    for obj in target.objects:
        bpy.context.scene.collection.objects.link(obj)
        if obj.type == 'MESH':
            for slot in obj.material_slots:
                original_name = slot.material.name.split('.')[0]
                if original_name in bpy.data.materials:
                    slot.material = bpy.data.materials[original_name]
    root = next(obj for obj in target.objects if obj.name == name or obj.name.startswith(name+'.'))
    # Find root by parent relation as some mesh names begin with the asset name.
    root = next(obj for obj in target.objects if obj.type=='EMPTY' and obj.parent is None)
    root.location = location
    return root

def text_object(body, location, size, material):
    curve = bpy.data.curves.new(body,'FONT')
    curve.body = body
    curve.font = font
    curve.size = size
    curve.align_x = 'CENTER'
    obj = bpy.data.objects.new(body,curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    curve.materials.append(material)
    return obj

def lighting():
    scene = bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=48
    scene.cycles.use_denoising=True
    scene.cycles.device='CPU'
    scene.render.threads_mode='FIXED'
    scene.render.threads=12
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'
    scene.world.color=(.18,.18,.18)
    for name,loc,energy,size,color in [
        ('Key',(2,-4,9),1000,7,(.88,.94,1)),
        ('Fill',(-5,0,6),500,6,(.70,.82,1)),
        ('Rim',(1,6,8),1200,5,(1,.88,.72)),
    ]:
        data=bpy.data.lights.new(name,'AREA')
        data.energy=energy; data.shape='DISK'; data.size=size; data.color=color
        obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj)
        obj.location=loc; aim(obj,(0,0,.3))
    data=bpy.data.cameras.new('OverviewCamera')
    camera=bpy.data.objects.new('OverviewCamera',data)
    scene.collection.objects.link(camera); scene.camera=camera
    data.type='ORTHO'
    return camera

reset_scene()
shared=materials()
library_dir=ART_ROOT/'Shared'
library_dir.mkdir(parents=True,exist_ok=True)
for material in shared.values(): material.use_fake_user=True
bpy.ops.wm.save_as_mainfile(filepath=str(library_dir/'PhaseFiveSharedMaterials.blend'))
(library_dir/'Palette.json').write_text(json.dumps({
    'colors_are_linear_rgb':True,
    'materials': [{'name':'PhaseFive_'+name,'base_color':values[0],'metallic':values[1], 'roughness':values[2],'emission_strength':values[3]} for name,values in PALETTE.items()]
},indent=2),encoding='utf-8')
font=bpy.data.fonts.load(str(PROJECT/'Assets/Defend/Content/Art/Fonts/NotoSansCJKsc-Regular.otf'))
font.pack()
scene=bpy.context.scene
scene.name='ModelOverview'
for index in range(13):
    if index<6:
        location=((index%3-1)*1.85, 2.7-(index//3)*1.85, .045)
    elif index<9:
        location=((index-7)*1.85,-1.0,.045)
    else:
        location=((index-10.5)*1.45,-2.88,.045)
    root=load_asset(index,location)
    size=1.30 if index>=9 else 1.42
    box('DisplayPlinth_'+NAMES[index],(location[0],location[1],.02),(size,size,.04),shared['StructureDark'],bevel=.01)
    text_object(LABELS[index],(location[0],location[1]-size/2-.23,.012),.18,shared['ArmorIvory'])
text_object('工业科幻白盒 · 首批模型',(0,4.03,.012),.32,shared['ArmorIvory'])
text_object('BLENDER  实际网格渲染   /   70° 正交视角',(0,3.7,.012),.14,shared['ArmorIvory'])
box('StudioFloor',(0,0,-.08),(200,200,.1),shared['StructureDark'],bevel=0)
camera=lighting()
target=Vector((0,0,.2))
camera.location=target+Vector((0,-math.cos(math.radians(70))*14,math.sin(math.radians(70))*14))
aim(camera,target); camera.data.ortho_scale=8.75
scene.render.resolution_x=1600; scene.render.resolution_y=1800
scene.render.resolution_percentage=100
scene.render.filepath=str(PREVIEW_ROOT/'ModelOverview.png')
bpy.ops.render.render(write_still=True)

# A second scene checks neighboring one-cell footprints, without touching a game map.
overview=scene
adjacent=bpy.data.scenes.new('AdjacentOneMeterFootprints')
bpy.context.window.scene=adjacent
adjacent.world=overview.world
for index in range(6):
    location=((index%3-1)*1.0,.5-(index//3)*1.0,0)
    load_asset(index,location)
    box('OneMeterFootprint_'+NAMES[index],(location[0],location[1],-.025),(.995,.995,.045),shared['GroundConcrete'],bevel=.003)
    for xoffset in (-.47,.47):
        for yoffset in (-.47,.47):
            box('FootprintCorner',(location[0]+xoffset,location[1]+yoffset,.004),(.045,.045,.008),shared['EnergyCyan'],bevel=0)
box('AdjacencyStudioFloor',(0,0,-.08),(200,200,.1),shared['StructureDark'],bevel=0)
camera=lighting()
target=Vector((0,0,.36)); camera.location=target+Vector((0,-2.736,7.518))
aim(camera,target); camera.data.ortho_scale=4.25
adjacent.render.resolution_x=1500; adjacent.render.resolution_y=1100
adjacent.render.resolution_percentage=100
adjacent.render.filepath=str(PREVIEW_ROOT/'AdjacentFootprints_GameView.png')
bpy.ops.render.render(write_still=True)
bpy.context.window.scene=overview
bpy.ops.wm.save_as_mainfile(filepath=str(ART_ROOT/'PhaseFiveModelOverview.blend'))
bpy.ops.wm.open_mainfile(filepath=str(ART_ROOT/'PhaseFiveModelOverview.blend'))
print('OVERVIEW_REOPENED',len(bpy.data.scenes),'scenes',flush=True)
