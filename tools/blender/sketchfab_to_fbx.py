"""
Reduces one scanned glTF model (a CC0 museum scan from Sketchfab) to a game prop laid out like a Poly Haven model
(ARCHITECTURE §25.3), for tools/Fetch-Sketchfab.ps1:

    <out>/<id>_<res>.fbx
    <out>/textures/<id>_diff_<res>.jpg      base colour, baked from the scan
    <out>/textures/<id>_nor_gl_<res>.jpg    tangent-space normal (OpenGL, +Y), baked from the scan
    <out>/textures/<id>_rough_<res>.jpg     only when the scan has a roughness map

The scan's chunks are joined (calibration cards and markers under MIN_PART_TRIS are dropped), turned by `rotate`
(degrees, applied in Blender's Z-up frame), scaled to `height` metres and set with its origin at the bottom centre. A
copy is decimated to `tris` triangles, unwrapped once, and the scan's relief, colour and roughness are baked onto it
with Cycles, so the reduced prop keeps the detail of the scan. Museum scans are often unlit (their colour feeds an
Emission shader): colour and roughness are baked through an emission of their source, which reads both kinds. An optional preview (front and side, solid colour) is rendered for
review.

Run headless:
    blender -b --factory-startup --python-exit-code 1 -P sketchfab_to_fbx.py -- <in.glb> <out dir> <id> <tris> <res>
        <height> <rx> <ry> <rz> [<preview.png>]
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Euler, Vector

MIN_PART_TRIS = 1000
RES = {"1k": 1024, "2k": 2048}


def args():
    a = [s.strip() for s in sys.argv[sys.argv.index("--") + 1:]]
    return {
        "src": a[0], "out": a[1], "id": a[2], "tris": int(a[3]), "res": a[4], "height": float(a[5]),
        "rotate": (float(a[6]), float(a[7]), float(a[8])), "preview": a[9] if len(a) > 9 else None,
    }


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def select_only(objs, active):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active


def import_scan(path):
    bpy.ops.import_scene.gltf(filepath=path, merge_vertices=True)   # split seam vertices would make every triangle an island
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    select_only(meshes, meshes[0])
    bpy.ops.object.convert(target="MESH")   # applies a rigged scan's armature pose before the armature goes
    for o in [o for o in bpy.context.scene.objects if o.type != "MESH"]:
        for child in o.children:
            mw = child.matrix_world.copy()
            child.parent = None
            child.matrix_world = mw
    for o in [o for o in bpy.context.scene.objects if o.type != "MESH"]:
        bpy.data.objects.remove(o, do_unlink=True)
    kept = [o for o in meshes if tri_count(o) >= MIN_PART_TRIS]
    for o in meshes:
        if o not in kept:
            print(f"  dropped part {o.name} ({tri_count(o)} tris)")
            bpy.data.objects.remove(o, do_unlink=True)
    select_only(kept, kept[0])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(kept) > 1:
        bpy.ops.object.join()
    high = bpy.context.view_layer.objects.active
    high.name = "high"
    return high


def place(obj, rotate, height):
    # on the mesh itself: the glTF importer leaves objects in quaternion mode, where rotation_euler does nothing
    obj.data.transform(Euler([math.radians(r) for r in rotate], "XYZ").to_matrix().to_4x4())
    pts = [v.co for v in obj.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    s = height / (hi.z - lo.z)
    offset = Vector(((lo.x + hi.x) * 0.5, (lo.y + hi.y) * 0.5, lo.z))
    for v in obj.data.vertices:
        v.co = (v.co - offset) * s
    obj.data.update()
    select_only([obj], obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=height * 1e-4)   # chunk borders of a split scan
    bpy.ops.object.mode_set(mode="OBJECT")


def make_low(high, tris):
    low = high.copy()
    low.data = high.data.copy()
    low.name = "low"
    bpy.context.scene.collection.objects.link(low)
    ratio = min(1.0, tris / max(1, tri_count(high)))
    if ratio < 1.0:
        mod = low.modifiers.new("decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        select_only([low], low)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    low.data.materials.clear()
    for uv in list(low.data.uv_layers):
        low.data.uv_layers.remove(uv)
    low.data.uv_layers.new(name="UVMap")
    select_only([low], low)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(80), island_margin=0.002, area_weight=0.0, scale_to_bounds=True)
    bpy.ops.uv.pack_islands(margin=0.002, rotate=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    for p in low.data.polygons:
        p.use_smooth = True
    print(f"  low: {tri_count(low)} tris (from {tri_count(high)})")
    return low


def source(mat, channel):
    """The socket feeding a material's colour ("color") or roughness ("rough"), or None."""
    for node in mat.node_tree.nodes:
        if node.type == "BSDF_PRINCIPLED":
            sock = node.inputs["Base Color" if channel == "color" else "Roughness"]
            if sock.is_linked:
                return sock.links[0].from_socket
        if channel == "color" and node.type == "EMISSION" and node.inputs["Color"].is_linked:
            return node.inputs["Color"].links[0].from_socket
    return None


def has_roughness_map(high):
    return any(m is not None and source(m, "rough") is not None for m in high.data.materials)


def emit(high, channel):
    """Rewires every material of the scan to emit its colour or roughness, so an EMIT bake reads it whatever the shader."""
    for mat in high.data.materials:
        if mat is None:
            continue
        tree = mat.node_tree
        src = source(mat, channel)
        out = next((n for n in tree.nodes if n.type == "OUTPUT_MATERIAL"), None) or tree.nodes.new("ShaderNodeOutputMaterial")
        em = tree.nodes.new("ShaderNodeEmission")
        if src is not None:
            tree.links.new(src, em.inputs["Color"])
        else:
            em.inputs["Color"].default_value = (0.5, 0.5, 0.5, 1.0) if channel == "color" else (0.6, 0.6, 0.6, 1.0)
        tree.links.new(em.outputs["Emission"], out.inputs["Surface"])


def bake(high, low, kind, size, colorspace):
    img = bpy.data.images.new(f"bake_{kind}", size, size, alpha=False, float_buffer=False)
    img.colorspace_settings.name = colorspace
    mat = bpy.data.materials.new("low_bake")
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = img
    mat.node_tree.nodes.active = node
    low.data.materials.clear()
    low.data.materials.append(mat)

    scene = bpy.context.scene
    bake_settings = scene.render.bake
    bake_settings.use_selected_to_active = True
    bake_settings.cage_extrusion = 0.015
    bake_settings.max_ray_distance = 0.05
    bake_settings.margin = 6
    bake_settings.normal_space = "TANGENT"
    bake_settings.normal_r, bake_settings.normal_g, bake_settings.normal_b = "POS_X", "POS_Y", "POS_Z"
    select_only([high, low], low)
    bpy.ops.object.bake(type="NORMAL" if kind == "nor_gl" else "EMIT")
    return img


def save_jpg(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.filepath_raw = path
    img.file_format = "JPEG"
    try:
        img.save(filepath=path, quality=90)
    except TypeError:
        img.save()
    print(f"  wrote {path}")


def export_fbx(low, path):
    select_only([low], low)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
                             path_mode="STRIP", embed_textures=False)
    print(f"  wrote {path}")


def preview(low, diff_img, path, height):
    mat = low.data.materials[0]
    nodes = mat.node_tree.nodes
    tex = next(n for n in nodes if n.type == "TEX_IMAGE")
    tex.image = diff_img
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x, scene.render.resolution_y = 512, 512
    scene.render.image_settings.file_format = "PNG"
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = max(low.dimensions) * 1.15
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    # front (looking along +Y) and side (looking along -X)
    base, ext = os.path.splitext(path)
    for name, loc, rot in (("front", (0, -10, height * 0.5), (90, 0, 0)), ("side", (10, 0, height * 0.5), (90, 0, 90))):
        cam.location = loc
        cam.rotation_euler = Euler([math.radians(r) for r in rot], "XYZ")
        scene.render.filepath = f"{base}_{name}{ext}"
        bpy.ops.render.render(write_still=True)


def main():
    a = args()
    clear_scene()
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 4
    high = import_scan(a["src"])
    place(high, a["rotate"], a["height"])
    low = make_low(high, a["tris"])
    size = RES[a["res"]]
    tex_dir = os.path.join(a["out"], "textures")
    nor = bake(high, low, "nor_gl", size, "Non-Color")   # first, while the scan keeps its own normal maps
    save_jpg(nor, os.path.join(tex_dir, f"{a['id']}_nor_gl_{a['res']}.jpg"))
    rough_map = has_roughness_map(high)
    emit(high, "color")
    diff = bake(high, low, "diff", size, "sRGB")
    save_jpg(diff, os.path.join(tex_dir, f"{a['id']}_diff_{a['res']}.jpg"))
    if rough_map:
        emit(high, "rough")
        rough = bake(high, low, "rough", size, "Non-Color")
        save_jpg(rough, os.path.join(tex_dir, f"{a['id']}_rough_{a['res']}.jpg"))
    bpy.data.objects.remove(high, do_unlink=True)
    low.data.materials.clear()
    mat = bpy.data.materials.new(a["id"])
    low.data.materials.append(mat)
    export_fbx(low, os.path.join(a["out"], f"{a['id']}_{a['res']}.fbx"))
    if a["preview"]:
        mat.node_tree.nodes.new("ShaderNodeTexImage")
        preview(low, diff, a["preview"], a["height"])


if __name__ == "__main__":
    main()
