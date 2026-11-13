using System.Collections.Generic;
using UnityEngine;

namespace DeadHaul
{
    public class Exfil
    {
        public Vector3 pos;
        public string name;
        public Light light;
    }

    // Seeded procedural town. Same seed gives the same map for every player in a lobby.
    public class World : MonoBehaviour
    {
        public const float BLOCK = 34f, ROAD = 10f;
        public const int N = 5;
        public static float HALF => (N * BLOCK + (N + 1) * ROAD) / 2f;

        public int seed;
        public readonly List<Container> containers = new List<Container>();
        public readonly List<Exfil> exfils = new List<Exfil>();
        public readonly List<Vector3> roadPts = new List<Vector3>();
        public Vector3 spawn;
        System.Random rng;
        Transform stat, dyn;

        static readonly Color[] BuildingCols =
        {
            new Color(0.62f,0.36f,0.28f), new Color(0.55f,0.53f,0.50f), new Color(0.74f,0.68f,0.56f),
            new Color(0.42f,0.45f,0.48f), new Color(0.66f,0.58f,0.47f), new Color(0.50f,0.30f,0.26f),
            new Color(0.80f,0.76f,0.70f), new Color(0.35f,0.38f,0.34f)
        };
        static readonly Color[] CarCols =
        {
            new Color(0.55f,0.08f,0.07f), new Color(0.10f,0.18f,0.35f), new Color(0.80f,0.80f,0.78f),
            new Color(0.12f,0.12f,0.13f), new Color(0.75f,0.58f,0.12f), new Color(0.22f,0.35f,0.24f), new Color(0.45f,0.45f,0.47f)
        };

        public static float RoadC(int i) => -HALF + ROAD / 2f + i * (BLOCK + ROAD);
        public static float BlockC(int i) => -HALF + ROAD + BLOCK / 2f + i * (BLOCK + ROAD);

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool Chance(float p) => rng.NextDouble() < p;
        T Pick<T>(T[] a) => a[rng.Next(a.Length)];

        public void Build(int seed)
        {
            this.seed = seed;
            rng = new System.Random(seed);
            stat = new GameObject("Static").transform; stat.SetParent(transform, false);
            dyn = new GameObject("Dynamic").transform; dyn.SetParent(transform, false);

            float H = HALF;
            // Ground and roads
            Gfx.Prim(PrimitiveType.Cube, stat, new Vector3(0, -0.5f, 0), new Vector3(H * 2 + 60, 1, H * 2 + 60), Gfx.Lit(new Color(0.16f, 0.16f, 0.17f), 0.35f), true);
            var dash = Gfx.Lit(new Color(0.85f, 0.72f, 0.25f));
            for (int i = 0; i <= N; i++)
            {
                float c = RoadC(i);
                for (float t = -H + 4; t < H - 4; t += 8f)
                {
                    Gfx.Prim(PrimitiveType.Cube, stat, new Vector3(c, 0.01f, t), new Vector3(0.18f, 0.02f, 3f), dash);
                    Gfx.Prim(PrimitiveType.Cube, stat, new Vector3(t, 0.01f, c), new Vector3(3f, 0.02f, 0.18f), dash);
                }
                for (float t = -H + ROAD; t < H; t += BLOCK + ROAD)
                    for (float u = t; u < t + BLOCK; u += 3f)
                    {
                        roadPts.Add(new Vector3(c + R(-2.5f, 2.5f), 0, u));
                        roadPts.Add(new Vector3(u, 0, c + R(-2.5f, 2.5f)));
                    }
            }

            // Blocks
            for (int bx = 0; bx < N; bx++)
                for (int bz = 0; bz < N; bz++)
                {
                    var center = new Vector3(BlockC(bx), 0, BlockC(bz));
                    Gfx.Prim(PrimitiveType.Cube, stat, center + new Vector3(0, 0.1f, 0), new Vector3(BLOCK, 0.2f, BLOCK), Gfx.Lit(new Color(0.46f, 0.45f, 0.43f), 0.15f), true);
                    double roll = rng.NextDouble();
                    if (roll < 0.68) BuildingBlock(center);
                    else if (roll < 0.84) Park(center);
                    else ParkingLot(center);
                }

            // Street lights and burning barrels at intersections
            int lights = 0;
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    var p = new Vector3(RoadC(i) + ROAD / 2f - 0.6f, 0, RoadC(j) + ROAD / 2f - 0.6f);
                    if (lights < 18 && Chance(0.55f)) { StreetLight(p); lights++; }
                    else if (Chance(0.18f)) FireBarrel(new Vector3(RoadC(i) + R(-3, 3), 0, RoadC(j) + R(-3, 3)));
                    if (Chance(0.25f)) Barrier(new Vector3(RoadC(i) + R(-3, 3), 0, RoadC(j) + R(-3, 3)), R(0, 180));
                }

            // Parked and wrecked cars along the roads
            for (int k = 0; k < 26; k++)
            {
                bool alongX = Chance(0.5f);
                float lane = RoadC(rng.Next(N + 1)) + (Chance(0.5f) ? 3.2f : -3.2f);
                float t = R(-H + 8, H - 8);
                var p = alongX ? new Vector3(t, 0, lane) : new Vector3(lane, 0, t);
                Car(p, (alongX ? 90f : 0f) + R(-12, 12), Chance(0.3f));
            }

            // Boundary walls
            var wall = Gfx.Lit(new Color(0.38f, 0.37f, 0.35f));
            Gfx.Prim(PrimitiveType.Cube, stat, new Vector3(0, 2.5f, H + 0.5f), new Vector3(H * 2 + 2, 5, 1), wall, true);
            Gfx.Prim(PrimitiveType.Cube, stat, new Vector3(0, 2.5f, -H - 0.5f), new Vector3(H * 2 + 2, 5, 1), wall, true);
            Gfx.Prim(PrimitiveType.Cube, stat, new Vector3(H + 0.5f, 2.5f, 0), new Vector3(1, 5, H * 2 + 2), wall, true);
            Gfx.Prim(PrimitiveType.Cube, stat, new Vector3(-H - 0.5f, 2.5f, 0), new Vector3(1, 5, H * 2 + 2), wall, true);

            // Extraction points on the map edge
            var names = new[] { "North Gate", "Rail Yard", "Overpass", "Dock Road" };
            var used = new HashSet<int>();
            for (int k = 0; k < 3; k++)
            {
                int side = k % 4, r;
                do { r = rng.Next(N + 1); } while (used.Contains(side * 10 + r));
                used.Add(side * 10 + r);
                float c = RoadC(r), e = H - 5f;
                Vector3 p = side == 0 ? new Vector3(c, 0, e) : side == 1 ? new Vector3(e, 0, c) : side == 2 ? new Vector3(c, 0, -e) : new Vector3(-e, 0, c);
                MakeExfil(p, names[k]);
            }

            // Player spawn: an intersection far from the first exfil
            Vector3 best = Vector3.zero; float bd = -1;
            for (int t = 0; t < 12; t++)
            {
                var p = new Vector3(RoadC(rng.Next(N + 1)), 0, RoadC(rng.Next(N + 1)));
                float d = float.MaxValue; foreach (var ex in exfils) d = Mathf.Min(d, Vector3.Distance(p, ex.pos));
                if (d > bd) { bd = d; best = p; }
            }
            spawn = best + new Vector3(1.5f, 0.2f, 1.5f);

        }

        void BuildingBlock(Vector3 c)
        {
            int layout = rng.Next(3);
            var lots = new List<(Vector3, Vector2)>();
            float q = BLOCK / 2f;
            if (layout == 0) lots.Add((c, new Vector2(BLOCK - 5, BLOCK - 5)));
            else if (layout == 1)
            {
                lots.Add((c + new Vector3(-q / 2, 0, 0), new Vector2(q - 3, BLOCK - 5)));
                lots.Add((c + new Vector3(q / 2, 0, 0), new Vector2(q - 3, BLOCK - 5)));
            }
            else
                for (int i = 0; i < 4; i++)
                    lots.Add((c + new Vector3((i % 2 == 0 ? -1 : 1) * q / 2, 0, (i < 2 ? -1 : 1) * q / 2), new Vector2(q - 3, q - 3)));

            foreach (var (p, size) in lots)
            {
                float w = size.x * R(0.7f, 1f), d = size.y * R(0.7f, 1f);
                float h = Mathf.Round(R(2, 8)) * 3.2f;
                Building(p + new Vector3(0, 0.2f, 0), w, d, h);
            }
        }

        void Building(Vector3 p, float w, float d, float h)
        {
            var col = Pick(BuildingCols);
            var mat = Gfx.Textured(col, Chance(0.4f) ? Gfx.WindowTexLit : Gfx.WindowTex, new Vector2(Mathf.Max(1, Mathf.Round(w / 6.4f)), Mathf.Max(1, Mathf.Round(h / 6.4f))));
            Gfx.Prim(PrimitiveType.Cube, stat, p + new Vector3(0, h / 2, 0), new Vector3(w, h, d), mat, true);
            // Roof cap, parapet and clutter
            var dark = Gfx.Lit(col * 0.55f);
            Gfx.Prim(PrimitiveType.Cube, stat, p + new Vector3(0, h + 0.25f, 0), new Vector3(w + 0.3f, 0.5f, d + 0.3f), dark, true);
            int ac = rng.Next(0, 4);
            for (int i = 0; i < ac; i++)
                Gfx.Box(stat, p + new Vector3(R(-w / 3, w / 3), h + 0.95f, R(-d / 3, d / 3)), new Vector3(R(1.2f, 2.4f), 0.9f, R(1.2f, 2f)), new Color(0.6f, 0.6f, 0.6f), true);
            // Street-level storefront strip
            Gfx.Box(stat, p + new Vector3(0, 3.35f, d / 2 + 0.25f), new Vector3(w * 0.9f, 0.35f, 0.5f), Pick(new[] { new Color(0.6f, 0.15f, 0.1f), new Color(0.1f, 0.3f, 0.45f), new Color(0.2f, 0.35f, 0.2f) }), false);
            Gfx.Box(stat, p + new Vector3(R(-w / 3, w / 3), 1.2f, d / 2 + 0.05f), new Vector3(1.6f, 2.4f, 0.15f), new Color(0.1f, 0.08f, 0.07f), false);

            int n = rng.Next(1, 4);
            for (int i = 0; i < n; i++)
            {
                int side = rng.Next(4);
                Vector3 off = side == 0 ? new Vector3(R(-w / 2.5f, w / 2.5f), 0, d / 2 + 0.9f) :
                              side == 1 ? new Vector3(R(-w / 2.5f, w / 2.5f), 0, -d / 2 - 0.9f) :
                              side == 2 ? new Vector3(w / 2 + 0.9f, 0, R(-d / 2.5f, d / 2.5f)) : new Vector3(-w / 2 - 0.9f, 0, R(-d / 2.5f, d / 2.5f));
                double r = rng.NextDouble();
                var kind = r < 0.08 ? ContainerKind.Safe : r < 0.4 ? ContainerKind.Crate : r < 0.7 ? ContainerKind.Locker : ContainerKind.Dumpster;
                MakeContainer(kind, p + off, side < 2 ? 0 : 90);
            }
        }

        void Park(Vector3 c)
        {
            Gfx.Prim(PrimitiveType.Cube, stat, c + new Vector3(0, 0.22f, 0), new Vector3(BLOCK - 2, 0.06f, BLOCK - 2), Gfx.Lit(new Color(0.24f, 0.36f, 0.16f), 0.05f), false);
            int trees = rng.Next(7, 14);
            for (int i = 0; i < trees; i++) Tree(c + new Vector3(R(-14, 14), 0.2f, R(-14, 14)));
            for (int i = 0; i < 3; i++)
                Gfx.Box(stat, c + new Vector3(R(-10, 10), 0.6f, R(-10, 10)), new Vector3(2.2f, 0.15f, 0.6f), new Color(0.4f, 0.27f, 0.15f), true, R(0, 180));
            for (int i = 0; i < 2; i++) MakeContainer(Chance(0.15f) ? ContainerKind.Safe : ContainerKind.Crate, c + new Vector3(R(-12, 12), 0.2f, R(-12, 12)), R(0, 90));
        }

        void ParkingLot(Vector3 c)
        {
            var line = Gfx.Lit(new Color(0.8f, 0.8f, 0.78f));
            for (int row = -1; row <= 1; row += 2)
                for (int i = -3; i <= 3; i++)
                {
                    var p = c + new Vector3(i * 4.2f, 0.2f, row * 8f);
                    Gfx.Prim(PrimitiveType.Cube, stat, p + new Vector3(2.1f, 0.02f, 0), new Vector3(0.12f, 0.02f, 5f), line);
                    if (Chance(0.6f)) Car(p, R(-4, 4), Chance(0.35f));
                }
            MakeContainer(ContainerKind.Dumpster, c + new Vector3(R(-12, 12), 0.2f, 14), 0);
        }

        void Tree(Vector3 p)
        {
            float s = R(0.8f, 1.3f);
            Gfx.Prim(PrimitiveType.Cylinder, stat, p + new Vector3(0, 1.4f * s, 0), new Vector3(0.35f, 1.4f * s, 0.35f), Gfx.Lit(new Color(0.3f, 0.2f, 0.12f)), true);
            var leaf = Gfx.Lit(new Color(0.18f + R(0, 0.08f), 0.32f + R(0, 0.1f), 0.12f), 0.05f);
            Gfx.Prim(PrimitiveType.Sphere, stat, p + new Vector3(0, 3.6f * s, 0), new Vector3(3.2f, 2.6f, 3.2f) * s, leaf);
            Gfx.Prim(PrimitiveType.Sphere, stat, p + new Vector3(0.6f, 4.6f * s, 0.3f), new Vector3(2.2f, 1.8f, 2.2f) * s, leaf);
        }

        void Car(Vector3 p, float rotY, bool lootable)
        {
            var root = new GameObject("Car").transform;
            root.SetParent(stat, false); root.localPosition = p; root.localRotation = Quaternion.Euler(0, rotY, 0);
            var paint = Gfx.Lit(Pick(CarCols), 0.6f);
            bool wreck = Chance(0.3f);
            if (wreck) paint = Gfx.Lit(new Color(0.22f, 0.15f, 0.11f), 0.1f);
            Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.75f, 0), new Vector3(1.9f, 0.8f, 4.4f), paint, true);
            Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0, 1.42f, -0.2f), new Vector3(1.7f, 0.6f, 2.3f), Gfx.Lit(new Color(0.08f, 0.1f, 0.12f), 0.9f), true);
            var tyre = Gfx.Lit(new Color(0.07f, 0.07f, 0.07f));
            for (int i = 0; i < 4; i++)
                Gfx.Prim(PrimitiveType.Cylinder, root, new Vector3(i % 2 == 0 ? -0.92f : 0.92f, 0.36f, i < 2 ? 1.4f : -1.4f), new Vector3(0.72f, 0.14f, 0.72f), tyre, false, new Vector3(0, 0, 90));
            Gfx.Prim(PrimitiveType.Cube, root, new Vector3(-0.6f, 0.8f, 2.21f), new Vector3(0.35f, 0.15f, 0.05f), Gfx.Glow(new Color(2f, 1.9f, 1.6f)));
            Gfx.Prim(PrimitiveType.Cube, root, new Vector3(0.6f, 0.8f, 2.21f), new Vector3(0.35f, 0.15f, 0.05f), Gfx.Glow(new Color(2f, 1.9f, 1.6f)));
            if (lootable) MakeContainer(ContainerKind.Trunk, p + root.rotation * new Vector3(0, 0, -2.9f), rotY);
        }

        void StreetLight(Vector3 p)
        {
            var pole = Gfx.Lit(new Color(0.2f, 0.21f, 0.22f), 0.5f);
            Gfx.Prim(PrimitiveType.Cylinder, stat, p + new Vector3(0, 3.5f, 0), new Vector3(0.18f, 3.5f, 0.18f), pole, true);
            Gfx.Prim(PrimitiveType.Cube, stat, p + new Vector3(-0.8f, 6.9f, -0.8f), new Vector3(0.15f, 0.12f, 2.2f), pole, false, new Vector3(0, 45, 0));
            Gfx.Prim(PrimitiveType.Cube, stat, p + new Vector3(-1.5f, 6.8f, -1.5f), new Vector3(0.6f, 0.12f, 0.35f), Gfx.Glow(new Color(3f, 2.4f, 1.4f)), false, new Vector3(0, 45, 0));
            var l = new GameObject("Lamp").AddComponent<Light>();
            l.transform.SetParent(dyn, false); l.transform.localPosition = p + new Vector3(-1.5f, 6.4f, -1.5f);
            l.type = LightType.Point; l.range = 16f; l.intensity = 2.2f; l.color = new Color(1f, 0.78f, 0.5f); l.shadows = LightShadows.None;
        }

        void FireBarrel(Vector3 p)
        {
            Gfx.Prim(PrimitiveType.Cylinder, stat, p + new Vector3(0, 0.5f, 0), new Vector3(0.7f, 0.5f, 0.7f), Gfx.Lit(new Color(0.3f, 0.18f, 0.1f), 0.3f), true);
            var fire = Gfx.Prim(PrimitiveType.Sphere, dyn, p + new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.4f, 0.6f), Gfx.Glow(new Color(4f, 1.6f, 0.3f)));
            var l = new GameObject("Fire").AddComponent<Light>();
            l.transform.SetParent(dyn, false); l.transform.localPosition = p + new Vector3(0, 1.6f, 0);
            l.type = LightType.Point; l.range = 9f; l.intensity = 2.5f; l.color = new Color(1f, 0.55f, 0.2f);
            var f = l.gameObject.AddComponent<Flicker>(); f.baseI = 2.5f; f.flame = fire.transform;
        }

        void Barrier(Vector3 p, float rot)
        {
            Gfx.Box(stat, p + new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.9f, 3f), new Color(0.6f, 0.59f, 0.56f), true, rot);
        }

        void MakeExfil(Vector3 p, string name)
        {
            var ex = new Exfil { pos = p, name = name };
            Gfx.Prim(PrimitiveType.Cylinder, dyn, p + new Vector3(0, 0.03f, 0), new Vector3(8f, 0.03f, 8f), Gfx.Glow(new Color(0.2f, 1.2f, 0.4f)));
            Gfx.Prim(PrimitiveType.Cube, dyn, p + new Vector3(0, 0.35f, 0), new Vector3(0.25f, 0.7f, 0.25f), Gfx.Glow(new Color(0.6f, 6f, 1.2f)));
            var l = new GameObject("Flare").AddComponent<Light>();
            l.transform.SetParent(dyn, false); l.transform.localPosition = p + new Vector3(0, 1.5f, 0);
            l.type = LightType.Point; l.range = 18f; l.intensity = 4f; l.color = new Color(0.3f, 1f, 0.4f);
            var f = l.gameObject.AddComponent<Flicker>(); f.baseI = 4f;
            ex.light = l;

            var ps = new GameObject("Smoke").AddComponent<ParticleSystem>();
            ps.transform.SetParent(dyn, false); ps.transform.localPosition = p + new Vector3(0, 0.7f, 0);
            ps.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var main = ps.main;
            main.startLifetime = 6f; main.startSpeed = 2.2f; main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startColor = new Color(0.45f, 1f, 0.55f, 0.35f); main.maxParticles = 160; main.gravityModifier = -0.05f;
            var em = ps.emission; em.rateOverTime = 14f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.2f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.6f));
            var co = ps.colorOverLifetime; co.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.6f, 0), new GradientAlphaKey(0f, 1) });
            co.color = g;
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = Gfx.Particle(new Color(0.6f, 1f, 0.7f, 0.5f));
            exfils.Add(ex);
        }

        public Container MakeContainer(ContainerKind kind, Vector3 p, float rotY)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(stat, false);
            go.transform.localPosition = p; go.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            switch (kind)
            {
                case ContainerKind.Crate:
                    Gfx.Box(go.transform, new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.9f, 0.9f), new Color(0.45f, 0.33f, 0.18f), true);
                    Gfx.Box(go.transform, new Vector3(0, 0.92f, 0), new Vector3(1.25f, 0.06f, 0.95f), new Color(0.32f, 0.23f, 0.12f));
                    break;
                case ContainerKind.Locker:
                    Gfx.Box(go.transform, new Vector3(0, 1f, 0), new Vector3(0.9f, 2f, 0.6f), new Color(0.28f, 0.36f, 0.42f), true);
                    Gfx.Box(go.transform, new Vector3(0.2f, 1.2f, 0.31f), new Vector3(0.05f, 0.3f, 0.02f), new Color(0.7f, 0.7f, 0.7f));
                    break;
                case ContainerKind.Dumpster:
                    Gfx.Box(go.transform, new Vector3(0, 0.7f, 0), new Vector3(2.4f, 1.4f, 1.4f), new Color(0.17f, 0.32f, 0.2f), true);
                    Gfx.Box(go.transform, new Vector3(0, 1.45f, 0), new Vector3(2.45f, 0.1f, 1.45f), new Color(0.1f, 0.2f, 0.12f));
                    break;
                case ContainerKind.Safe:
                    Gfx.Box(go.transform, new Vector3(0, 0.55f, 0), new Vector3(0.9f, 1.1f, 0.9f), new Color(0.2f, 0.2f, 0.22f), true);
                    Gfx.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.65f, 0.46f), new Vector3(0.25f, 0.02f, 0.25f), Gfx.Glow(new Color(2.5f, 1.8f, 0.4f)), false, new Vector3(90, 0, 0));
                    break;
                case ContainerKind.Trunk:
                    break;
            }
            var c = go.AddComponent<Container>();
            c.Init(kind, rng.Next());
            containers.Add(c);
            return c;
        }

        public Vector3 RoadPoint(float minD, float maxD, Vector3 from)
        {
            for (int t = 0; t < 40; t++)
            {
                var p = roadPts[Random.Range(0, roadPts.Count)];
                float d = Vector3.Distance(p, from);
                if (d >= minD && d <= maxD) return p;
            }
            return roadPts[Random.Range(0, roadPts.Count)];
        }
    }

    public class Flicker : MonoBehaviour
    {
        public float baseI = 2f;
        public Transform flame;
        Light l; float seed;
        void Awake() { l = GetComponent<Light>(); seed = Random.value * 100f; }
        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 6f);
            if (l) l.intensity = baseI * (0.7f + n * 0.6f);
            if (flame) flame.localScale = new Vector3(0.6f, 0.3f + n * 0.4f, 0.6f);
        }
    }
}
