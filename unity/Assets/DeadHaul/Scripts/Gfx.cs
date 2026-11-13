using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeadHaul
{
    // Materials, primitives, lighting, post-processing and quality presets.
    public static class Gfx
    {
        public const int EnemyLayer = 1;      // TransparentFX: enemies and remote players (bullets hit them, cover checks ignore them)
        public const int PlayerLayer = 2;     // Ignore Raycast: the local player is skipped by every default raycast
        public static int WorldMask => 1 << 0;
        public static int ShotMask => (1 << 0) | (1 << EnemyLayer);

        static Material baseLit, baseUnlit, baseParticle;
        static readonly Dictionary<int, Material> litCache = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> glowCache = new Dictionary<int, Material>();
        static readonly Dictionary<int, Material> partCache = new Dictionary<int, Material>();
        public static Texture2D WindowTex, WindowTexLit, NoiseTex;

        public static Volume Post;
        public static Vignette Vig;
        public static ColorAdjustments Grade;
        public static ChromaticAberration Chroma;
        public static Light Sun;

        public static void Init()
        {
            // Saved material assets (made by the editor script) are what a built .exe can rely on.
            baseLit = Resources.Load<Material>("DH_Lit");
            baseUnlit = Resources.Load<Material>("DH_Unlit");
            baseParticle = Resources.Load<Material>("DH_Particle");
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null)
            {
                if (baseLit == null) baseLit = rp.defaultMaterial;
                if (baseParticle == null) baseParticle = rp.defaultParticleMaterial;
            }
            if (baseLit == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                baseLit = new Material(sh);
                Debug.LogWarning("Dead Haul: material assets missing. Use the Dead Haul menu > Set Up Project.");
            }
            if (baseUnlit == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                baseUnlit = sh != null ? new Material(sh) : baseLit;
            }
            if (baseParticle == null) baseParticle = baseUnlit;
            WindowTex = MakeWindowTex(false);
            WindowTexLit = MakeWindowTex(true);
            NoiseTex = MakeNoiseTex();
        }

        static int Key(Color c) => ((int)(c.r * 255) << 16) ^ ((int)(c.g * 255) << 8) ^ (int)(c.b * 255) ^ ((int)(c.a * 255) << 24);

        public static Material Lit(Color c, float smooth = 0.25f)
        {
            int k = Key(c) ^ (int)(smooth * 97) * 7919;
            if (litCache.TryGetValue(k, out var m) && m != null) return m;
            m = new Material(baseLit);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", NoiseTex);
            m.enableInstancing = true;
            litCache[k] = m;
            return m;
        }

        public static Material Textured(Color c, Texture2D tex, Vector2 tiling, float smooth = 0.2f)
        {
            var m = new Material(baseLit);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", tiling); }
            else { m.mainTexture = tex; m.mainTextureScale = tiling; }
            return m;
        }

        static int HdrKey(Color c) => (Mathf.RoundToInt(c.r * 40) << 21) ^ (Mathf.RoundToInt(c.g * 40) << 11) ^ Mathf.RoundToInt(c.b * 40) ^ (Mathf.RoundToInt(c.a * 15) << 28);

        // Solid unlit colour. Values above 1 glow through bloom.
        public static Material Glow(Color c)
        {
            int k = HdrKey(c);
            if (glowCache.TryGetValue(k, out var m) && m != null) return m;
            m = new Material(baseUnlit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            glowCache[k] = m;
            return m;
        }

        // Soft transparent material for particles, smoke and tracers.
        public static Material Particle(Color c)
        {
            int k = HdrKey(c);
            if (partCache.TryGetValue(k, out var m) && m != null) return m;
            m = new Material(baseParticle);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            partCache[k] = m;
            return m;
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = false, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(t);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (!collider) { var c = g.GetComponent<Collider>(); c.enabled = false; Object.Destroy(c); }
            return g;
        }

        public static GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Color c, bool collider = false, float rotY = 0f)
            => Prim(PrimitiveType.Cube, parent, pos, scale, Lit(c), collider, new Vector3(0, rotY, 0));

        public static void SetLayer(GameObject g, int layer)
        {
            g.layer = layer;
            foreach (Transform t in g.transform) SetLayer(t.gameObject, layer);
        }

        static Texture2D MakeWindowTex(bool lit)
        {
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
            var px = new Color[S * S];
            var rng = new System.Random(lit ? 7 : 3);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float n = 0.9f + (float)rng.NextDouble() * 0.1f;
                    Color c = new Color(n, n, n);
                    int cx = x % 32, cy = y % 32;
                    bool win = cx > 6 && cx < 26 && cy > 8 && cy < 27;
                    bool frame = win && (cx == 16 || cy == 18);
                    if (win)
                    {
                        bool on = lit && ((x / 32 + y / 32 * 3) % 3 == 0);
                        c = frame ? new Color(0.3f, 0.3f, 0.3f) : on ? new Color(1.6f, 1.25f, 0.7f) : new Color(0.12f, 0.15f, 0.18f);
                    }
                    if (cy < 2) c *= 0.75f; // floor line
                    px[y * S + x] = c;
                }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 4;
            t.Apply(true);
            return t;
        }

        static Texture2D MakeNoiseTex()
        {
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
            var rng = new System.Random(11);
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) { float n = 0.86f + (float)rng.NextDouble() * 0.14f; px[i] = new Color(n, n, n); }
            t.SetPixels(px); t.wrapMode = TextureWrapMode.Repeat; t.Apply(true);
            return t;
        }

        public static void SetupLighting()
        {
            var sunGo = new GameObject("Sun");
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(1f, 0.80f, 0.62f);
            Sun.intensity = 1.5f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.85f;
            sunGo.transform.rotation = Quaternion.Euler(32f, -38f, 0f);
            RenderSettings.sun = Sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.47f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.32f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.14f, 0.13f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0085f;
            RenderSettings.fogColor = new Color(0.55f, 0.50f, 0.47f);
        }

        public static void SetupPost(Camera cam)
        {
            var go = new GameObject("PostFX");
            Post = go.AddComponent<Volume>();
            Post.isGlobal = true;
            Post.priority = 100;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            Post.sharedProfile = p;

            var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bl = p.Add<Bloom>(true); bl.intensity.Override(0.9f); bl.threshold.Override(0.95f); bl.scatter.Override(0.7f);
            Vig = p.Add<Vignette>(true); Vig.intensity.Override(0.3f); Vig.smoothness.Override(0.45f); Vig.color.Override(Color.black);
            Grade = p.Add<ColorAdjustments>(true);
            Grade.postExposure.Override(0.25f); Grade.contrast.Override(14f); Grade.saturation.Override(-6f);
            Grade.colorFilter.Override(new Color(1f, 0.97f, 0.92f));
            Chroma = p.Add<ChromaticAberration>(true); Chroma.intensity.Override(0f);
            var fg = p.Add<FilmGrain>(true); fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(0.22f);
            var lgg = p.Add<ShadowsMidtonesHighlights>(true);
            lgg.shadows.Override(new Vector4(0.92f, 0.98f, 1.08f, 0f));
            lgg.highlights.Override(new Vector4(1.06f, 1.0f, 0.93f, 0f));

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
        }

        public static readonly string[] QualityNames = { "Low", "Medium", "High", "Ultra" };

        // 0 Low .. 3 Ultra. Ultra is for PCs that run Fortnite well; Low keeps older laptops playable.
        public static void ApplyQuality(int q, Camera cam)
        {
            int n = QualitySettings.names.Length;
            if (n > 0) QualitySettings.SetQualityLevel(q == 0 ? 0 : n - 1, true);
            var a = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (a != null)
            {
                a.renderScale = q == 0 ? 0.75f : 1f;
                a.msaaSampleCount = q == 0 ? 1 : q == 1 ? 2 : 4;
                a.shadowDistance = q == 0 ? 45f : q == 1 ? 75f : q == 2 ? 110f : 160f;
                a.shadowCascadeCount = q == 0 ? 1 : q == 1 ? 2 : 4;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = q == 0 ? 60 : 165;
            if (Sun != null) Sun.shadows = q == 0 ? LightShadows.Hard : LightShadows.Soft;
            if (cam != null)
            {
                cam.farClipPlane = q == 0 ? 220f : 400f;
                var data = cam.GetUniversalAdditionalCameraData();
                data.antialiasing = q == 0 ? AntialiasingMode.None : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            RenderSettings.fogDensity = q == 0 ? 0.011f : 0.0085f;
        }
    }
}
