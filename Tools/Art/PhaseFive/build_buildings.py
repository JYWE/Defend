"""Editable first-pass industrial building assets, authored in metres.

Run: Blender --background --python Tools/Art/PhaseFive/build_buildings.py
All six assets use a bottom-centred root, -Y forward, Z up. No gameplay code.
"""
from pathlib import Path
import sys
import math
import json
import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from common import reset_scene, materials, empty, box, cylinder, mesh_object, join_meshes, finish_asset


def annulus(name, center, outer, inner, depth, material, parent=None,
            segments=16, rotation=None, start=0.0, sweep=math.tau):
    closed = abs(sweep - math.tau) < 1e-6
    count = segments if closed else segments + 1
    vertices = []
    for height, radius in ((-depth / 2, outer), (-depth / 2, inner),
                           (depth / 2, outer), (depth / 2, inner)):
        for index in range(count):
            angle = start + sweep * index / segments
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), height))
    faces = []
    for index in range(segments):
        following = (index + 1) % count
        faces += [(index, following, count * 2 + following, count * 2 + index),
                  (count + following, count + index, count * 3 + index, count * 3 + following),
                  (count * 2 + index, count * 2 + following, count * 3 + following, count * 3 + index),
                  (following, index, count + index, count + following)]
    if not closed:
        faces += [(0, count * 2, count * 3, count),
                  (count - 1, count * 2 - 1, count * 4 - 1, count * 3 - 1)]
    # Apply the intended orientation to vertex positions before parenting.
    from mathutils import Euler, Vector
    transform = Euler(rotation or (0, 0, 0)).to_matrix()
    vertices = [tuple(transform @ Vector(v) + Vector(center)) for v in vertices]
    return mesh_object(name, vertices, faces, material, parent=parent)


def chamfered_taper(name, bottom, top, height0, height1, material,
                     parent=None, center=(0.0, 0.0), cut=0.10):
    vertices = []
    for dimensions, height in ((bottom, height0), (top, height1)):
        x, y = dimensions[0] / 2, dimensions[1] / 2
        c = min(cut, min(x, y) * 0.45)
        outline = [(-x + c, -y), (x - c, -y), (x, -y + c), (x, y - c),
                   (x - c, y), (-x + c, y), (-x, y - c), (-x, -y + c)]
        vertices.extend((a + center[0], b + center[1], height) for a, b in outline)
    faces = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces.extend((i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8))
    return mesh_object(name, vertices, faces, material, parent=parent)


def merge(parts, name, root, origin=(0, 0, 0)):
    return join_meshes(parts, name, parent=root, origin=origin)


def setup(name):
    reset_scene()
    mats = materials()
    root = empty(name, (0, 0, 0))
    empty('ForwardReference', (0, -0.5, 0), root)
    root['asset_family'] = 'FriendlyBuilding'
    root['footprint_cells'] = 1
    root['forward_axis'] = '-Y'
    return root, mats


def foundation(name, root, mats, width=0.84):
    pieces = [chamfered_taper(name + '_Foundation', (width, width), (width, width), 0.0, 0.09,
                               mats['StructureDark'], root),
              chamfered_taper(name + '_SlopedArmor', (width - 0.05, width - 0.05),
                               (width - 0.18, width - 0.18), 0.09, 0.25,
                               mats['ArmorIvory'], root)]
    for x in (-width * 0.35, width * 0.35):
        pieces.append(box(name + '_AnchorFoot', (x, -width * 0.37, 0.06),
                          (0.14, 0.16, 0.10), mats['MetalSteel'], bevel=0.015, parent=root))
    pieces.append(box(name + '_FrontSocket', (0, -width / 2 + 0.045, 0.15),
                      (0.20, 0.055, 0.07), mats['StructureDark'], bevel=0.008, parent=root))
    pieces.append(box(name + '_StatusLight', (0, -width / 2 + 0.012, 0.15),
                      (0.10, 0.012, 0.025), mats['EnergyCyan'], bevel=0.002, parent=root))
    return pieces


def reactor():
    name = 'Reactor'
    root, m = setup(name)
    p = foundation(name, root, m)
    p += [cylinder('Reactor_ChamberSeat', (0, 0, 0.285), 0.225, 0.09, m['StructureDark'], parent=root),
          cylinder('Reactor_EnergyChamber', (0, 0, 0.635), 0.145, 0.64, m['EnergyCyan'], parent=root),
          annulus('Reactor_LowerCollar', (0, 0, 0.35), 0.22, 0.15, 0.08, m['MetalSteel'], root),
          annulus('Reactor_UpperCollar', (0, 0, 0.935), 0.22, 0.15, 0.07, m['MetalSteel'], root),
          chamfered_taper('Reactor_TopArmor', (0.46, 0.46), (0.37, 0.37), 0.965, 1.065,
                           m['ArmorIvory'], root, cut=0.09),
          cylinder('Reactor_TopVent', (0, 0, 1.08), 0.10, 0.06, m['StructureDark'], vertices=8, parent=root)]
    for x in (-0.155, 0.155):
        for y in (-0.115, 0.115):
            p.append(box('Reactor_ChamberBrace', (x, y, 0.64), (0.045, 0.055, 0.62),
                         m['MetalSteel'], bevel=0.006, parent=root))
    for x in (-0.31, 0.31):
        p += [box('Reactor_RadiatorHousing', (x, 0.025, 0.495), (0.17, 0.52, 0.52),
                  m['ArmorIvory'], bevel=0.025, parent=root),
              box('Reactor_RadiatorInset', (x, -0.241, 0.49), (0.12, 0.024, 0.34),
                  m['StructureDark'], bevel=0.005, parent=root),
              box('Reactor_RadiatorTop', (x, 0.025, 0.763), (0.115, 0.37, 0.018),
                  m['StructureDark'], bevel=0.006, parent=root)]
        for z in (0.385, 0.475, 0.565):
            p.append(box('Reactor_RadiatorFin', (x, -0.261, z), (0.11, 0.024, 0.03),
                         m['MetalSteel'], bevel=0.003, parent=root))
        for y in (-0.07, 0.025, 0.12):
            p.append(box('Reactor_TopCoolingFin', (x, y, 0.776), (0.10, 0.02, 0.018),
                         m['MetalSteel'], bevel=0.002, parent=root))
    merge(p, 'Reactor_StaticStructure', root)
    finish_asset(root, name, 'Buildings')


def relay():
    name = 'Relay'
    root, m = setup(name)
    p = foundation(name, root, m, 0.72)
    p += [cylinder('Relay_MastFoot', (0, 0, 0.275), 0.19, 0.07, m['MetalSteel'], vertices=8, parent=root),
          chamfered_taper('Relay_TaperedMast', (0.29, 0.29), (0.145, 0.145), 0.28, 0.79,
                           m['ArmorIvory'], root, cut=0.05),
          box('Relay_MastInset', (0, -0.119, 0.51), (0.065, 0.05, 0.32), m['StructureDark'], bevel=0.006, parent=root),
          box('Relay_EnergyConduit', (0, -0.15, 0.44), (0.026, 0.016, 0.14), m['EnergyCyan'], bevel=0.002, parent=root),
          cylinder('Relay_AerialStem', (0, 0, 0.92), 0.065, 0.34, m['MetalSteel'], vertices=8, parent=root),
          annulus('Relay_OpenRing', (0, 0, 0.88), 0.335, 0.258, 0.075, m['ArmorIvory'], root),
          annulus('Relay_RingEnergy', (0, 0, 0.922), 0.315, 0.270, 0.012, m['EnergyCyan'], root),
          chamfered_taper('Relay_AerialTip', (0.12, 0.12), (0.04, 0.04), 1.065, 1.165,
                           m['ArmorIvory'], root, cut=0.025)]
    for angle in (math.pi / 6, 5 * math.pi / 6, 3 * math.pi / 2):
        x, y = 0.18 * math.cos(angle), 0.18 * math.sin(angle)
        arm = box('Relay_RingSupport', (x, y, 0.83), (0.27, 0.045, 0.055),
                  m['StructureDark'], bevel=0.008, parent=root)
        arm.rotation_euler.z = angle
        p.append(arm)
    merge(p, 'Relay_StaticStructure', root)
    finish_asset(root, name, 'Buildings')


def turret_mount(name, root, m, wide=False):
    p = foundation(name, root, m, 0.88 if wide else 0.84)
    p.append(cylinder(name + '_FixedBearing', (0, 0, 0.26), 0.26, 0.04,
                      m['StructureDark'], vertices=16, parent=root))
    merge(p, name + '_StaticStructure', root)
    yaw = empty('TurretYaw', (0, 0, 0.30), root)
    yaw['motion'] = 'Rotate local Z; converted to Unity local Y'
    return yaw


def rapid_turret():
    name = 'RapidTurret'
    root, m = setup(name)
    yaw = turret_mount(name, root, m)
    p = [cylinder('RapidTurret_RotatingBearing', (0, 0, 0.31), 0.235, 0.07, m['MetalSteel'], parent=yaw),
         box('RapidTurret_RearDrive', (0, 0.135, 0.46), (0.36, 0.24, 0.21), m['StructureDark'], bevel=0.03, parent=yaw),
         box('RapidTurret_RearArmor', (0, 0.19, 0.57), (0.39, 0.20, 0.09), m['ArmorIvory'], bevel=0.02, parent=yaw)]
    for x in (-0.215, 0.215):
        p += [box('RapidTurret_ShoulderArmor', (x, 0.025, 0.485), (0.13, 0.36, 0.265),
                  m['ArmorIvory'], bevel=0.025, parent=yaw),
              cylinder('RapidTurret_Trunnion', (x * 1.30, 0.025, 0.47), 0.053, 0.016,
                       m['MetalSteel'], vertices=8, rotation=(0, math.pi / 2, 0), parent=yaw)]
    merge(p, 'RapidTurret_Turntable', yaw, (0, 0, 0.30))
    recoil = empty('BarrelRecoil', (0, -0.015, 0.52), yaw)
    recoil['motion'] = 'Translate local +Y for recoil; converted to Unity local -Z'
    p = [box('RapidTurret_Breech', (0, -0.015, 0.52), (0.27, 0.25, 0.16),
             m['StructureDark'], bevel=0.02, parent=recoil)]
    for index, x in enumerate((-0.095, 0.095)):
        p += [cylinder('RapidTurret_BarrelTube', (x, -0.28, 0.52), 0.036, 0.39,
                       m['MetalSteel'], vertices=10, rotation=(math.pi / 2, 0, 0), parent=recoil),
              cylinder('RapidTurret_BarrelCollar', (x, -0.145, 0.52), 0.055, 0.10,
                       m['StructureDark'], vertices=10, rotation=(math.pi / 2, 0, 0), parent=recoil),
              annulus('RapidTurret_OpenMuzzle', (x, -0.474, 0.52), 0.053, 0.031, 0.065,
                      m['MetalSteel'], recoil, 10, rotation=(math.pi / 2, 0, 0)),
              cylinder('RapidTurret_RecessedBore', (x, -0.481, 0.52), 0.031, 0.007,
                       m['StructureDark'], vertices=10, rotation=(math.pi / 2, 0, 0), parent=recoil)]
        empty('MuzzleLeft' if index == 0 else 'MuzzleRight', (x, -0.508, 0.52), recoil)
    merge(p, 'RapidTurret_Barrels', recoil, (0, -0.015, 0.52))
    finish_asset(root, name, 'Buildings')


def heavy_turret():
    name = 'HeavyTurret'
    root, m = setup(name)
    yaw = turret_mount(name, root, m, True)
    p = [cylinder('HeavyTurret_RotatingBearing', (0, 0, 0.32), 0.25, 0.08, m['MetalSteel'], parent=yaw),
         box('HeavyTurret_DriveBlock', (0, 0.12, 0.47), (0.42, 0.29, 0.21), m['StructureDark'], bevel=0.025, parent=yaw)]
    for x in (-0.22, 0.22):
        p += [box('HeavyTurret_ShoulderArmor', (x, 0.055, 0.545), (0.15, 0.37, 0.32),
                  m['ArmorIvory'], bevel=0.03, parent=yaw),
              cylinder('HeavyTurret_Trunnion', (x * 1.40, 0.02, 0.56), 0.073, 0.02,
                       m['MetalSteel'], vertices=10, rotation=(0, math.pi / 2, 0), parent=yaw),
              cylinder('HeavyTurret_RecoilRail', (x * 0.66, -0.005, 0.44), 0.024, 0.36,
                       m['MetalSteel'], vertices=8, rotation=(math.pi / 2, 0, 0), parent=yaw)]
    p.append(box('HeavyTurret_RearCap', (0, 0.238, 0.615), (0.39, 0.07, 0.15), m['ArmorIvory'], bevel=0.018, parent=yaw))
    merge(p, 'HeavyTurret_Turntable', yaw, (0, 0, 0.30))
    recoil = empty('BarrelRecoil', (0, 0.015, 0.565), yaw)
    recoil['motion'] = 'Translate local +Y for recoil; converted to Unity local -Z'
    p = [box('HeavyTurret_Breech', (0, 0.035, 0.565), (0.27, 0.28, 0.195),
             m['StructureDark'], bevel=0.02, parent=recoil),
         cylinder('HeavyTurret_BarrelSleeve', (0, -0.13, 0.565), 0.105, 0.20,
                  m['MetalSteel'], vertices=12, rotation=(math.pi / 2, 0, 0), parent=recoil),
         cylinder('HeavyTurret_BarrelTube', (0, -0.32, 0.565), 0.075, 0.30,
                  m['StructureDark'], vertices=12, rotation=(math.pi / 2, 0, 0), parent=recoil),
         annulus('HeavyTurret_OpenMuzzle', (0, -0.474, 0.565), 0.108, 0.064, 0.09,
                 m['MetalSteel'], recoil, 12, rotation=(math.pi / 2, 0, 0)),
         cylinder('HeavyTurret_RecessedBore', (0, -0.483, 0.565), 0.064, 0.01,
                  m['StructureDark'], vertices=12, rotation=(math.pi / 2, 0, 0), parent=recoil),
         box('HeavyTurret_BreechTop', (0, 0.04, 0.676), (0.22, 0.20, 0.035),
             m['ArmorIvory'], bevel=0.008, parent=recoil)]
    empty('Muzzle', (0, -0.521, 0.565), recoil)
    merge(p, 'HeavyTurret_Barrel', recoil, (0, 0.015, 0.565))
    finish_asset(root, name, 'Buildings')


def area_turret():
    name = 'AreaTurret'
    root, m = setup(name)
    yaw = turret_mount(name, root, m)
    p = [cylinder('AreaTurret_RotatingBearing', (0, 0, 0.32), 0.255, 0.09, m['MetalSteel'], parent=yaw),
         cylinder('AreaTurret_EmitterDrum', (0, 0, 0.405), 0.255, 0.15, m['StructureDark'], parent=yaw)]
    merge(p, 'AreaTurret_Turntable', yaw, (0, 0, 0.30))
    recoil = empty('BarrelRecoil', (0, 0, 0.49), yaw)
    recoil['motion'] = 'Reserved emitter pulse assembly; no animation authored'
    p = [annulus('AreaTurret_EmitterSeat', (0, 0, 0.48), 0.34, 0.19, 0.08, m['StructureDark'], recoil),
         annulus('AreaTurret_InnerEnergyRing', (0, 0, 0.532), 0.278, 0.244, 0.018, m['EnergyCyan'], recoil),
         cylinder('AreaTurret_CentralEmitter', (0, 0, 0.53), 0.165, 0.07, m['MetalSteel'], vertices=12, parent=recoil),
         cylinder('AreaTurret_EmitterLens', (0, 0, 0.57), 0.105, 0.016, m['EnergyCyan'], vertices=16, parent=recoil)]
    for index in range(6):
        a = index * math.tau / 6
        p.append(annulus('AreaTurret_SegmentedArmor', (0, 0, 0.50), 0.405, 0.298, 0.16,
                         m['ArmorIvory'], recoil, segments=2, start=a + 0.11, sweep=math.tau / 6 - 0.22))
        x, y = 0.344 * math.cos(a), 0.344 * math.sin(a)
        strip = box('AreaTurret_RadialConduit', (x, y, 0.50), (0.105, 0.024, 0.032),
                    m['EnergyCyan'], bevel=0.004, parent=recoil)
        strip.rotation_euler.z = a
        p.append(strip)
    empty('Muzzle', (0, 0, 0.59), recoil)
    merge(p, 'AreaTurret_Emitter', recoil, (0, 0, 0.49))
    finish_asset(root, name, 'Buildings')


def core():
    name = 'Core'
    root, m = setup(name)
    p = foundation(name, root, m, 0.86)
    p += [box('Core_ProtectedChamber', (0, 0, 0.41), (0.61, 0.61, 0.40),
              m['StructureDark'], bevel=0.035, parent=root),
          box('Core_TopArmor', (0, 0, 0.58), (0.58, 0.58, 0.12),
              m['ArmorIvory'], bevel=0.025, parent=root),
          box('Core_TopInset', (0, 0, 0.65), (0.40, 0.40, 0.025),
              m['StructureDark'], bevel=0.017, parent=root),
          box('Core_EnergyVault', (0, 0, 0.668), (0.28, 0.28, 0.018),
              m['EnergyCyan'], bevel=0.02, parent=root),
          box('Core_CentralSeal', (0, 0, 0.69), (0.13, 0.13, 0.05),
              m['MetalSteel'], bevel=0.015, parent=root)]
    for x in (-0.31, 0.31):
        for y in (-0.31, 0.31):
            p += [box('Core_CornerShield', (x, y, 0.50), (0.175, 0.175, 0.54),
                      m['ArmorIvory'], bevel=0.026, parent=root),
                  box('Core_ShieldTopSlot', (x, y, 0.779), (0.095, 0.095, 0.018),
                      m['StructureDark'], bevel=0.008, parent=root),
                  box('Core_ShieldEnergy', (x, y, 0.791), (0.055, 0.055, 0.01),
                      m['EnergyCyan'], bevel=0.005, parent=root)]
    for x in (-0.322, 0.322):
        p.append(box('Core_SideArmor', (x, 0, 0.405), (0.025, 0.31, 0.22),
                     m['ArmorIvory'], bevel=0.008, parent=root))
    for y in (-0.322, 0.322):
        p += [box('Core_FrontArmor', (0, y, 0.405), (0.31, 0.025, 0.22),
                  m['ArmorIvory'], bevel=0.008, parent=root),
              box('Core_FrontEnergy', (0, y * 1.05, 0.445), (0.15, 0.012, 0.025),
                  m['EnergyCyan'], bevel=0.003, parent=root)]
    merge(p, 'Core_StaticStructure', root)
    finish_asset(root, name, 'Buildings')


def audit_buildings():
    """Exercise saved pivots in memory without altering delivered source files."""
    project = Path(__file__).resolve().parents[3]
    results = []
    names = ('Reactor', 'Relay', 'RapidTurret', 'HeavyTurret', 'AreaTurret', 'Core')
    for name in names:
        source = project / 'ArtSource/PhaseFive/Buildings' / (name + '.blend')
        bpy.ops.wm.open_mainfile(filepath=str(source))
        root = bpy.data.objects[name]
        bpy.context.view_layer.update()
        static = bpy.data.objects[name + '_StaticStructure']
        points = [static.matrix_world @ v.co for v in static.data.vertices]
        bounds = [[min(p[i] for p in points), max(p[i] for p in points)] for i in range(3)]
        forward = bpy.data.objects['ForwardReference']
        entry = {
            'asset': name,
            'root_bottom_center': all(abs(value) < 1e-6 for value in root.location),
            'root_unit_scale': all(abs(value - 1.0) < 1e-6 for value in root.scale),
            'static_bounds': bounds,
            'static_footprint_within_088_meters': all(bounds[i][0] >= -0.44001 and bounds[i][1] <= 0.44001 for i in (0, 1)),
            'forward_reference_world': list(forward.matrix_world.translation),
            'forward_reference_correct': (forward.matrix_world.translation - Vector((0, -0.5, 0))).length < 1e-6,
            'source_mesh_count': sum(o.type == 'MESH' for o in root.children_recursive),
        }
        if 'Turret' in name:
            yaw = bpy.data.objects['TurretYaw']
            recoil = bpy.data.objects['BarrelRecoil']
            muzzles = [o for o in root.children_recursive if o.type == 'EMPTY' and o.name.startswith('Muzzle')]
            moving = [o for o in yaw.children_recursive if o.type == 'MESH']
            points = [o.matrix_world @ v.co for o in moving for v in o.data.vertices]
            entry['full_rotation_maximum_horizontal_radius_meters'] = max(math.hypot(p.x, p.y) for p in points)
            entry['moving_assembly_attached_to_yaw'] = len(moving) == 2 and recoil.parent == yaw
            entry['muzzle_count'] = len(muzzles)
            before = [o.matrix_world.translation.copy() for o in muzzles]
            yaw.rotation_euler.z = math.pi / 2
            bpy.context.view_layer.update()
            actual = [o.matrix_world.translation.copy() for o in muzzles]
            expected = [Vector((-p.y, p.x, p.z)) for p in before]
            entry['quarter_turn_muzzle_error_meters'] = max((a - b).length for a, b in zip(actual, expected))
            entry['quarter_turn_follows_yaw'] = entry['quarter_turn_muzzle_error_meters'] < 1e-5
            yaw.rotation_euler.z = 0
            recoil.location.y += 0.04
            bpy.context.view_layer.update()
            offsets = [o.matrix_world.translation - p for o, p in zip(muzzles, before)]
            entry['recoil_test_muzzle_offsets_meters'] = [list(o) for o in offsets]
            entry['recoil_moves_muzzle_four_centimeters_backward'] = all(
                abs(o.x) < 1e-5 and abs(o.y - 0.04) < 1e-5 and abs(o.z) < 1e-5 for o in offsets)
        results.append(entry)
    target = project / 'Docs/Art/PhaseFive/Checks/Buildings/BuildingMotionAndFootprint.json'
    target.write_text(json.dumps({'method': 'Saved Blender source reopened; direct dimensions and temporary in-memory pivot transforms; no source modification',
                                   'assets': results}, indent=2), encoding='utf-8')
    print('BUILDING_MOTION_AUDIT_COMPLETE', target, flush=True)


if __name__ == '__main__':
    choices = {'Reactor': reactor, 'Relay': relay, 'RapidTurret': rapid_turret,
               'HeavyTurret': heavy_turret, 'AreaTurret': area_turret, 'Core': core}
    requested = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else list(choices)
    if requested == ['--audit']:
        audit_buildings()
    else:
        for asset_name in requested:
            choices[asset_name]()
