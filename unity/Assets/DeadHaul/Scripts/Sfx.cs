using System.Collections.Generic;
using UnityEngine;

namespace DeadHaul
{
    // Every sound is synthesised at start-up, so the project needs no audio files.
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        const int RATE = 44100;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource ui;
        readonly List<AudioSource> pool = new List<AudioSource>();
        int next;

        void Awake()
        {
            I = this;
            ui = gameObject.AddComponent<AudioSource>(); ui.spatialBlend = 0; ui.playOnAwake = false;
            for (int i = 0; i < 24; i++)
            {
                var go = new GameObject("sfx" + i); go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 3f; s.maxDistance = 90f; s.playOnAwake = false; s.dopplerLevel = 0;
                pool.Add(s);
            }
            var rng = new System.Random(5);
            clips["pistol"] = Shot(rng, 0.28f, 900f, 0.55f, 110f);
            clips["smg"] = Shot(rng, 0.16f, 1400f, 0.45f, 130f);
            clips["rifle"] = Shot(rng, 0.32f, 1100f, 0.65f, 90f);
            clips["shotgun"] = Shot(rng, 0.55f, 600f, 0.9f, 70f);
            clips["sniper"] = Shot(rng, 0.9f, 700f, 1f, 55f);
            clips["minigun"] = Shot(rng, 0.12f, 1600f, 0.4f, 140f);
            clips["swing"] = Swish(rng);
            clips["hit"] = Tone(1800f, 0.05f, 0.5f, 900f);
            clips["head"] = Tone(2600f, 0.08f, 0.6f, 1400f);
            clips["kill"] = Tone(520f, 0.18f, 0.5f, 1040f);
            clips["groan"] = Groan(rng);
            clips["pickup"] = Chime(new[] { 880f, 1320f }, 0.09f);
            clips["rare"] = Chime(new[] { 660f, 880f, 1100f, 1760f }, 0.1f);
            clips["extract"] = Chime(new[] { 523f, 659f, 784f, 1046f, 1318f }, 0.14f);
            clips["reload"] = Click(rng);
            clips["hurt"] = Thud(rng, 140f);
            clips["step"] = Thud(rng, 80f);
            clips["click"] = Tone(1200f, 0.025f, 0.3f, 1200f);
            clips["power"] = Chime(new[] { 300f, 450f, 600f, 900f, 1200f }, 0.06f);
        }

        public void Play2D(string id, float vol = 1f, float pitch = 1f)
        {
            if (!clips.TryGetValue(id, out var c)) return;
            ui.pitch = pitch; ui.PlayOneShot(c, vol);
        }

        public void PlayAt(string id, Vector3 pos, float vol = 1f, float pitch = 1f)
        {
            if (!clips.TryGetValue(id, out var c)) return;
            var s = pool[next]; next = (next + 1) % pool.Count;
            s.transform.position = pos; s.pitch = pitch * Random.Range(0.94f, 1.06f); s.volume = vol; s.clip = c; s.Play();
        }

        static AudioClip Make(string name, float[] d)
        {
            var c = AudioClip.Create(name, d.Length, 1, RATE, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip Shot(System.Random rng, float len, float crack, float boom, float thumpHz)
        {
            int n = (int)(RATE * len); var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)RATE;
                float noise = (float)rng.NextDouble() * 2 - 1;
                lp += (noise - lp) * Mathf.Clamp01(crack / RATE * 6.28f);
                float env = Mathf.Exp(-t * (7f / len));
                float thump = Mathf.Sin(t * thumpHz * 6.28f * (1f - t * 1.5f)) * Mathf.Exp(-t * 30f) * boom;
                d[i] = Mathf.Clamp((noise * 0.35f * Mathf.Exp(-t * 60f) + lp * 1.6f * env + thump) * 0.9f, -1, 1);
            }
            return Make("shot", d);
        }

        static AudioClip Swish(System.Random rng)
        {
            int n = (int)(RATE * 0.22f); var d = new float[n]; float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float noise = (float)rng.NextDouble() * 2 - 1;
                lp += (noise - lp) * (0.05f + t * 0.3f);
                d[i] = lp * Mathf.Sin(t * Mathf.PI) * 1.4f;
            }
            return Make("swish", d);
        }

        static AudioClip Tone(float f0, float len, float vol, float f1)
        {
            int n = (int)(RATE * len); var d = new float[n]; float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                ph += Mathf.Lerp(f0, f1, t) / RATE * 6.28f;
                d[i] = Mathf.Sin(ph) * vol * (1 - t);
            }
            return Make("tone", d);
        }

        static AudioClip Chime(float[] notes, float each)
        {
            int per = (int)(RATE * each); int n = per * notes.Length + RATE / 4; var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
                for (int i = 0; i < RATE / 3 && k * per + i < n; i++)
                {
                    float t = i / (float)RATE;
                    d[k * per + i] += (Mathf.Sin(t * notes[k] * 6.28f) + 0.3f * Mathf.Sin(t * notes[k] * 12.56f)) * Mathf.Exp(-t * 9f) * 0.25f;
                }
            return Make("chime", d);
        }

        static AudioClip Groan(System.Random rng)
        {
            int n = (int)(RATE * 1.1f); var d = new float[n]; float ph = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = 85f + Mathf.Sin(t * 9f) * 18f;
                ph += f / RATE * 6.28f;
                float noise = (float)rng.NextDouble() * 2 - 1; lp += (noise - lp) * 0.08f;
                d[i] = (Mathf.Sign(Mathf.Sin(ph)) * 0.25f + lp * 0.8f) * Mathf.Sin(t * Mathf.PI) * 0.6f;
            }
            return Make("groan", d);
        }

        static AudioClip Click(System.Random rng)
        {
            int n = (int)(RATE * 0.35f); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)RATE;
                float a = t < 0.02f ? 1 : (t > 0.22f && t < 0.25f) ? 1 : 0;
                d[i] = ((float)rng.NextDouble() * 2 - 1) * a * 0.5f;
            }
            return Make("click", d);
        }

        static AudioClip Thud(System.Random rng, float f)
        {
            int n = (int)(RATE * 0.18f); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)RATE;
                d[i] = (Mathf.Sin(t * f * 6.28f) * 0.7f + ((float)rng.NextDouble() * 2 - 1) * 0.15f) * Mathf.Exp(-t * 28f);
            }
            return Make("thud", d);
        }
    }
}
