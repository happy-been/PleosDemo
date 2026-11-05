"""Blender 2.93 batch: reduce CC0 scan meshes and export Unity-readable FBX copies."""
import bpy
import json
import sys
from pathlib import Path

root = Path(sys.argv[sys.argv.index('--') + 1])
staging = root / 'build/parking-lookdev/sources'
output = root / 'unity/InputProbe/Assets/_Parkside/Lookdev/Models'
output.mkdir(parents=True, exist_ok=True)
report = []
for asset, budget in [('covered_car', 18000), ('shrub_01', 18000), ('tree_small_02', 200000)]:
    if '--only-tree' in sys.argv and asset != 'tree_small_02':
        continue
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(staging / asset / 'source.fbx'))
    objects = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    faces = sum(len(obj.data.polygons) for obj in objects)
    ratio = min(1, budget / max(1, faces))
    materials = []
    for obj in objects:
        materials += [slot.material.name for slot in obj.material_slots if slot.material]
        if ratio < .98:
            bpy.context.view_layer.objects.active = obj
            modifier = obj.modifiers.new(name='Lookdev mesh budget', type='DECIMATE')
            modifier.ratio = ratio
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(output / (asset + '.fbx')), use_selection=True,
        apply_unit_scale=True, bake_space_transform=False, axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, path_mode='STRIP', bake_anim=False)
    report.append({'asset':asset,'source_faces':faces,'export_faces':sum(len(obj.data.polygons) for obj in objects),
        'materials': sorted(set(materials)), 'objects':[{'name':obj.name,'dimensions':list(obj.dimensions)} for obj in objects]})
    print('LOOKDEV_MODEL', asset, faces, '->', report[-1]['export_faces'], flush=True)
(root / 'build/parking-lookdev/model-report.json').write_text(json.dumps(report, indent=2))
