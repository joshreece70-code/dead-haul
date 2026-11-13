using UnityEngine;

namespace DeadHaul
{
    // Low-poly character built from primitives, with pivots at hips and shoulders so limbs swing naturally.
    public class Rig
    {
        public Transform root, body, head, armL, armR, legL, legR, hand, gun, muzzle;
        public string gunId;
        float walk;

        public static Rig Build(Transform parent, Color skin, Color shirt, Color pants, Color hat)
        {
            var r = new Rig();
            r.root = new GameObject("Rig").transform; r.root.SetParent(parent, false);
            r.body = new GameObject("Body").transform; r.body.SetParent(r.root, false);
            var shirtM = Gfx.Lit(shirt, 0.15f); var pantsM = Gfx.Lit(pants, 0.1f); var skinM = Gfx.Lit(skin, 0.3f);
            Gfx.Prim(PrimitiveType.Cube, r.body, new Vector3(0, 1.27f, 0), new Vector3(0.56f, 0.66f, 0.3f), shirtM);
            Gfx.Prim(PrimitiveType.Cube, r.body, new Vector3(0, 1.0f, 0), new Vector3(0.5f, 0.14f, 0.28f), Gfx.Lit(new Color(0.12f, 0.1f, 0.08f)));
            r.head = new GameObject("Head").transform; r.head.SetParent(r.body, false); r.head.localPosition = new Vector3(0, 1.62f, 0);
            Gfx.Prim(PrimitiveType.Cube, r.head, new Vector3(0, 0.17f, 0), new Vector3(0.3f, 0.32f, 0.3f), skinM);
            Gfx.Prim(PrimitiveType.Cube, r.head, new Vector3(0, 0.36f, -0.02f), new Vector3(0.34f, 0.1f, 0.36f), Gfx.Lit(hat, 0.2f));
            Gfx.Prim(PrimitiveType.Cube, r.head, new Vector3(0, 0.2f, 0.151f), new Vector3(0.2f, 0.05f, 0.01f), Gfx.Lit(new Color(0.05f, 0.05f, 0.05f)));
            r.armL = Limb(r.body, new Vector3(-0.36f, 1.55f, 0), new Vector3(0.16f, 0.66f, 0.17f), shirtM, skinM);
            r.armR = Limb(r.body, new Vector3(0.36f, 1.55f, 0), new Vector3(0.16f, 0.66f, 0.17f), shirtM, skinM);
            r.legL = Limb(r.root, new Vector3(-0.14f, 0.95f, 0), new Vector3(0.21f, 0.92f, 0.23f), pantsM, Gfx.Lit(new Color(0.1f, 0.08f, 0.06f)));
            r.legR = Limb(r.root, new Vector3(0.14f, 0.95f, 0), new Vector3(0.21f, 0.92f, 0.23f), pantsM, Gfx.Lit(new Color(0.1f, 0.08f, 0.06f)));
            r.hand = new GameObject("Hand").transform; r.hand.SetParent(r.armR, false);
            r.hand.localPosition = new Vector3(0, -0.66f, 0); r.hand.localRotation = Quaternion.Euler(90, 0, 0);
            return r;
        }

        static Transform Limb(Transform parent, Vector3 pivot, Vector3 size, Material upper, Material end)
        {
            var p = new GameObject("Limb").transform; p.SetParent(parent, false); p.localPosition = pivot;
            Gfx.Prim(PrimitiveType.Cube, p, new Vector3(0, -size.y * 0.42f, 0), new Vector3(size.x, size.y * 0.84f, size.z), upper);
            Gfx.Prim(PrimitiveType.Cube, p, new Vector3(0, -size.y * 0.92f, size.y > 0.8f ? 0.04f : 0), new Vector3(size.x * 0.95f, size.y * 0.16f, size.z * (size.y > 0.8f ? 1.4f : 0.95f)), end);
            return p;
        }

        public void SetGun(string id)
        {
            if (gunId == id && gun != null) return;
            gunId = id;
            if (gun != null) Object.Destroy(gun.gameObject);
            gun = null; muzzle = null;
            if (string.IsNullOrEmpty(id)) return;
            gun = GunModel(id, hand, out muzzle);
        }

        // Guns built from boxes and cylinders. Forward is local +Z.
        public static Transform GunModel(string id, Transform parent, out Transform muzzle)
        {
            var def = Defs.Gun(id);
            var g = new GameObject("Gun_" + id).transform; g.SetParent(parent, false);
            var metal = Gfx.Lit(new Color(0.13f, 0.14f, 0.15f), 0.65f);
            var tint = Gfx.Lit(def.tint, 0.4f);
            float L = def.length;
            muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(g, false);
            if (def.melee)
            {
                if (id == "saber")
                {
                    Gfx.Prim(PrimitiveType.Cylinder, g, new Vector3(0, 0, 0.05f), new Vector3(0.05f, 0.13f, 0.05f), metal, false, new Vector3(90, 0, 0));
                    Gfx.Prim(PrimitiveType.Cylinder, g, new Vector3(0, 0, 0.18f + L / 2), new Vector3(0.045f, L / 2, 0.045f), Gfx.Glow(new Color(0.6f, 1.8f, 6f)), false, new Vector3(90, 0, 0));
                }
                else
                {
                    Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, 0, 0.02f), new Vector3(0.05f, 0.05f, 0.24f), Gfx.Lit(new Color(0.12f, 0.06f, 0.05f)));
                    Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, 0, 0.15f), new Vector3(0.12f, 0.02f, 0.05f), Gfx.Lit(new Color(0.6f, 0.5f, 0.2f), 0.7f));
                    Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, 0.01f, 0.18f + L / 2), new Vector3(0.015f, 0.05f, L), Gfx.Lit(new Color(0.8f, 0.82f, 0.85f), 0.95f));
                }
                muzzle.localPosition = new Vector3(0, 0, L);
                g.localRotation = Quaternion.Euler(-35, 0, 0);
                return g;
            }
            if (id == "minigun")
            {
                Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, 0, 0.1f), new Vector3(0.22f, 0.22f, 0.45f), tint);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3;
                    Gfx.Prim(PrimitiveType.Cylinder, g, new Vector3(Mathf.Cos(a) * 0.06f, Mathf.Sin(a) * 0.06f, 0.65f), new Vector3(0.035f, 0.4f, 0.035f), metal, false, new Vector3(90, 0, 0));
                }
                Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, -0.2f, 0.05f), new Vector3(0.18f, 0.2f, 0.25f), Gfx.Lit(new Color(0.3f, 0.32f, 0.2f)));
                muzzle.localPosition = new Vector3(0, 0, 1.05f);
                return g;
            }
            bool longGun = def.slot == Slot.Primary;
            float bodyL = L * (longGun ? 0.45f : 0.6f);
            Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, 0.03f, bodyL * 0.35f), new Vector3(0.07f, 0.12f, bodyL), tint);
            Gfx.Prim(PrimitiveType.Cylinder, g, new Vector3(0, 0.05f, bodyL * 0.85f + L * 0.2f), new Vector3(0.035f, L * 0.2f, 0.035f), metal, false, new Vector3(90, 0, 0));
            Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, -0.08f, -0.02f), new Vector3(0.05f, 0.14f, 0.07f), metal, false, new Vector3(-15, 0, 0));
            if (longGun)
            {
                Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, 0.0f, -0.2f), new Vector3(0.06f, 0.11f, 0.3f), tint);
                if (id != "shotgun" && id != "spas") Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, -0.12f, bodyL * 0.42f), new Vector3(0.05f, 0.18f, 0.08f), metal, false, id == "ak" ? new Vector3(20, 0, 0) : (Vector3?)null);
                else Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, -0.04f, bodyL * 0.9f), new Vector3(0.07f, 0.06f, 0.25f), tint);
                if (def.scope)
                {
                    float sl = id == "sniper" ? 0.35f : 0.14f;
                    Gfx.Prim(PrimitiveType.Cylinder, g, new Vector3(0, 0.14f, bodyL * 0.45f), new Vector3(0.05f, sl / 2, 0.05f), metal, false, new Vector3(90, 0, 0));
                    if (id == "rifle") Gfx.Prim(PrimitiveType.Cube, g, new Vector3(0, 0.14f, bodyL * 0.45f + 0.08f), new Vector3(0.012f, 0.012f, 0.005f), Gfx.Glow(new Color(6f, 0.3f, 0.2f)));
                }
            }
            muzzle.localPosition = new Vector3(0, 0.05f, bodyL * 0.85f + L * 0.4f);
            return g;
        }

        // speed in m/s; aim true holds the gun up to the shoulder.
        public void Animate(float dt, float speed, bool aim, bool melee, float swing, float crouch, float prone)
        {
            walk += dt * Mathf.Clamp(speed, 0, 9) * 1.7f;
            float amp = Mathf.Clamp01(speed / 4f) * 0.75f;
            float s = Mathf.Sin(walk) * amp;
            legL.localRotation = Quaternion.Euler(s * 45f - crouch * 50f, 0, 0);
            legR.localRotation = Quaternion.Euler(-s * 45f - crouch * 50f, 0, 0);
            body.localPosition = new Vector3(0, -crouch * 0.38f + Mathf.Abs(Mathf.Cos(walk)) * amp * 0.06f, crouch * 0.12f);
            body.localRotation = Quaternion.Euler(crouch * 18f, 0, 0);
            root.localRotation = Quaternion.Euler(prone * 82f, 0, 0);
            root.localPosition = new Vector3(0, prone * 0.22f, 0);
            if (melee)
            {
                float sw = swing > 0 ? Mathf.Sin((1 - swing) * Mathf.PI) : 0;
                armR.localRotation = Quaternion.Euler(-70f + sw * -60f, -sw * 70f, 0);
                armL.localRotation = Quaternion.Euler(-30f + s * 20f, 10f, 0);
            }
            else if (aim)
            {
                armR.localRotation = Quaternion.Euler(-88f, -4f, 0);
                armL.localRotation = Quaternion.Euler(-80f, 32f, 0);
            }
            else
            {
                armR.localRotation = Quaternion.Euler(-55f, -10f, 0);
                armL.localRotation = Quaternion.Euler(-50f, 30f, 0);
            }
        }

        // Zombie arms-forward shamble.
        public void AnimateInfected(float dt, float speed, float attack)
        {
            walk += dt * Mathf.Clamp(speed, 0, 9) * 1.5f;
            float s = Mathf.Sin(walk) * Mathf.Clamp01(speed / 3f) * 0.8f;
            legL.localRotation = Quaternion.Euler(s * 40f, 0, 0);
            legR.localRotation = Quaternion.Euler(-s * 40f, 0, 0);
            float a = attack > 0 ? Mathf.Sin(attack * Mathf.PI) * 40f : 0;
            armL.localRotation = Quaternion.Euler(-85f + s * 15f + a, 8f, 0);
            armR.localRotation = Quaternion.Euler(-85f - s * 15f + a, -8f, 0);
            body.localRotation = Quaternion.Euler(14f + a * 0.3f, Mathf.Sin(walk * 0.5f) * 6f, 0);
            head.localRotation = Quaternion.Euler(-10f, 0, Mathf.Sin(walk * 0.7f) * 12f);
        }
    }
}
