"""Build the four editable Phase Five environment assets in local Blender.

Run: blender --background --python Tools/Art/PhaseFive/build_environment.py
Optional single-asset run: append -- --asset IndustrialFloor
All dimensions are metres; Blender -Y is forward and Z is up.
"""
from pathlib import Path
import argparse
import sys

import bpy

SCRIPT_DIRECTORY = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIRECTORY))
from common import (reset_scene, materials, empty, box, cylinder,
                    mesh_object, join_meshes, finish_asset)


def polygon_prism(name, outline, bottom, top, material, parent=None):
    """Closed planar polygon extrusion with explicit outward face winding."""
    area = sum(outline[i][0] * outline[(i + 1) % len(outline)][1]
               - outline[(i + 1) % len(outline)][0] * outline[i][1]
               for i in range(len(outline)))
    if area < 0:
        outline = list(reversed(outline))
    count = len(outline)
    vertices = [(x, y, z) for z in (bottom, top) for x, y in outline]
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count)
              for i in range(count)]
    return mesh_object(name, vertices, faces, material, parent=parent)


def fractured_rock(name, outline, shoulder, crown, crown_center, material):
    """A hand-authored closed low-poly rock with broad geological planes."""
    count = len(outline)
    vertices = [(x, y, 0.0) for x, y in outline] + shoulder + crown + [crown_center]
    # The asymmetric silhouette still has an exact bounding-box bottom centre.
    center_x = (min(v[0] for v in vertices) + max(v[0] for v in vertices)) * .5
    center_y = (min(v[1] for v in vertices) + max(v[1] for v in vertices)) * .5
    vertices = [(x - center_x, y - center_y, z) for x, y, z in vertices]
    faces = [tuple(reversed(range(count)))]
    for level in range(2):
        for i in range(count):
            lower = level * count
            upper = (level + 1) * count
            faces.append((lower + i, lower + (i + 1) % count,
                          upper + (i + 1) % count, upper + i))
    for i in range(count):
        faces.append((count * 2 + i, count * 2 + (i + 1) % count, count * 3))
    return mesh_object(name, vertices, faces, material)


def build_rock_low_a():
    reset_scene()
    palette = materials()
    root = empty('RockLowA')
    outline = [(-.44, -.18), (-.28, -.35), (.16, -.32), (.43, -.12),
               (.38, .20), (.13, .33), (-.23, .29), (-.43, .10)]
    shoulder = [(-.40, -.16, .15), (-.25, -.29, .18), (.16, -.27, .15), (.37, -.11, .14),
                (.34, .17, .13), (.10, .28, .18), (-.24, .22, .20), (-.39, .08, .17)]
    crown = [(-.30, -.09, .33), (-.17, -.21, .34), (.12, -.20, .28), (.24, -.08, .24),
             (.19, .14, .27), (.03, .19, .38), (-.18, .15, .42), (-.31, .03, .39)]
    rock = fractured_rock('RockLowA_FracturedSlate', outline, shoulder, crown,
                          (-.05, -.01, .37), palette['RockSlate'])
    join_meshes([rock], 'RockLowA_Static', parent=root)
    finish_asset(root, 'RockLowA', 'Environment')


def build_rock_low_b():
    reset_scene()
    palette = materials()
    root = empty('RockLowB')
    outline = [(-.45, -.12), (-.23, -.30), (.13, -.29), (.45, -.15),
               (.47, .10), (.21, .27), (-.13, .29), (-.40, .16)]
    shoulder = [(-.40, -.11, .11), (-.22, -.26, .13), (.14, -.24, .11), (.40, -.13, .10),
                (.41, .08, .12), (.19, .23, .12), (-.11, .24, .15), (-.36, .14, .13)]
    crown = [(-.27, -.07, .22), (-.13, -.18, .23), (.12, -.17, .22), (.28, -.07, .17),
             (.30, .05, .18), (.16, .15, .23), (-.09, .16, .28), (-.24, .09, .27)]
    rock = fractured_rock('RockLowB_BroadSlate', outline, shoulder, crown,
                          (-.03, -.01, .26), palette['RockSlate'])
    join_meshes([rock], 'RockLowB_Static', parent=root)
    finish_asset(root, 'RockLowB', 'Environment')


def build_industrial_floor():
    reset_scene()
    palette = materials()
    root = empty('IndustrialFloor')
    pieces = []
    pieces.append(box('IndustrialFloor_LowerSupport', (0, 0, .016), (1, 1, .032),
                      palette['StructureDark'], bevel=.003))
    pieces.append(box('IndustrialFloor_ConcreteSurface', (0, 0, .055), (.996, .996, .046),
                      palette['GroundConcrete'], bevel=.005))
    pieces.append(box('IndustrialFloor_MaintenanceRecess', (.32, .34, .079), (.235, .125, .006),
                      palette['StructureDark'], bevel=.003))
    pieces.append(box('IndustrialFloor_MaintenanceCover', (.32, .34, .082), (.221, .111, .006),
                      palette['MetalSteel'], bevel=.003))
    # Three short recessed-looking ventilation marks keep the remaining top clear.
    for index in range(3):
        pieces.append(box(f'IndustrialFloor_CoverSlot{index + 1}',
                          (.277 + index * .043, .34, .0855), (.019, .060, .001),
                          palette['StructureDark'], bevel=0))
    for x in (-.451, .451):
        for y in (-.451, .451):
            pieces.append(cylinder(f'IndustrialFloor_Anchor_{x}_{y}', (x, y, .0785),
                                   .013, .003, palette['MetalSteel'], vertices=8))
    join_meshes(pieces, 'IndustrialFloor_Static', parent=root)
    finish_asset(root, 'IndustrialFloor', 'Environment')


def build_entrance_marker():
    reset_scene()
    palette = materials()
    root = empty('EntranceMarker')
    pieces = []
    footprint = [(-.36, -.30), (.36, -.30), (.42, -.24), (.42, .24),
                 (.36, .30), (-.36, .30), (-.42, .24), (-.42, -.24)]
    pieces.append(polygon_prism('EntranceMarker_Base', footprint, 0, .052,
                                palette['StructureDark']))
    pieces.append(box('EntranceMarker_UpperPlate', (0, 0, .061), (.76, .51, .018),
                      palette['MetalSteel'], bevel=.016))
    pieces.append(box('EntranceMarker_ArrowBed', (0, 0, .072), (.45, .48, .006),
                      palette['StructureDark'], bevel=.010))
    # The arrow is an actual closed mesh, visible from the steep game camera.
    arrow = [(-.065, .18), (.065, .18), (.065, -.035), (.17, -.035),
             (0, -.224), (-.17, -.035), (-.065, -.035)]
    pieces.append(polygon_prism('EntranceMarker_ForwardArrow', arrow, .075, .079,
                                palette['ArmorIvory']))
    for side in (-1, 1):
        pieces.append(box(f'EntranceMarker_OrangeWarningRail_{side}',
                          (side * .305, 0, .078), (.085, .435, .016),
                          palette['EnemyOrange'], bevel=.007))
        for y in (-.14, 0, .14):
            # Three dark breaks make each orange rail a restrained hazard stripe.
            pieces.append(box(f'EntranceMarker_WarningBreak_{side}_{y}',
                              (side * .305, y, .0865), (.086, .027, .003),
                              palette['StructureDark'], bevel=.001))
    join_meshes(pieces, 'EntranceMarker_Static', parent=root)
    finish_asset(root, 'EntranceMarker', 'Environment')


ASSETS = {'RockLowA': build_rock_low_a, 'RockLowB': build_rock_low_b,
          'IndustrialFloor': build_industrial_floor, 'EntranceMarker': build_entrance_marker}
if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--asset', choices=ASSETS)
    arguments = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    for name, build in ASSETS.items():
        if arguments.asset is None or arguments.asset == name:
            print(f'PHASE_FIVE_ENVIRONMENT_BEGIN {name}', flush=True)
            build()
            print(f'PHASE_FIVE_ENVIRONMENT_COMPLETE {name}', flush=True)
