"""Shared editable hard-surface asset production; run inside Blender 5.2.

World-space constructors retain world placement when parenting. Sources are
outside Assets so Unity only imports the deliberate FBX exports.
"""
import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Vector

PROJECT = Path(__file__).resolve().parents[3]
ART_ROOT = PROJECT / 'ArtSource/PhaseFive'
EXPORT_ROOT = PROJECT / 'Assets/Defend/Content/Art/PhaseFive/Models'
PREVIEW_ROOT = PROJECT / 'Docs/Art/PhaseFive/Previews'
CHECK_ROOT = PROJECT / 'Docs/Art/PhaseFive/Checks'
PALETTE = {
    'ArmorIvory': ((0.62, 0.69, 0.72, 1), 0.22, 0.42, 0),
    'StructureDark': ((0.045, 0.061, 0.077, 1), 0.42, 0.48, 0),
    'MetalSteel': ((0.20, 0.26, 0.31, 1), 0.65, 0.32, 0),
    'EnergyCyan': ((0.015, 0.62, 0.76, 1), 0.18, 0.25, 0.7),
    'EnemyOrange': ((0.83, 0.185, 0.037, 1), 0.22, 0.46, 0),
    'RubberDark': ((0.022, 0.028, 0.035, 1), 0.02, 0.78, 0),
    'GroundConcrete': ((0.24, 0.28, 0.31, 1), 0.08, 0.78, 0),
    'RockSlate': ((0.12, 0.15, 0.18, 1), 0.05, 0.86, 0),
}

def reset_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.collections):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    bpy.context.preferences.filepaths.save_version = 0

def materials():
    result = {}
    for key, (color, metallic, roughness, emission) in PALETTE.items():
        name = 'PhaseFive_' + key
        material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        material.use_nodes = True
        material.diffuse_color = color
        material.metallic = metallic
        material.roughness = roughness
        shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
        shader.inputs['Base Color'].default_value = color
        shader.inputs['Metallic'].default_value = metallic
        shader.inputs['Roughness'].default_value = roughness
        shader.inputs['Emission Color'].default_value = color
        shader.inputs['Emission Strength'].default_value = emission
        result[key] = material
    return result

def parent_world(obj, parent):
    if parent is not None:
        bpy.context.view_layer.update()
        matrix = obj.matrix_world.copy()
        obj.parent = parent
        obj.matrix_world = matrix
        bpy.context.view_layer.update()
    return obj

def empty(name, location=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.empty_display_type = 'PLAIN_AXES'
    obj.empty_display_size = 0.08
    return parent_world(obj, parent)

def activate(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

def finish_mesh(obj, name, material, parent=None, bevel=0):
    obj.name = name
    activate(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if material:
        obj.data.materials.append(material)
    if bevel > 0:
        modifier = obj.modifiers.new('SingleSegmentBevel', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 1
        modifier.affect = 'EDGES'
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    # Flat broad planes keep the whitebox facets and bevels legible.
    for face in obj.data.polygons:
        face.use_smooth = False
    return parent_world(obj, parent)

def box(name, location, dimensions, material, bevel=0.02, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.dimensions = dimensions
    return finish_mesh(obj, name, material, parent, min(bevel, min(dimensions) * 0.24))

def cylinder(name, location, radius, depth, material, vertices=12, rotation=None, parent=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                      end_fill_type='NGON', location=location,
                                      rotation=rotation or (0, 0, 0))
    return finish_mesh(bpy.context.object, name, material, parent)

def mesh_object(name, vertices, faces, material, parent=None):
    mesh = bpy.data.meshes.new(name + 'Mesh')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    mesh.validate(verbose=False)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish_mesh(obj, name, material, parent)

def join_meshes(objects, name, parent=None, origin=(0, 0, 0)):
    objects = [obj for obj in objects if obj is not None and obj.type == 'MESH']
    if not objects:
        raise ValueError('Cannot join an empty mesh list: ' + name)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    saved_cursor = bpy.context.scene.cursor.location.copy()
    bpy.context.scene.cursor.location = origin
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.context.scene.cursor.location = saved_cursor
    # Remove unused/duplicate material slots while retaining polygon assignments.
    old_materials = list(obj.data.materials)
    unique = []
    remap = {}
    for index, material in enumerate(old_materials):
        if material not in unique:
            unique.append(material)
        remap[index] = unique.index(material)
    assignments = [remap[p.material_index] for p in obj.data.polygons]
    obj.data.materials.clear()
    for material in unique:
        obj.data.materials.append(material)
    for polygon, material_index in zip(obj.data.polygons, assignments):
        polygon.material_index = material_index
    return parent_world(obj, parent)

def descendants(root):
    return [root] + list(root.children_recursive)

def inspect_objects(objects):
    meshes = [obj for obj in objects if obj.type == 'MESH']
    bpy.context.view_layer.update()
    points = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    minimum = [min(p[i] for p in points) for i in range(3)]
    maximum = [max(p[i] for p in points) for i in range(3)]
    records = []
    for obj in meshes:
        mesh = obj.data
        mesh.calc_loop_triangles()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        record = {
            'name': obj.name, 'triangles': len(mesh.loop_triangles),
            'vertices': len(mesh.vertices), 'material_slots': len(mesh.materials),
            'materials': [m.name if m else None for m in mesh.materials],
            'boundary_edges': sum(e.is_boundary for e in bm.edges),
            'non_manifold_edges': sum(not e.is_manifold for e in bm.edges),
            'degenerate_faces': sum(f.calc_area() < 1e-10 for f in bm.faces),
            'signed_volume': bm.calc_volume(signed=True),
            'non_finite_normals': sum(not all(math.isfinite(v) for v in p.normal) for p in mesh.polygons),
            'negative_determinant': obj.matrix_world.determinant() < 0,
            'world_origin': list(obj.matrix_world.translation),
            'local_scale': list(obj.scale),
        }
        bm.free()
        records.append(record)
    return {
        'bounds_min': minimum, 'bounds_max': maximum,
        'dimensions': [maximum[i]-minimum[i] for i in range(3)],
        'triangles': sum(m['triangles'] for m in records),
        'mesh_count': len(records),
        'material_names': sorted({m.name for obj in meshes for m in obj.data.materials if m}),
        'material_slot_count': sum(m['material_slots'] for m in records),
        'meshes': records,
        'pivots': [{'name': obj.name, 'parent': obj.parent.name if obj.parent else None,
                    'world_position': list(obj.matrix_world.translation),
                    'local_position': list(obj.location), 'local_scale': list(obj.scale)}
                   for obj in objects if obj.type == 'EMPTY'],
    }

def aim(obj, target):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z', 'Y').to_euler()

def studio(root, stats):
    scene = bpy.context.scene
    collection = bpy.data.collections.new('PresentationStudio_ExcludedFromExport')
    scene.collection.children.link(collection)
    def move(obj):
        for source in list(obj.users_collection):
            source.objects.unlink(obj)
        collection.objects.link(obj)
        return obj
    material = bpy.data.materials.new('StudioBackdrop_ExcludedFromExport')
    material.diffuse_color = (0.095, 0.125, 0.15, 1)
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = material.diffuse_color
    shader.inputs['Roughness'].default_value = 0.86
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.007))
    floor = move(bpy.context.object)
    floor.name = 'StudioFloor_ExcludedFromExport'
    floor.data.materials.append(material)
    for name, position, energy, size, color in [
        ('StudioKey', (3,-4,7), 550, 4, (0.88,0.94,1)),
        ('StudioFill', (-4,-1,4), 300, 5, (0.62,0.78,1)),
        ('StudioRim', (1,4,6), 750, 3, (1,0.86,0.7)),
    ]:
        data = bpy.data.lights.new(name, 'AREA')
        data.energy = energy
        data.shape = 'DISK'
        data.size = size
        data.color = color
        obj = bpy.data.objects.new(name, data)
        collection.objects.link(obj)
        obj.location = position
        aim(obj, (0,0,0.4))
    camera_data = bpy.data.cameras.new('PreviewCamera')
    camera = bpy.data.objects.new('PreviewCamera', camera_data)
    collection.objects.link(camera)
    camera_data.type = 'ORTHO'
    camera_data.lens = 50
    scene.camera = camera
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.cycles.device = 'CPU'
    scene.render.threads_mode = 'FIXED'
    scene.render.threads = 6
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.world.color = (0.18, 0.18, 0.18)
    scene.view_settings.view_transform = 'AgX'
    return camera

def set_camera(camera, stats, elevation):
    size = max(stats['dimensions'])
    height = stats['dimensions'][2]
    target = Vector((0,0,height*0.43))
    angle = math.radians(elevation)
    azimuth = math.radians(24 if elevation < 60 else 12)
    direction = Vector((math.sin(azimuth)*math.cos(angle), -math.cos(azimuth)*math.cos(angle), math.sin(angle)))
    camera.location = target + direction * 7
    aim(camera, target)
    camera.data.ortho_scale = max(1.05, size*1.64)

def finish_asset(root, name, category):
    source_dir = ART_ROOT / category
    export_dir = EXPORT_ROOT / category
    check_dir = CHECK_ROOT / category
    for directory in (source_dir, export_dir, check_dir, PREVIEW_ROOT):
        directory.mkdir(parents=True, exist_ok=True)
    if not any(obj.name == 'ForwardReference' for obj in root.children):
        empty('ForwardReference', (0,-0.5,0), root)
    root['asset_name'] = name
    root['unit_meters'] = 1.0
    root['forward_axis'] = '-Y'
    root['root_pivot'] = 'bottom_center'
    before = inspect_objects(descendants(root))
    camera = studio(root, before)
    set_camera(camera, before, 70)
    source_path = source_dir / (name + '.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(source_path))
    # Actually reopen the saved editable source, then inspect and export it.
    bpy.ops.wm.open_mainfile(filepath=str(source_path))
    root = bpy.data.objects[name]
    reopened = inspect_objects(descendants(root))
    for angle, suffix in ((70, 'GameView'), (36, 'CloseView')):
        set_camera(bpy.context.scene.camera, reopened, angle)
        bpy.context.scene.render.filepath = str(PREVIEW_ROOT / f'{name}_{suffix}.png')
        bpy.ops.render.render(write_still=True)
    set_camera(bpy.context.scene.camera, reopened, 70)
    bpy.ops.wm.save_as_mainfile(filepath=str(source_path))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in descendants(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    export_path = export_dir / (name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(export_path), use_selection=True,
        object_types={'EMPTY','MESH'}, axis_forward='-Z', axis_up='Y',
        global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        use_space_transform=True, bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
        add_leaf_bones=False, bake_anim=False, use_custom_props=True)
    reset_scene()
    bpy.ops.import_scene.fbx(filepath=str(export_path), use_anim=False)
    exported = inspect_objects(list(bpy.context.scene.objects))
    bounds_delta = max(abs(reopened[key][i]-exported[key][i]) for key in ('bounds_min','bounds_max') for i in range(3))
    report = {
        'asset': name, 'category': category, 'blender_version': bpy.app.version_string,
        'source': str(source_path.relative_to(PROJECT)), 'export': str(export_path.relative_to(PROJECT)),
        'render_views_degrees': [70,36], 'source_before_save': before,
        'source_reopened': reopened, 'fbx_reimported': exported,
        'checks': {
            'source_reopened': True, 'export_reimported': True,
            'save_reopen_dimensions_equal': before['dimensions'] == reopened['dimensions'],
            'export_bounds_maximum_difference': bounds_delta,
            'export_bounds_match': bounds_delta < 0.0001,
            'triangles_match': reopened['triangles'] == exported['triangles'],
            'mesh_count_match': reopened['mesh_count'] == exported['mesh_count'],
            'material_names_match': reopened['material_names'] == exported['material_names'],
            'grounded': abs(reopened['bounds_min'][2]) < 0.001,
            'positive_mesh_transforms': all(not m['negative_determinant'] for m in reopened['meshes']),
            'valid_surface_normals': all(m['non_finite_normals'] == 0 and m['degenerate_faces'] == 0 and m['signed_volume'] > 0 for m in reopened['meshes']),
        },
    }
    (check_dir/(name+'.json')).write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
    print('ASSET_COMPLETE', name, reopened['triangles'], 'triangles', reopened['mesh_count'], 'meshes', flush=True)
    return report
