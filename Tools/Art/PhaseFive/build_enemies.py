"""Editable first-pass enemy meshes for the approved industrial whitebox.

Run with Blender 5.2: blender --factory-startup --background --python Tools/Art/PhaseFive/build_enemies.py
Coordinates are authored in metres, Blender -Y forward and +Z up. Each rigid
animated part has a named parent pivot; no animation or gameplay code is created.
"""
from pathlib import Path
import json
import math
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import (
    reset_scene, materials, empty, box, cylinder, mesh_object, join_meshes,
    finish_asset,
)


def prism(name, outline, bottom, top, material):
    """Make a closed extrusion of a counterclockwise horizontal outline."""
    count = len(outline)
    vertices = [(x, y, bottom) for x, y in outline]
    vertices += [(x, y, top) for x, y in outline]
    faces = [tuple(reversed(range(count))), tuple(range(count, 2 * count))]
    for index in range(count):
        following = (index + 1) % count
        faces.append((index, following, following + count, index + count))
    return mesh_object(name, vertices, faces, material)


def beam(name, start, end, width, depth, material):
    start, end = Vector(start), Vector(end)
    result = box(name, (start + end) * 0.5,
                 (width, depth, (end - start).length), material, bevel=0)
    result.rotation_mode = 'QUATERNION'
    result.rotation_quaternion = (end - start).to_track_quat('Z', 'Y')
    return result


def wedge(name, width_front, width_back, front, back, bottom,
          height_front, height_back, material):
    vertices = [
        (-width_front / 2, front, bottom),
        (width_front / 2, front, bottom),
        (width_back / 2, back, bottom),
        (-width_back / 2, back, bottom),
        (-width_front / 2, front, height_front),
        (width_front / 2, front, height_front),
        (width_back / 2, back, height_back),
        (-width_back / 2, back, height_back),
    ]
    return mesh_object(name, vertices,
                       [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4),
                        (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], material)


def finish_enemy(root, name):
    report = finish_asset(root, name, 'Enemies')
    # finish_asset leaves the actual exported FBX reimported in the current scene.
    # Exercise the named pivots and restore them in memory, without resaving.
    body = bpy.data.objects[name + 'Body']
    body_matrix = body.matrix_world.copy()
    motion_checks = []
    pivots = [obj for obj in bpy.context.scene.objects
              if obj.type == 'EMPTY' and obj.name.endswith('Pivot')]
    for pivot in sorted(pivots, key=lambda item: item.name):
        moving_mesh = next(child for child in pivot.children if child.type == 'MESH')
        point = moving_mesh.data.vertices[0].co.copy()
        before = moving_mesh.matrix_world @ point
        saved_rotation = pivot.rotation_euler.copy()
        saved_location = pivot.location.copy()
        if pivot.name == 'ImpactPivot':
            pivot.location.y -= 0.08
        else:
            pivot.rotation_euler.x += math.radians(12)
        bpy.context.view_layer.update()
        after = moving_mesh.matrix_world @ point
        pivot.rotation_euler = saved_rotation
        pivot.location = saved_location
        bpy.context.view_layer.update()
        restored = moving_mesh.matrix_world @ point
        motion_checks.append({
            'pivot': pivot.name,
            'mesh': moving_mesh.name,
            'sample_vertex_displacement_metres': (after-before).length,
            'moving_part_responds': (after-before).length > 0.001,
            'neutral_pose_restored': (restored-before).length < 0.00001,
            'mesh_origin_matches_pivot': (moving_mesh.matrix_world.translation
                                          - pivot.matrix_world.translation).length < 0.00001,
            'stationary_body_unchanged': all(
                abs(body.matrix_world[row][column]-body_matrix[row][column]) < 0.00001
                for row in range(4) for column in range(4)),
        })
    report['motion_checks'] = motion_checks
    path = Path(__file__).resolve().parents[3] / 'Docs/Art/PhaseFive/Checks/Enemies' / (name + '.json')
    path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    return report


def standard_enemy():
    reset_scene()
    material = materials()
    root = empty('StandardEnemy', (0, 0, 0))
    root['design_dimensions_metres'] = '0.76 width, 0.76 length, 0.48 height'
    root['animation_notes'] = 'Rotate four named hip pivots; each whole leg remains rigid.'
    body = [
        box('BodyChassis', (0, 0, 0.285), (0.36, 0.44, 0.17),
            material['StructureDark'], bevel=0.035),
        box('UpperArmor', (0, 0.015, 0.395), (0.31, 0.36, 0.15),
            material['MetalSteel'], bevel=0.027),
        box('TopIdentificationPlate', (0, 0.005, 0.477), (0.145, 0.24, 0.006),
            material['EnemyOrange'], bevel=0),
        box('SensorHousing', (0, -0.225, 0.335), (0.27, 0.075, 0.105),
            material['StructureDark'], bevel=0.015),
        box('ForwardSensor', (0, -0.265, 0.35), (0.16, 0.008, 0.035),
            material['EnemyOrange'], bevel=0),
        box('RearHeatSink', (0, 0.237, 0.32), (0.21, 0.075, 0.08),
            material['MetalSteel'], bevel=0),
    ]
    join_meshes(body, 'StandardEnemyBody', parent=root)
    for side, side_name in [(-1, 'Left'), (1, 'Right')]:
        for direction, direction_name in [(-1, 'Front'), (1, 'Rear')]:
            hip = (side * 0.175, direction * 0.145, 0.305)
            knee = (side * 0.29, direction * 0.26, 0.205)
            ankle = (side * 0.33, direction * 0.32, 0.045)
            pivot_name = 'Leg' + direction_name + side_name + 'Pivot'
            pivot = empty(pivot_name, hip, parent=root)
            parts = [
                cylinder('HipJoint', hip, 0.067, 0.076, material['MetalSteel'],
                         vertices=8, rotation=(0, math.pi / 2, 0)),
                beam('UpperLeg', hip, knee, 0.075, 0.085, material['StructureDark']),
                beam('LowerLeg', knee, ankle, 0.057, 0.067, material['MetalSteel']),
                box('KneeArmor', knee, (0.095, 0.10, 0.092),
                    material['EnemyOrange'], bevel=0.013),
                box('ContactFoot', (ankle[0], ankle[1], 0.0225),
                    (0.10, 0.12, 0.045), material['StructureDark'], bevel=0),
            ]
            join_meshes(parts, 'Leg' + direction_name + side_name,
                        parent=pivot, origin=hip)
    finish_enemy(root, 'StandardEnemy')


def fast_enemy():
    reset_scene()
    material = materials()
    root = empty('FastEnemy', (0, 0, 0))
    root['design_dimensions_metres'] = '0.50 width, 0.80 length, 0.25 height'
    root['animation_notes'] = 'Whole-body translation and banking; split rear propulsion is static.'
    parts = [
        wedge('SkimmerHull', 0.075, 0.32, -0.40, 0.26, 0.06,
              0.10, 0.205, material['StructureDark']),
        wedge('UpperArmor', 0.045, 0.23, -0.315, 0.18, 0.125,
              0.135, 0.25, material['ArmorIvory']),
        wedge('OrangeSpine', 0.023, 0.08, -0.26, 0.175, 0.148,
              0.153, 0.254, material['EnemyOrange']),
        box('RearCentralEngine', (0, 0.265, 0.122), (0.16, 0.16, 0.13),
            material['StructureDark'], bevel=0.013),
        box('NoseSensor', (0, -0.34, 0.133), (0.065, 0.035, 0.018),
            material['EnemyOrange'], bevel=0),
    ]
    for side in (-1, 1):
        outline = [(side * x, y) for x, y in
                   [(0.11, -0.25), (0.16, -0.335), (0.245, 0.19),
                    (0.25, 0.4), (0.115, 0.4)]]
        if side < 0:
            outline.reverse()
        parts.append(prism('SkimmerRail', outline, 0.0, 0.105,
                           material['StructureDark']))
        parts.append(box('PropulsionUpperPlate', (side * 0.181, 0.25, 0.122),
                         (0.125, 0.29, 0.045), material['EnemyOrange'], bevel=0.013))
        parts.append(cylinder('RearThrusterRing', (side * 0.18, 0.384, 0.065),
                              0.047, 0.029, material['MetalSteel'], vertices=8,
                              rotation=(math.pi / 2, 0, 0)))
        parts.append(cylinder('RearThrusterCore', (side * 0.18, 0.397, 0.065),
                              0.032, 0.006, material['EnemyOrange'], vertices=8,
                              rotation=(math.pi / 2, 0, 0)))
    join_meshes(parts, 'FastEnemyBody', parent=root)
    finish_enemy(root, 'FastEnemy')


def track(name, side, material):
    profile = [(-0.42, 0.07), (-0.34, 0), (0.42, 0), (0.56, 0.09),
               (0.56, 0.23), (0.46, 0.29), (-0.32, 0.29), (-0.42, 0.20)]
    # Road-wheel caps project beyond the rubber sidewall to avoid coincident faces.
    inner, outer = sorted((side * 0.28, side * 0.51))
    vertices = [(inner, y, z) for y, z in profile]
    vertices += [(outer, y, z) for y, z in profile]
    faces = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces += [(i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8)]
    return mesh_object(name, vertices, faces, material)


def heavy_enemy():
    reset_scene()
    material = materials()
    root = empty('HeavyEnemy', (0, 0, 0))
    root['design_dimensions_metres'] = '1.04 width, 1.16 length, 0.48 height'
    root['animation_notes'] = 'ImpactPivot moves locally toward -Y then recovers; tracks are rigid.'
    body = [
        box('ArmoredChassis', (0, 0.045, 0.267), (0.76, 0.76, 0.24),
            material['StructureDark'], bevel=0.042),
        box('UpperCarapace', (0, 0.09, 0.413), (0.60, 0.53, 0.13),
            material['MetalSteel'], bevel=0.023),
        box('CenterArmorSpine', (0, 0.095, 0.482), (0.23, 0.48, 0.013),
            material['StructureDark'], bevel=0),
        box('RearIdentificationPlate', (0, 0.383, 0.402), (0.56, 0.095, 0.06),
            material['EnemyOrange'], bevel=0.008),
    ]
    for side in (-1, 1):
        body.append(track('ContinuousTrack', side, material['RubberDark']))
        body.append(box('TrackSideSkirt', (side * 0.401, 0.11, 0.301),
                        (0.23, 0.76, 0.068), material['StructureDark'], bevel=0.009))
        for y in (-0.245, 0.075, 0.385):
            body.append(cylinder('TrackRoadWheel', (side * 0.513, y, 0.138),
                                 0.102, 0.014, material['MetalSteel'], vertices=8,
                                 rotation=(0, math.pi / 2, 0)))
        for y in (-0.235, -0.035, 0.165, 0.365):
            body.append(box('TopTreadRib', (side * 0.407, y, 0.341),
                            (0.205, 0.055, 0.017), material['RubberDark'], bevel=0))
        body.append(box('ForwardSideMarker', (side * 0.264, -0.185, 0.456),
                        (0.061, 0.13, 0.023), material['EnemyOrange'], bevel=0))
    join_meshes(body, 'HeavyEnemyBody', parent=root)
    impact_pivot = empty('ImpactPivot', (0, -0.27, 0.20), parent=root)
    ram = [
        wedge('ImpactArmor', 0.74, 0.72, -0.60, -0.265, 0.045,
              0.10, 0.355, material['MetalSteel']),
        box('RamSupport', (0, -0.31, 0.224), (0.52, 0.21, 0.17),
            material['StructureDark'], bevel=0),
        box('RamTopIdentification', (0, -0.281, 0.363), (0.66, 0.075, 0.018),
            material['EnemyOrange'], bevel=0),
    ]
    # Recessed center seam splits the front impact plate into two broad shoulders.
    seam = wedge('ImpactCenterSeam', 0.065, 0.065, -0.597, -0.266, 0.05,
                 0.106, 0.362, material['StructureDark'])
    ram.append(seam)
    join_meshes(ram, 'ImpactArmorAssembly', parent=impact_pivot,
                origin=(0, -0.27, 0.20))
    finish_enemy(root, 'HeavyEnemy')


if __name__ == '__main__':
    selected = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    assets = [('StandardEnemy', standard_enemy), ('FastEnemy', fast_enemy),
              ('HeavyEnemy', heavy_enemy)]
    for name, build in assets:
        if not selected or name in selected:
            build()
