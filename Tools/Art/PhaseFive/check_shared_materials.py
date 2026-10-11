"""Reopen the delivered shared material library and inspect direct properties."""
import sys
import json
from pathlib import Path
import bpy
sys.path.insert(0,str(Path(__file__).resolve().parent))
from common import ART_ROOT,CHECK_ROOT,PALETTE

bpy.ops.wm.open_mainfile(filepath=str(ART_ROOT/'Shared/PhaseFiveSharedMaterials.blend'))
names=sorted(m.name for m in bpy.data.materials if m.name.startswith('PhaseFive_'))
expected=sorted('PhaseFive_'+name for name in PALETTE)
assert names==expected
assert all(m.use_nodes and any(n.type=='BSDF_PRINCIPLED' for n in m.node_tree.nodes) for m in bpy.data.materials if m.name in expected)
assert all(m.use_fake_user for m in bpy.data.materials if m.name in expected)
external_files=[image.filepath for image in bpy.data.images if image.source=='FILE' and image.filepath and image.packed_file is None]
assert not external_files
report={'source_reopened':True,'blender_version':bpy.app.version_string,'shared_material_count':len(names),
        'material_names':names,'all_editable_principled_materials':True,'fake_users_preserve_library':True,
        'external_texture_files':external_files,
        'image_datablocks':[{'name':image.name,'source':image.source} for image in bpy.data.images],
        'render_assets_are_not_part_of_library':len(bpy.data.objects)==0}
(CHECK_ROOT/'SharedMaterialSource.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report),flush=True)
