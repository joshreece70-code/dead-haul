using System.Collections.Generic;
using UnityEngine;

namespace DeadHaul
{
    // Pooled tracers, muzzle flashes and impact bursts.
    public class Fx : MonoBehaviour
    {
        public static Fx I;

        class Tracer { public LineRenderer lr; public float t; }
        readonly List<Tracer> tracers = new List<Tracer>();
        int nextT;
        Light flash; float flashT;
        ParticleSystem sparks, blood, dust;

        void Awake()
        {
            I = this;
            var tracerMat = Gfx.Particle(new Color(5f, 3.6f, 1.6f));
            for (int i = 0; i < 48; i++)
            {
                var go = new GameObject("tracer"); go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = tracerMat; lr.positionCount = 2; lr.useWorldSpace = true;
                lr.startWidth = 0.035f; lr.endWidth = 0.012f; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
                lr.enabled = false;
                tracers.Add(new Tracer { lr = lr });
            }
            flash = new GameObject("flash").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.type = LightType.Point; flash.range = 9f; flash.color = new Color(1f, 0.75f, 0.4f); flash.intensity = 0; flash.shadows = LightShadows.None;
            sparks = Burst("sparks", new Color(1f, 0.7f, 0.3f), 0.06f, 7f, 0.25f, 0.6f, 4f);
            blood = Burst("blood", new Color(0.5f, 0.02f, 0.02f, 0.9f), 0.12f, 3.5f, 0.45f, 1.2f, 1f);
            dust = Burst("dust", new Color(0.55f, 0.5f, 0.45f, 0.6f), 0.25f, 1.5f, 0.7f, 0.2f, 1f);
        }

        ParticleSystem Burst(string name, Color c, float size, float speed, float life, float gravity, float boost)
        {
            var ps = new GameObject(name).AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = false; m.playOnAwake = false; m.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 1.5f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed); m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            m.gravityModifier = gravity; m.maxParticles = 400; m.simulationSpace = ParticleSystemSimulationSpace.World; m.startColor = c;
            var e = ps.emission; e.rateOverTime = 0;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.05f;
            var r = ps.GetComponent<ParticleSystemRenderer>(); var w = Color.white * boost; w.a = 1f; r.sharedMaterial = Gfx.Particle(w);
            return ps;
        }

        public void Shot(Vector3 from, Vector3 to, bool enemy = false)
        {
            var t = tracers[nextT]; nextT = (nextT + 1) % tracers.Count;
            t.lr.enabled = true; t.t = 0.06f;
            t.lr.SetPosition(0, from); t.lr.SetPosition(1, to);
            var col = enemy ? new Color(1f, 0.5f, 0.4f) : Color.white;
            t.lr.startColor = col; t.lr.endColor = col;
        }

        public void Muzzle(Vector3 p)
        {
            flash.transform.position = p; flash.intensity = 5f; flashT = 0.045f;
        }

        public void Impact(Vector3 p, Vector3 normal, bool flesh)
        {
            var ps = flesh ? blood : sparks;
            ps.transform.position = p; ps.transform.rotation = Quaternion.LookRotation(normal == Vector3.zero ? Vector3.up : normal) * Quaternion.Euler(-90, 0, 0);
            ps.Emit(flesh ? 14 : 9);
            if (!flesh) { dust.transform.position = p; dust.transform.rotation = ps.transform.rotation; dust.Emit(4); }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var t in tracers)
                if (t.lr.enabled) { t.t -= dt; if (t.t <= 0) t.lr.enabled = false; }
            if (flashT > 0) { flashT -= dt; if (flashT <= 0) flash.intensity = 0; }
        }
    }
}
