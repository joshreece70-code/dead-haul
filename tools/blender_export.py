# Dead Haul weapon exporter. Run inside Blender: Scripting tab > Open this file > Run Script.
#
# Save your .blend inside this repository first (for example blender/weapons.blend).
# Name each weapon object exactly: pistol, smg, shotgun, spas, uzis, rifle, ak, sniper, minigun, katana, saber.
# For saber, model the hilt only: the game draws the glowing blade itself.
# Modelling rules the game expects:
#   - one object per weapon; several materials are fine, and each part keeps its material's base colour
#   - for a textured weapon, use a single mesh with a single material
#   - barrel points along +Y, top of the gun along +Z
#   - object origin at the grip, where the hand holds it
#   - real-world size in metres (a pistol is about 0.2 long)
# The script writes public/models/<name>.glb for each weapon it finds and updates public/models/manifest.json.

import bpy, json, os

NAMES = ["pistol", "smg", "shotgun", "spas", "uzis", "rifle", "ak", "sniper", "minigun", "katana", "saber"]

def repo_root():
    d = os.path.dirname(bpy.data.filepath)
    if not d:
        raise RuntimeError("Save the .blend file inside the repository first.")
    while d and not os.path.isdir(os.path.join(d, "public")):
        parent = os.path.dirname(d)
        if parent == d:
            raise RuntimeError("Could not find the repository's public folder above this .blend file.")
        d = parent
    return d

out = os.path.join(repo_root(), "public", "models")
os.makedirs(out, exist_ok=True)
done = []
for name in NAMES:
    obj = bpy.data.objects.get(name)
    if obj is None or obj.type != "MESH":
        continue
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    where = obj.location.copy()
    obj.location = (0, 0, 0)  # weapons may be laid out side by side in the scene; export each from the origin
    try:
        bpy.ops.export_scene.gltf(
            filepath=os.path.join(out, name + ".glb"),
            export_format="GLB",
            use_selection=True,
            export_apply=True,
            export_yup=True,
        )
    finally:
        obj.location = where
    done.append(name)

with open(os.path.join(out, "manifest.json"), "w") as f:
    json.dump({"weapons": done}, f)
print("Exported:", ", ".join(done) if done else "nothing (no objects with the expected names)")
