"""
GLB / glTF  ->  FBX for TownBuilder (Blender 3.6 or 4.x).

Converts a 3D model exported from an AI 3D tool (GLB) into an FBX that Unity reads without any extra package.
It also:
  * writes the model's textures as PNG/JPG files next to the FBX (Unity finds them automatically),
  * puts the model on the ground: centred on X/Y, lowest point at height 0 (handy for rendering isometric pictures),
  * uses Unity-friendly FBX settings (Y up, real scale, no extra "leaf" bones, animations kept if there are any).

HOW TO RUN
----------
A) From a terminal (Blender closed, nothing else needed):

   "C:\\Program Files\\Blender Foundation\\Blender 4.2\\blender.exe" --background --python glb_to_fbx.py -- "D:\\01-Projects\\05-Township\\Sources\\Models\\feed_mill\\feed_mill.glb"

   * Pass a .glb/.gltf file, or a folder: every .glb/.gltf inside it (and its sub-folders) is converted.
   * The FBX is saved next to each GLB with the same name (feed_mill.glb -> feed_mill.fbx).
   * Optional second path: a different output folder.

B) Inside Blender: Scripting tab -> Open -> this file -> set INPUT below -> Run Script (the play button).

Only for models of our OWN art (made from our PNGs). Don't convert models copied from other games.
"""

import os
import shutil
import sys

import bpy
from mathutils import Vector

# Used when the script runs inside Blender without command-line paths (option B).
INPUT = r"D:\01-Projects\05-Township\Sources\Models"
OUTPUT = ""  # empty = next to each GLB

# Put the model's lowest point at height 0 and centre it (set False to keep the original position).
PLACE_ON_GROUND = True


def log(message):
    print(f"[glb_to_fbx] {message}")


def clear_scene():
    """Empties the scene without resetting Blender (safe from the Scripting tab too)."""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for data in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.textures, bpy.data.actions,
                 bpy.data.armatures, bpy.data.cameras, bpy.data.lights, bpy.data.node_groups):
        for block in list(data):
            data.remove(block)


def import_gltf(path):
    bpy.ops.import_scene.gltf(filepath=path)


def place_on_ground():
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        return
    bpy.context.view_layer.update()
    corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    min_v = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    max_v = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    offset = Vector((-(min_v.x + max_v.x) / 2, -(min_v.y + max_v.y) / 2, -min_v.z))
    for obj in bpy.context.scene.objects:
        if obj.parent is None:
            obj.location += offset
    size = max_v - min_v
    log(f"model size (Blender units): {size.x:.2f} x {size.y:.2f} x {size.z:.2f}")


def write_textures(work_dir):
    """GLB textures are packed inside the file: write them out so the FBX can point at real image files
    (the FBX export then copies them next to the FBX)."""
    count = 0
    for img in bpy.data.images:
        if not img.packed_file:
            continue
        ext = ".jpg" if img.file_format == "JPEG" else ".png"
        safe = "".join(ch if ch.isalnum() or ch in "-_" else "_" for ch in img.name) or "texture"
        path = os.path.join(work_dir, safe + ext)
        with open(path, "wb") as f:
            f.write(img.packed_file.data)
        img.filepath = path
        img.unpack(method="USE_ORIGINAL")  # use the file just written, drop the packed copy
        count += 1
    log(f"textures written: {count}")


def export_fbx(fbx_path):
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=False,
        object_types={"MESH", "ARMATURE", "EMPTY"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=False,
        path_mode="COPY",       # copy the texture files next to the FBX
        embed_textures=False,
    )


def convert(glb_path, out_dir):
    name = os.path.splitext(os.path.basename(glb_path))[0]
    out_dir = out_dir or os.path.dirname(glb_path)
    os.makedirs(out_dir, exist_ok=True)
    fbx_path = os.path.join(out_dir, name + ".fbx")
    work_dir = os.path.join(out_dir, "_blender_work_" + name)
    os.makedirs(work_dir, exist_ok=True)
    log(f"{glb_path} -> {fbx_path}")
    try:
        clear_scene()
        import_gltf(glb_path)
        if PLACE_ON_GROUND:
            place_on_ground()
        write_textures(work_dir)
        export_fbx(fbx_path)
        log("done")
        return True
    except Exception as e:  # keep going with the other files
        log(f"FAILED: {e}")
        return False
    finally:
        shutil.rmtree(work_dir, ignore_errors=True)


def find_models(path):
    if os.path.isfile(path):
        return [path]
    found = []
    for root, _dirs, files in os.walk(path):
        if "_blender_work_" in root:
            continue
        for f in files:
            if f.lower().endswith((".glb", ".gltf")):
                found.append(os.path.join(root, f))
    return sorted(found)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    source = argv[0] if argv else INPUT
    out_dir = argv[1] if len(argv) > 1 else OUTPUT
    models = find_models(source)
    if not models:
        log(f"no .glb/.gltf found in {source}")
        return
    ok = sum(convert(m, out_dir) for m in models)
    log(f"converted {ok} of {len(models)}")


main()
