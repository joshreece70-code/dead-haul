using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadHaul
{
    // Entry point. Builds the whole game from code when any scene loads, so the project needs no hand-made scenes.
    public class Game : MonoBehaviour
    {
        public enum St { Menu, Raid, Result }
        public static Game I;
        public St state = St.Menu;
        public SaveData S;
        public Camera cam;
        public World world;
        public Player player;
        public readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Container> lootables = new List<Container>();
        readonly List<GameObject> tracked = new List<GameObject>();
        public float raidTime;
        public int raidKills, raidHunterKills;
        public string lastResult = "";
        public int lastValue;
        public List<string> lastLost = new List<string>();
        float zT, hunterT, menuT;
        bool slowmo;
        public const float RaidLength = 15 * 60f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            // Clear whatever the template scene contains (camera, light, volume); the game builds its own.
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Destroy(go);
            new GameObject("DeadHaul").AddComponent<Game>();
        }

        void Awake()
        {
            I = this;
            Application.runInBackground = true;
            S = SaveData.Load();
            Gfx.Init();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f; cam.farClipPlane = 400f; cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();

            Gfx.SetupLighting();
            Gfx.SetupPost(cam);
            Gfx.ApplyQuality(S.quality, cam);

            gameObject.AddComponent<Sfx>();
            gameObject.AddComponent<Fx>();
            gameObject.AddComponent<Net>();
            gameObject.AddComponent<Hud>();

            BuildWorld(Net.SeedNow());
            Hud.I.ShowMenu();
        }

        void BuildWorld(int seed)
        {
            if (world != null) Destroy(world.gameObject);
            var go = new GameObject("World");
            world = go.AddComponent<World>();
            world.Build(seed);
        }

        void ClearRaid()
        {
            foreach (var e in enemies) if (e != null) Destroy(e.gameObject);
            enemies.Clear();
            lootables.Clear();
            foreach (var t in tracked) if (t != null) Destroy(t);
            tracked.Clear();
            if (player != null) Destroy(player.gameObject);
            player = null;
        }

        public void StartRaid()
        {
            ClearRaid();
            SetSlowmo(false);
            int seed = Net.I != null ? Net.I.SharedSeed() : Net.SeedNow();
            BuildWorld(seed);
            player = Player.Create(world.spawn, S, cam);
            raidTime = RaidLength; raidKills = 0; raidHunterKills = 0;
            zT = 5f; hunterT = 150f;
            for (int i = 0; i < 34; i++) SpawnEnemy(false, 28f, 110f);
            for (int i = 0; i < 4; i++) SpawnEnemy(true, 45f, 140f);
            S.raids++; S.Save();
            state = St.Raid;
            Hud.I.ShowRaid();
            Hud.I.Banner("RAID STARTED", "Loot what you can. Extract at a green flare.", new Color(1f, 0.8f, 0.3f));
        }

        Enemy SpawnEnemy(bool hunter, float minD, float maxD)
        {
            var p = world.RoadPoint(minD, maxD, player != null ? player.transform.position : world.spawn);
            var e = Enemy.Spawn(p, hunter);
            enemies.Add(e);
            return e;
        }

        void Update()
        {
            if (state == St.Menu || state == St.Result)
            {
                menuT += Time.unscaledDeltaTime;
                float a = menuT * 0.04f;
                cam.transform.position = new Vector3(Mathf.Cos(a) * 95f, 48f, Mathf.Sin(a) * 95f);
                cam.transform.LookAt(new Vector3(0, 4f, 0));
                cam.fieldOfView = 55f;
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                return;
            }
            if (state != St.Raid) return;

            bool block = Hud.I.Blocking;
            Cursor.lockState = block ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = block;

            float dt = Time.deltaTime;
            raidTime -= dt;
            if (raidTime <= 0) { EndRaid("mia"); return; }

            enemies.RemoveAll(e => e == null);
            zT -= dt;
            if (zT <= 0)
            {
                zT = 5f;
                int alive = 0; foreach (var e in enemies) if (!e.dead && !e.hunter) alive++;
                if (alive < 40) SpawnEnemy(false, 50f, 85f);
            }
            hunterT -= dt;
            if (hunterT <= 0)
            {
                hunterT = 140f;
                int h = 0; foreach (var e in enemies) if (!e.dead && e.hunter) h++;
                if (h < 8)
                {
                    var e = SpawnEnemy(true, 60f, 90f);
                    e.Alert(player.transform.position);
                    Hud.I.Banner("HUNTER INBOUND", "Someone is tracking you", new Color(1f, 0.4f, 0.3f));
                }
            }
            // Keep the dead body count in check
            int deadN = 0;
            for (int i = enemies.Count - 1; i >= 0; i--)
                if (enemies[i].dead && ++deadN > 45) { lootables.Remove(enemies[i].GetComponent<Container>()); Destroy(enemies[i].gameObject); enemies.RemoveAt(i); }
        }

        public IEnumerable<Container> Lootables()
        {
            foreach (var c in world.containers) yield return c;
            foreach (var c in lootables) if (c != null) yield return c;
        }

        public void AddLootable(Container c) { lootables.Add(c); }
        public void Track(GameObject g) { tracked.Add(g); }

        public void Noise(Vector3 p, float radius)
        {
            float r2 = radius * radius;
            foreach (var e in enemies)
                if (e != null && !e.dead && (e.transform.position - p).sqrMagnitude < r2) e.Alert(p);
        }

        public void OnKill(Enemy e)
        {
            raidKills++; S.kills++;
            if (e.hunter) { raidHunterKills++; Hud.I.Feed("Hunter down  +250 XP", new Color(1f, 0.75f, 0.3f)); }
            else Hud.I.Feed("Infected killed  +25 XP", new Color(0.85f, 0.85f, 0.85f));
        }

        public void SetSlowmo(bool on)
        {
            slowmo = on;
            Time.timeScale = on ? 0.32f : 1f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            if (Gfx.Chroma != null) Gfx.Chroma.intensity.value = on ? 0.7f : 0f;
            if (Gfx.Grade != null) Gfx.Grade.saturation.value = on ? -40f : -6f;
        }

        public void EndRaid(string result)
        {
            if (state != St.Raid) return;
            SetSlowmo(false);
            state = St.Result;
            lastResult = result;
            lastLost.Clear();
            lastValue = 0;
            if (result == "extract")
            {
                foreach (var l in player.bag)
                {
                    if (l.cash > 0) S.cash += l.cash;
                    else if (l.SaveId != null) S.stash.Add(l.SaveId);
                    lastValue += l.Value;
                }
                S.extracts++;
                S.cash += raidKills * 25 + raidHunterKills * 225;
                Sfx.I.Play2D("extract");
            }
            else
            {
                if (result == "kia" && Net.I != null) Net.I.OnDeath();
                // Dying (or running out of time) costs the bag and the primary weapon.
                if (!string.IsNullOrEmpty(S.primary))
                {
                    lastLost.Add(Defs.Gun(S.primary).name);
                    S.owned.Remove(S.primary);
                    S.primary = "";
                }
                foreach (var l in player.bag) lastValue += l.Value;
                Sfx.I.Play2D("hurt", 1f, 0.6f);
            }
            S.Fix();
            S.Save();
            Hud.I.ShowResult();
        }

        public void BackToMenu()
        {
            ClearRaid();
            BuildWorld(Net.SeedNow());
            state = St.Menu;
            Hud.I.ShowMenu();
        }

        void OnApplicationQuit() { if (S != null) S.Save(); }
    }
}
