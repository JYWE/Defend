"""Check tall facilities in the front row at the actual one-cell spacing."""
import sys
from pathlib import Path
import bpy
sys.path.insert(0,str(Path(__file__).resolve().parent))
from common import ART_ROOT, PREVIEW_ROOT

source=ART_ROOT/'PhaseFiveModelOverview.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
scene=bpy.data.scenes['AdjacentOneMeterFootprints']
bpy.context.window.scene=scene
arrangement={'RapidTurret':(-1,.5,0),'HeavyTurret':(0,.5,0),'AreaTurret':(1,.5,0),
             'Reactor':(-1,-.5,0),'Relay':(0,-.5,0),'Core':(1,-.5,0)}
for obj in scene.objects:
    if obj.type=='EMPTY' and obj.parent is None:
        name=obj.name.split('.')[0]
        if name in arrangement: obj.location=arrangement[name]
scene.name='AdjacentTallFrontOneMeterFootprints'
scene.render.filepath=str(PREVIEW_ROOT/'AdjacentTallFront_GameView.png')
bpy.ops.render.render(write_still=True)
bpy.context.window.scene=bpy.data.scenes['ModelOverview']
bpy.ops.wm.save_as_mainfile(filepath=str(source))
bpy.ops.wm.open_mainfile(filepath=str(source))
print('ADJACENCY_SOURCE_REOPENED',len(bpy.data.scenes),'scenes',flush=True)
