# Dead Haul weapon models.
#
# In Blender:  Scripting tab > Open this file > Run Script. It builds all the weapons as editable mesh
#              objects in a "DeadHaulWeapons" collection. Edit them, then run tools/blender_export.py.
# No Blender:  python3 tools/blender_build_weapons.py   writes public/models/*.glb directly from the same designs.
#
# Units are metres. Blender axes: +Y is the barrel direction, +Z is up, the origin is where the hand grips.

import json, math, os, struct, sys

MATS = {  # name: (sRGB hex, metallic, roughness)
    "steel":  (0x3a3d42, 0.85, 0.38),
    "dark":   (0x1b1c1f, 0.60, 0.45),
    "poly":   (0x202226, 0.00, 0.62),
    "tan":    (0x8a7a58, 0.00, 0.70),
    "olive":  (0x4a5238, 0.00, 0.70),
    "wood":   (0x6e4a28, 0.00, 0.55),
    "rubber": (0x101011, 0.00, 0.90),
    "brass":  (0xb08a3a, 0.90, 0.35),
    "red":    (0x9a2a20, 0.30, 0.50),
    "mag":    (0x2c2e33, 0.40, 0.50),
    "blade":  (0xc9ced4, 0.90, 0.25),
}

def B(mat, sx, sy, sz, x, y, z, rx=0):
    return ("box", mat, (sx, sy, sz), (x, y, z), rx)

def C(mat, r, length, x, y, z, seg=10, r2=None, rx=0):
    return ("cyl", mat, (r, r if r2 is None else r2, length, seg), (x, y, z), rx)

def R(mat, r_in, r_out, thick, x, y, z, seg=24):
    """Flat ring facing along the barrel (an aperture sight)."""
    return ("ring", mat, (r_in, r_out, thick, seg), (x, y, z), 0)

def guard(y0, y1, z, mat="poly"):
    """Trigger guard: a bottom strap and a front strap."""
    return [B(mat, .010, y1 - y0, .005, 0, (y0 + y1) / 2, z), B(mat, .010, .005, -z - .012, 0, y1, (z - .012) / 2)]

def pistol():
    p = [
        B("steel", .030, .190, .034, 0, .075, .038),
        B("poly", .028, .150, .024, 0, .055, .010),
        B("poly", .022, .055, .012, 0, .118, -.006),
        B("poly", .030, .046, .104, 0, -.013, -.052, -14),
        B("mag", .032, .052, .010, 0, -.027, -.108, -14),
        B("steel", .006, .007, .020, 0, .022, -.016, 16),
        C("dark", .0085, .012, 0, .176, .038, 8),
        B("dark", .016, .036, .002, .008, .060, .0555),
        B("dark", .020, .008, .008, 0, -.008, .060),
        B("dark", .005, .008, .008, 0, .160, .060),
        B("dark", .010, .012, .014, 0, -.024, .046),
    ]
    p += guard(.000, .055, -.040)
    p += [B("dark", .0305, .003, .026, 0, -.004 - i * .007, .038) for i in range(5)]
    return p

def smg():
    p = [
        B("steel", .044, .300, .066, 0, .100, .022),
        B("dark", .020, .240, .008, 0, .100, .059),
        C("dark", .020, .090, 0, .295, .030, 10),
        C("steel", .010, .120, 0, .390, .030, 8),
        C("dark", .014, .030, 0, .455, .030, 8),
        B("poly", .036, .052, .050, 0, .110, -.030),
        B("mag", .026, .038, .200, 0, .110, -.120),
        B("poly", .032, .044, .100, 0, -.030, -.052, -16),
        B("steel", .006, .007, .020, 0, .010, -.018, 16),
        C("steel", .006, .230, -.013, -.165, .040, 6),
        C("steel", .006, .230, .013, -.165, .040, 6),
        B("rubber", .032, .018, .100, 0, -.285, .010),
        B("dark", .012, .020, .012, -.028, .160, .040),
        B("dark", .002, .050, .020, .0225, .090, .030),
        B("dark", .020, .012, .022, 0, .000, .0735),
        B("dark", .007, .012, .022, 0, .225, .0735),
    ]
    return p + guard(-.008, .050, -.040)

def shotgun():
    p = [
        B("steel", .044, .260, .060, 0, .030, .020),
        C("steel", .014, .580, 0, .450, .040, 10),
        C("dark", .013, .440, 0, .380, .008, 10),
        C("steel", .015, .020, 0, .610, .008, 10),
        B("steel", .020, .012, .056, 0, .560, .024),
        C("wood", .025, .160, 0, .340, .006, 8),
        B("wood", .036, .110, .050, 0, -.145, -.004, 12),
        B("wood", .038, .200, .086, 0, -.285, -.034, 10),
        B("rubber", .040, .016, .102, 0, -.390, -.052, 10),
        B("steel", .006, .007, .020, 0, .000, -.020, 16),
        B("dark", .030, .090, .002, 0, .030, -.011),
        B("dark", .002, .060, .020, .0225, .050, .030),
        C("brass", .004, .008, 0, .730, .060, 6, rx=90),
    ]
    return p + guard(-.020, .045, -.042, "steel")

def rifle():
    p = [
        B("dark", .044, .200, .050, 0, .060, .035),
        B("poly", .040, .170, .045, 0, .045, -.008),
        B("poly", .036, .060, .050, 0, .110, -.040),
        B("mag", .026, .058, .170, 0, .118, -.118, 10),
        C("tan", .030, .280, 0, .300, .035, 8),
        B("dark", .020, .460, .008, 0, .190, .064),
        C("steel", .009, .200, 0, .540, .035, 8),
        B("dark", .024, .030, .040, 0, .455, .040),
        C("dark", .013, .050, 0, .665, .035, 8),
        B("dark", .008, .012, .040, 0, .455, .080),
        B("dark", .004, .012, .040, -.014, .455, .082),
        B("dark", .004, .012, .040, .014, .455, .082),
        C("dark", .016, .200, 0, -.140, .030, 8),
        B("tan", .040, .140, .080, 0, -.200, .012),
        B("rubber", .042, .020, .110, 0, -.280, .000),
        B("tan", .030, .042, .095, 0, -.045, -.068, -18),
        B("steel", .006, .007, .020, 0, .005, -.020, 16),
        B("dark", .030, .020, .010, 0, -.045, .062),
        B("steel", .002, .070, .025, .0225, .070, .035),
        B("dark", .008, .008, .024, 0, -.040, .078),
        R("dark", .0077, .0133, .0056, 0, -.040, .100),
    ]
    return p + guard(-.020, .050, -.036)

def minigun():
    p = [
        B("steel", .090, .260, .100, 0, -.020, 0),
        C("dark", .050, .080, 0, .150, 0, 12),
        C("dark", .048, .030, 0, .300, 0, 12),
        C("dark", .048, .030, 0, .580, 0, 12),
        B("poly", .030, .045, .100, 0, -.080, -.092, -12),
        B("dark", .012, .012, .040, 0, -.060, .068),
        B("dark", .012, .012, .040, 0, .060, .068),
        B("dark", .020, .140, .012, 0, .000, .080),
        B("olive", .070, .100, .120, .085, -.020, -.030),
        B("brass", .030, .050, .030, .050, .030, .030),
    ]
    for i in range(6):
        a = i * math.pi / 3
        p.append(C("steel", .011, .500, math.cos(a) * .032, .400, math.sin(a) * .032, 6))
    return p

def spas():
    p = [
        B("dark", .046, .280, .064, 0, .030, .022),
        C("steel", .014, .500, 0, .400, .044, 10),
        C("dark", .013, .470, 0, .390, .010, 10),
        B("dark", .034, .300, .020, 0, .330, .064),
        B("poly", .056, .190, .052, 0, .310, .006),
        B("dark", .020, .012, .050, 0, .560, .030),
        C("dark", .016, .030, 0, .660, .044, 10),
        B("poly", .032, .046, .100, 0, -.050, -.052, -16),
        B("steel", .006, .007, .020, 0, .000, -.020, 16),
        B("steel", .010, .300, .010, -.018, -.250, .062),
        B("steel", .010, .300, .010, .018, -.250, .062),
        B("rubber", .050, .016, .070, 0, -.405, .040),
        B("steel", .012, .020, .050, 0, -.395, .030),
        B("dark", .030, .090, .002, 0, .040, -.011),
        B("dark", .002, .060, .020, .0235, .060, .032),
        B("dark", .008, .012, .014, 0, .640, .066),
        B("dark", .016, .012, .012, 0, -.080, .066),
    ]
    p += [B("dark", .036, .004, .022, 0, .22 + i * .03, .064) for i in range(8)]
    return p + guard(-.020, .045, -.040)

def uzis():
    p = [
        B("steel", .042, .240, .060, 0, .060, .022),
        B("dark", .034, .200, .010, 0, .060, .056),
        C("dark", .016, .040, 0, .200, .026, 8),
        C("steel", .009, .070, 0, .250, .026, 8),
        B("poly", .034, .050, .110, 0, .010, -.055, -6),
        B("mag", .024, .034, .120, 0, .014, -.150, -6),
        B("poly", .038, .090, .040, 0, .110, -.006),
        B("steel", .006, .007, .020, 0, .045, -.018, 16),
        B("dark", .014, .018, .014, 0, .090, .066),
        B("steel", .010, .160, .010, -.016, -.110, .040),
        B("steel", .010, .160, .010, .016, -.110, .040),
        B("steel", .044, .012, .050, 0, -.190, .030),
        B("dark", .020, .012, .016, 0, -.040, .067),
        B("dark", .008, .012, .016, 0, .170, .067),
        B("dark", .002, .050, .018, .0215, .070, .030),
    ]
    return p + guard(.030, .085, -.036, "steel")

def ak():
    p = [
        B("steel", .040, .260, .050, 0, .040, .025),
        B("dark", .036, .200, .016, 0, .030, .058),
        B("dark", .020, .030, .014, 0, .140, .069),
        B("wood", .042, .170, .040, 0, .255, .020),
        B("wood", .030, .150, .022, 0, .255, .056),
        C("steel", .009, .300, 0, .480, .030, 8),
        C("steel", .008, .160, 0, .410, .056, 8),
        B("dark", .022, .030, .040, 0, .490, .040),
        B("dark", .014, .020, .050, 0, .600, .055),
        C("dark", .012, .040, 0, .645, .030, 8),
        B("mag", .026, .055, .070, 0, .100, -.035, 8),
        B("mag", .026, .055, .070, 0, .118, -.098, 22),
        B("mag", .026, .055, .060, 0, .148, -.152, 36),
        B("wood", .030, .040, .090, 0, -.050, -.045, -18),
        B("steel", .006, .007, .020, 0, .000, -.018, 16),
        B("wood", .036, .240, .060, 0, -.215, .005, 8),
        B("steel", .038, .014, .090, 0, -.340, -.012, 8),
        B("dark", .020, .014, .012, .028, .070, .045),
        B("dark", .002, .070, .012, .021, .000, .030),
    ]
    return p + guard(-.025, .045, -.036, "steel")

def sniper():
    p = [
        B("olive", .045, .260, .110, 0, -.300, -.015, 6),
        B("olive", .036, .140, .030, 0, -.270, .055),
        B("olive", .040, .180, .050, 0, -.120, .005),
        B("rubber", .047, .020, .120, 0, -.435, -.030, 6),
        B("olive", .036, .060, .100, 0, -.060, -.060, -20),
        B("olive", .050, .420, .050, 0, .260, .000),
        B("steel", .040, .240, .045, 0, .050, .040),
        C("steel", .012, .620, 0, .600, .045, 10),
        C("dark", .018, .070, 0, .930, .045, 8),
        B("dark", .050, .012, .012, .035, -.030, .050),
        B("dark", .018, .018, .018, .062, -.030, .040),
        B("mag", .030, .070, .060, 0, .070, -.035),
        B("steel", .006, .007, .020, 0, -.005, -.020, 16),
        C("dark", .017, .300, 0, .060, .105, 10),
        C("dark", .026, .080, 0, .240, .105, 10),
        C("dark", .022, .060, 0, -.110, .105, 10),
        B("dark", .018, .020, .030, 0, .060, .130),
        B("dark", .030, .020, .018, .022, .060, .105),
        B("dark", .020, .020, .030, 0, -.010, .078),
        B("dark", .020, .020, .030, 0, .130, .078),
        B("dark", .008, .160, .008, -.028, .360, -.030),
        B("dark", .008, .160, .008, .028, .360, -.030),
    ]
    return p + guard(-.030, .040, -.040, "steel")

def katana():
    return [
        C("rubber", .016, .260, 0, -.040, 0, 8),
        B("brass", .034, .030, .012, 0, -.100, 0),
        B("brass", .034, .030, .012, 0, -.020, 0),
        C("brass", .018, .020, 0, -.178, 0, 8),
        C("dark", .045, .008, 0, .095, 0, 10),
        B("brass", .012, .030, .034, 0, .115, 0),
        B("blade", .006, .260, .032, 0, .260, .000),
        B("blade", .006, .260, .030, 0, .512, .008, 3),
        B("blade", .006, .240, .026, 0, .752, .026, 6),
        B("blade", .006, .070, .018, 0, .895, .046, 12),
    ]

def saber():
    # The hilt only. The glowing blade is drawn by the game itself.
    p = [
        C("steel", .018, .260, 0, -.030, 0, 10),
        C("dark", .024, .030, 0, .105, 0, 10),
        C("dark", .022, .025, 0, -.170, 0, 10),
        B("red", .010, .030, .008, .018, .040, 0),
        B("brass", .008, .050, .010, -.018, .020, 0),
    ]
    p += [C("dark", .021, .020, 0, y, 0, 10) for y in (-.120, -.080, -.040, .000)]
    return p

WEAPONS = {"pistol": pistol, "smg": smg, "shotgun": shotgun, "spas": spas, "uzis": uzis, "rifle": rifle, "ak": ak, "sniper": sniper, "minigun": minigun, "katana": katana, "saber": saber}

# ---------------------------------------------------------------- geometry

def part_faces(part):
    """Return (material, verts, faces) with faces as lists of vertex indices, wound to face outward."""
    kind, mat, dims, loc, rx = part
    verts, faces = [], []
    if kind == "ring":
        r_in, r_out, thick, seg = dims
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ca, sa = math.cos(a), math.sin(a)
            for r, yy in ((r_in, -thick / 2), (r_out, -thick / 2), (r_out, thick / 2), (r_in, thick / 2)):
                verts.append((r * ca + loc[0], yy + loc[1], r * sa + loc[2]))
        out = []
        for i in range(seg):
            j = (i + 1) % seg
            v = lambda n, k: n * 4 + k
            a = 2 * math.pi * (i + .5) / seg
            radial = (math.cos(a), 0, math.sin(a))
            for f, want in (([v(i, 1), v(j, 1), v(j, 2), v(i, 2)], radial),
                            ([v(i, 0), v(i, 3), v(j, 3), v(j, 0)], tuple(-c for c in radial)),
                            ([v(i, 0), v(j, 0), v(j, 1), v(i, 1)], (0, -1, 0)),
                            ([v(i, 3), v(i, 2), v(j, 2), v(j, 3)], (0, 1, 0))):
                n = face_normal(verts, f)
                if n[0] * want[0] + n[1] * want[1] + n[2] * want[2] < 0:
                    f = f[::-1]
                out.append(f)
        return mat, verts, out
    if kind == "box":
        hx, hy, hz = dims[0] / 2, dims[1] / 2, dims[2] / 2
        for sz in (-1, 1):
            for sy in (-1, 1):
                for sx in (-1, 1):
                    verts.append((sx * hx, sy * hy, sz * hz))
        faces = [[0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4], [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5]]
    else:
        r1, r2, length, seg = dims
        for i in range(seg):
            a = 2 * math.pi * i / seg
            verts.append((r1 * math.cos(a), -length / 2, r1 * math.sin(a)))
        for i in range(seg):
            a = 2 * math.pi * i / seg
            verts.append((r2 * math.cos(a), length / 2, r2 * math.sin(a)))
        for i in range(seg):
            j = (i + 1) % seg
            faces.append([i, j, seg + j, seg + i])
        faces.append(list(range(seg)))
        faces.append(list(range(seg, 2 * seg)))
    t = math.radians(rx)
    ct, st = math.cos(t), math.sin(t)
    verts = [(x + loc[0], y * ct - z * st + loc[1], y * st + z * ct + loc[2]) for x, y, z in verts]
    cx = sum(v[0] for v in verts) / len(verts); cy = sum(v[1] for v in verts) / len(verts); cz = sum(v[2] for v in verts) / len(verts)
    out = []
    for f in faces:  # flip any face whose normal points at the part's own centre
        n = face_normal(verts, f)
        m = [sum(verts[i][k] for i in f) / len(f) for k in range(3)]
        if n[0] * (m[0] - cx) + n[1] * (m[1] - cy) + n[2] * (m[2] - cz) < 0:
            f = f[::-1]
        out.append(f)
    return mat, verts, out

def face_normal(verts, f):
    nx = ny = nz = 0.0
    for i in range(len(f)):  # Newell's method
        a, b = verts[f[i]], verts[f[(i + 1) % len(f)]]
        nx += (a[1] - b[1]) * (a[2] + b[2]); ny += (a[2] - b[2]) * (a[0] + b[0]); nz += (a[0] - b[0]) * (a[1] + b[1])
    l = math.sqrt(nx * nx + ny * ny + nz * nz) or 1.0
    return (nx / l, ny / l, nz / l)

def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def colour(hexv):
    return [srgb_to_linear(((hexv >> s) & 255) / 255.0) for s in (16, 8, 0)]

# ---------------------------------------------------------------- GLB (no Blender needed)

def to_gltf(v):  # Blender Z-up to glTF Y-up
    return (v[0], v[2], -v[1])

def write_glb(name, parts, path):
    by_mat = {}
    for part in parts:
        mat, verts, faces = part_faces(part)
        pos, nor, idx = by_mat.setdefault(mat, ([], [], []))
        for f in faces:
            n = to_gltf(face_normal(verts, f))
            base = len(pos)
            for i in f:
                pos.append(to_gltf(verts[i])); nor.append(n)
            for k in range(1, len(f) - 1):
                idx.extend((base, base + k, base + k + 1))
    blob = bytearray(); views = []; accessors = []; prims = []; materials = []
    def add(data, target):
        while len(blob) % 4: blob.append(0)
        views.append({"buffer": 0, "byteOffset": len(blob), "byteLength": len(data), "target": target})
        blob.extend(data); return len(views) - 1
    for mat, (pos, nor, idx) in by_mat.items():
        hexv, metal, rough = MATS[mat]
        materials.append({"name": mat, "pbrMetallicRoughness": {"baseColorFactor": colour(hexv) + [1.0], "metallicFactor": metal, "roughnessFactor": rough}})
        pv = add(b"".join(struct.pack("<3f", *p) for p in pos), 34962)
        accessors.append({"bufferView": pv, "componentType": 5126, "count": len(pos), "type": "VEC3",
                          "min": [min(p[k] for p in pos) for k in range(3)], "max": [max(p[k] for p in pos) for k in range(3)]})
        nv = add(b"".join(struct.pack("<3f", *n) for n in nor), 34962)
        accessors.append({"bufferView": nv, "componentType": 5126, "count": len(nor), "type": "VEC3"})
        iv = add(struct.pack("<%dH" % len(idx), *idx), 34963)
        accessors.append({"bufferView": iv, "componentType": 5123, "count": len(idx), "type": "SCALAR"})
        prims.append({"attributes": {"POSITION": len(accessors) - 3, "NORMAL": len(accessors) - 2}, "indices": len(accessors) - 1, "material": len(materials) - 1})
    while len(blob) % 4: blob.append(0)
    doc = {"asset": {"version": "2.0", "generator": "dead-haul weapon builder"}, "scene": 0, "scenes": [{"nodes": [0]}],
           "nodes": [{"name": name, "mesh": 0}], "meshes": [{"name": name, "primitives": prims}], "materials": materials,
           "accessors": accessors, "bufferViews": views, "buffers": [{"byteLength": len(blob)}]}
    js = json.dumps(doc, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    with open(path, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(js) + 8 + len(blob)))
        f.write(struct.pack("<I4s", len(js), b"JSON")); f.write(js)
        f.write(struct.pack("<I4s", len(blob), b"BIN\x00")); f.write(bytes(blob))

def build_glbs(out):
    os.makedirs(out, exist_ok=True)
    for name, fn in WEAPONS.items():
        write_glb(name, fn(), os.path.join(out, name + ".glb"))
    with open(os.path.join(out, "manifest.json"), "w") as f:
        json.dump({"weapons": list(WEAPONS)}, f)

# ---------------------------------------------------------------- Blender

def build_in_blender(bpy):
    col = bpy.data.collections.get("DeadHaulWeapons")
    if col is None:
        col = bpy.data.collections.new("DeadHaulWeapons"); bpy.context.scene.collection.children.link(col)
    mats = {}
    for mname, (hexv, metal, rough) in MATS.items():
        m = bpy.data.materials.get("dh_" + mname) or bpy.data.materials.new("dh_" + mname)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = colour(hexv) + [1.0]
            bsdf.inputs["Metallic"].default_value = metal
            bsdf.inputs["Roughness"].default_value = rough
        m.diffuse_color = colour(hexv) + [1.0]
        mats[mname] = m
    for n, (name, fn) in enumerate(WEAPONS.items()):
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
        verts, faces, face_mats, order = [], [], [], []
        for part in fn():
            mat, pv, pf = part_faces(part)
            if mat not in order: order.append(mat)
            base = len(verts); verts.extend(pv)
            for f in pf:
                faces.append([base + i for i in f]); face_mats.append(order.index(mat))
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces); mesh.update()
        for mname in order: mesh.materials.append(mats[mname])
        for poly, mi in zip(mesh.polygons, face_mats): poly.material_index = mi
        obj = bpy.data.objects.new(name, mesh)
        obj.location = (n * 0.45 - 1.1, 0, 0)  # laid out side by side; the exporter moves each back to the origin
        col.objects.link(obj)
    print("Built:", ", ".join(WEAPONS))

try:
    import bpy
except ImportError:
    bpy = None

if bpy is not None:
    build_in_blender(bpy)
elif __name__ == "__main__":
    here = os.path.dirname(os.path.abspath(__file__))
    build_glbs(os.path.join(here, "..", "public", "models"))
    print("Wrote public/models/*.glb for:", ", ".join(WEAPONS))
