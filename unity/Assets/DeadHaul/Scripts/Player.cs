using System.Collections.Generic;
using UnityEngine;

namespace DeadHaul
{
    public class Player : MonoBehaviour
    {
        public CharacterController cc;
        public Rig rig;
        public Camera cam;

        public float hp = 100, stamina = 100;
        public bool dead;
        public float yaw, pitch = 12f;
        Vector3 vel;
        public bool aiming, crouch, farView;
        public float diveT, proneT, airT;
        public readonly string[] slots = new string[3];
        public readonly int[] ammo = new int[3];
        public int cur = 1;
        float fireCd, reloadT, swingT, healT, bloom;
        public float miniT, adrenT;
        public int medkits = 2;
        public readonly List<Loot> bag = new List<Loot>();
        public const float MaxKg = 32f;
        public Container searching; public float searchT;
        public float exfilT; public Exfil atExfil;
        Vector3 camPos; float camFov = 68f; float bobT; float stepT;
        float crouchK, proneK;
        public Vector3 lastShotEnd; public int shotN;
        public bool moving;

        public float Kg { get { float k = 0; foreach (var l in bag) k += l.Kg; return k; } }
        public int BagValue { get { int v = 0; foreach (var l in bag) v += l.Value; return v; } }
        public GunDef Cur => miniT > 0 ? Defs.Minigun : Defs.Gun(slots[cur]);
        public bool Reloading => reloadT > 0;
        public float ReloadFrac(GunDef g) => g == null || g.reload <= 0 ? 0 : 1f - reloadT / g.reload;
        public bool Healing => healT > 0;
        public float HealFrac => 1f - healT / 2.5f;
        public float Spread => bloom;
        bool UiOpen => Hud.I != null && Hud.I.Blocking;

        public static Player Create(Vector3 pos, SaveData S, Camera cam)
        {
            var go = new GameObject("Player");
            go.transform.position = pos;
            var p = go.AddComponent<Player>();
            p.cam = cam;
            p.cc = go.AddComponent<CharacterController>();
            p.cc.height = 1.8f; p.cc.radius = 0.38f; p.cc.center = new Vector3(0, 0.9f, 0); p.cc.stepOffset = 0.4f; p.cc.skinWidth = 0.05f;
            p.rig = Rig.Build(go.transform, new Color(0.85f, 0.66f, 0.52f), new Color(0.22f, 0.27f, 0.2f), new Color(0.2f, 0.2f, 0.24f), new Color(0.12f, 0.14f, 0.12f));
            Gfx.SetLayer(go, Gfx.PlayerLayer);
            p.slots[0] = S.primary; p.slots[1] = S.secondary; p.slots[2] = S.melee;
            for (int i = 0; i < 3; i++) { var g = Defs.Gun(p.slots[i]); p.ammo[i] = g != null && !g.melee ? g.mag : 0; }
            p.cur = string.IsNullOrEmpty(S.primary) ? 1 : 0;
            p.yaw = Random.Range(0f, 360f);
            p.camPos = pos + Vector3.up * 3;
            return p;
        }

        void Update()
        {
            if (dead || Game.I == null || Game.I.state != Game.St.Raid) return;
            float dt = Time.deltaTime;
            bool ui = UiOpen;
            var S = Game.I.S;
            var g = Cur;

            // Look
            if (!ui)
            {
                var md = Inp.MouseDelta * 0.075f * S.sens * (aiming ? (g != null && g.id == "sniper" ? 0.35f : 0.6f) : 1f);
                yaw += md.x; pitch = Mathf.Clamp(pitch - md.y, -60f, 72f);
            }

            // Slot keys
            if (!ui)
            {
                if (Inp.Down(KeyCode.Alpha1)) Select(0);
                if (Inp.Down(KeyCode.Alpha2)) Select(1);
                if (Inp.Down(KeyCode.Alpha3)) Select(2);
                float sc = Inp.Scroll;
                if (sc != 0) { for (int k = 1; k <= 3; k++) { int n = ((cur - (int)sc * k) % 3 + 3) % 3; if (!string.IsNullOrEmpty(slots[n])) { Select(n); break; } } }
                if (Inp.Down(KeyCode.R)) StartReload();
                if (Inp.Down(KeyCode.H)) StartHeal();
                if (Inp.Down(KeyCode.C)) crouch = !crouch;
                if (Inp.Down(KeyCode.V)) farView = !farView;
                if (Inp.MouseDown(1) && g != null && !g.melee) aiming = !aiming;
                if (Inp.Down(KeyCode.E)) Interact();
            }
            if (g == null || g.melee) aiming = false;

            // Movement
            Vector2 input = Vector2.zero;
            if (!ui)
            {
                if (Inp.Held(KeyCode.W)) input.y += 1; if (Inp.Held(KeyCode.S)) input.y -= 1;
                if (Inp.Held(KeyCode.D)) input.x += 1; if (Inp.Held(KeyCode.A)) input.x -= 1;
            }
            input = Vector2.ClampMagnitude(input, 1);
            var flatRot = Quaternion.Euler(0, yaw, 0);
            Vector3 wish = flatRot * new Vector3(input.x, 0, input.y);
            moving = input.sqrMagnitude > 0.01f;
            bool sprint = !ui && Inp.Held(KeyCode.LeftShift) && input.y > 0.1f && stamina > 1 && !aiming && !crouch && healT <= 0;
            float speed = sprint ? 7.2f : crouch ? 2.5f : aiming ? 3.4f : 4.7f;
            if (adrenT > 0) speed *= 1.35f;
            if (Kg > MaxKg) speed *= 0.75f;
            if (healT > 0) speed *= 0.5f;
            if (sprint && moving) stamina = Mathf.Max(0, stamina - 17 * dt * (adrenT > 0 ? 0.3f : 1f));
            else stamina = Mathf.Min(100, stamina + 15 * dt);

            bool grounded = cc.isGrounded;
            if (grounded) airT = 0; else airT += dt;

            if (diveT > 0)
            {
                diveT -= dt;
                vel.y -= 13f * dt;
                if ((grounded && diveT < 0.65f) || diveT <= 0)
                {
                    diveT = 0; proneT = 0.75f; vel.x *= 0.25f; vel.z *= 0.25f;
                    Game.I.SetSlowmo(false);
                    Sfx.I.PlayAt("hurt", transform.position, 0.6f, 0.7f);
                }
            }
            else
            {
                if (proneT > 0) { proneT -= dt; speed = 0.6f; }
                float accel = grounded ? 14f : 3f;
                Vector3 h = new Vector3(vel.x, 0, vel.z);
                h = Vector3.MoveTowards(h, wish * speed, accel * speed * dt);
                vel.x = h.x; vel.z = h.z;
                if (grounded)
                {
                    if (vel.y < 0) vel.y = -2f;
                    if (!ui && Inp.Down(KeyCode.Space) && proneT <= 0 && stamina > 6)
                    {
                        if (crouch) crouch = false;
                        else { vel.y = 6.4f; stamina -= 6; }
                    }
                    if (!ui && Inp.Down(KeyCode.Z) && proneT <= 0 && stamina > 15)
                    {
                        Vector3 dir = moving ? wish.normalized : flatRot * Vector3.forward;
                        vel = dir * 10.5f + Vector3.up * 4.6f;
                        diveT = 0.95f; crouch = false; stamina -= 15;
                        Game.I.SetSlowmo(true);
                    }
                }
                vel.y -= 20f * dt;
            }
            cc.Move(vel * dt);
            if ((cc.collisionFlags & CollisionFlags.Above) != 0 && vel.y > 0) vel.y = 0;

            crouchK = Mathf.MoveTowards(crouchK, crouch ? 1 : 0, dt * 6);
            proneK = Mathf.MoveTowards(proneK, diveT > 0 || proneT > 0 ? 1 : 0, dt * (diveT > 0 ? 9 : 5));
            float hgt = Mathf.Lerp(1.8f, 1.2f, crouchK);
            hgt = Mathf.Lerp(hgt, 0.9f, proneK);
            cc.height = hgt; cc.center = new Vector3(0, hgt / 2, 0);
            transform.rotation = Quaternion.Euler(0, yaw, 0);

            // Footsteps
            float hs = new Vector2(vel.x, vel.z).magnitude;
            if (grounded && hs > 1.5f) { stepT -= dt * hs; if (stepT <= 0) { stepT = 2.4f; Sfx.I.PlayAt("step", transform.position, crouch ? 0.15f : 0.35f, Random.Range(0.8f, 1.1f)); } }

            // Timers
            fireCd -= dt; swingT = Mathf.Max(0, swingT - dt * 3f);
            bloom = Mathf.MoveTowards(bloom, 0, dt * 6f);
            if (miniT > 0) { miniT -= dt; if (miniT <= 0) Hud.I.Feed("Minigun spent", Color.gray); }
            if (adrenT > 0) adrenT -= dt;
            if (reloadT > 0)
            {
                reloadT -= dt;
                if (reloadT <= 0) { var rg = Defs.Gun(slots[cur]); if (rg != null) ammo[cur] = rg.mag; }
            }
            if (healT > 0)
            {
                healT -= dt;
                if (healT <= 0) { hp = Mathf.Min(100, hp + 55); medkits--; Sfx.I.Play2D("pickup"); Hud.I.Feed("+55 health", new Color(0.4f, 1f, 0.5f)); }
            }

            // Fire
            if (!ui && g != null && healT <= 0)
            {
                bool want = g.auto ? Inp.MouseHeld(0) : Inp.MouseDown(0);
                if (want && fireCd <= 0) Fire(g);
            }

            UpdateSearch(dt);
            UpdateExfil(dt);

            rig.SetGun(g != null ? g.id : null);
            rig.Animate(dt, hs, aiming || fireCd > -0.6f, g != null && g.melee, swingT, crouchK, proneK);
        }

        void LateUpdate()
        {
            if (Game.I == null || Game.I.state != Game.St.Raid || cam == null) return;
            float dt = Time.unscaledDeltaTime;
            var g = Cur;
            bool scoped = aiming && g != null && g.id == "sniper";
            float dist = aiming ? (scoped ? 1.2f : 1.75f) : farView ? 7.5f : 3.7f;
            float side = aiming ? 0.55f : 0.7f;
            float hs = new Vector2(vel.x, vel.z).magnitude;
            bobT += Time.deltaTime * hs * 1.8f;
            float bob = cc.isGrounded ? Mathf.Sin(bobT) * Mathf.Clamp01(hs / 5f) * 0.05f : 0f;
            float sway = Mathf.Cos(bobT * 0.5f) * Mathf.Clamp01(hs / 5f) * 0.035f;

            var rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 pivot = transform.position + Vector3.up * (1.62f - crouchK * 0.5f - proneK * 1.05f + bob);
            Vector3 shoulder = pivot + rot * new Vector3(side + sway, 0.12f, 0);
            Vector3 want = shoulder - rot * Vector3.forward * dist;
            Vector3 dir = want - pivot;
            float len = dir.magnitude;
            if (Physics.SphereCast(pivot, 0.22f, dir / Mathf.Max(len, 0.001f), out var hit, len, Gfx.WorldMask, QueryTriggerInteraction.Ignore))
                want = pivot + dir / len * Mathf.Max(0.2f, hit.distance - 0.05f);
            camPos = Vector3.Lerp(camPos, want, 1f - Mathf.Exp(-dt * 22f));
            if ((camPos - want).sqrMagnitude > 25f) camPos = want;
            cam.transform.position = camPos;
            cam.transform.rotation = rot * Quaternion.Euler(0, 0, -sway * 20f);
            float fovWant = scoped ? 22f : aiming ? 50f : 68f;
            if (diveT > 0) fovWant += 6f;
            camFov = Mathf.Lerp(camFov, fovWant, 1f - Mathf.Exp(-dt * 12f));
            cam.fieldOfView = camFov;
        }

        public void Select(int i)
        {
            if (miniT > 0 || string.IsNullOrEmpty(slots[i]) || i == cur) return;
            cur = i; reloadT = 0; fireCd = Mathf.Max(fireCd, 0.25f); aiming = false;
            Sfx.I.Play2D("click", 0.6f);
        }

        void StartReload()
        {
            var g = Defs.Gun(slots[cur]);
            if (miniT > 0 || g == null || g.melee || reloadT > 0 || ammo[cur] >= g.mag) return;
            reloadT = g.reload; aiming = false;
            Sfx.I.PlayAt("reload", transform.position, 0.8f);
        }

        void StartHeal()
        {
            if (medkits <= 0 || hp >= 100 || healT > 0) return;
            healT = 2.5f; reloadT = 0;
        }

        Vector3 AimPoint(out Collider hitCol)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            float skip = Vector3.Distance(cam.transform.position, transform.position + Vector3.up * 1.4f);
            hitCol = null;
            var hits = Physics.RaycastAll(ray, 400f, Gfx.ShotMask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue; Vector3 p = ray.GetPoint(300f);
            foreach (var h in hits)
            {
                if (h.distance < skip * 0.85f) continue;
                if (h.distance < best) { best = h.distance; p = h.point; hitCol = h.collider; }
            }
            return p;
        }

        void Fire(GunDef g)
        {
            if (g.melee) { Melee(g); return; }
            if (miniT <= 0)
            {
                if (reloadT > 0) return;
                if (ammo[cur] <= 0) { StartReload(); fireCd = 0.2f; return; }
                ammo[cur]--;
            }
            fireCd = 60f / g.rpm;
            Vector3 aim = AimPoint(out _);
            Vector3 origin = rig.muzzle != null ? rig.muzzle.position : transform.position + Vector3.up * 1.4f;
            // If the muzzle is inside cover (hugging a wall), fire from the chest so the shot still goes where you look.
            Vector3 chest = transform.position + Vector3.up * (1.35f - crouchK * 0.45f - proneK * 1f);
            if (Physics.Linecast(chest, origin, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) origin = chest;
            Vector3 baseDir = (aim - origin).normalized;
            float spread = g.spread * (aiming ? 0.45f : 1f) * (moving ? 1.35f : 1f) * (crouch ? 0.8f : 1f) + bloom;
            if (diveT > 0) spread *= 0.6f;
            for (int i = 0; i < g.pellets; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), baseDir) * Quaternion.AngleAxis(Random.Range(0f, spread), Vector3.Cross(baseDir, Vector3.up).normalized) * baseDir;
                Vector3 end = origin + dir * g.range;
                if (Physics.Raycast(origin, dir, out var hit, g.range, Gfx.ShotMask, QueryTriggerInteraction.Collide))
                {
                    end = hit.point;
                    var e = hit.collider.GetComponentInParent<Enemy>();
                    var av = hit.collider.GetComponentInParent<RemoteAvatar>();
                    float falloff = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(hit.distance / g.range));
                    if (e != null && !e.dead)
                    {
                        bool head = hit.point.y > e.HeadY;
                        bool killed = e.Hit(g.dmg * falloff * (head ? 2.2f : 1f), dir);
                        Hud.I.Hitmarker(head, killed);
                        Hud.I.Pop(hit.point, Mathf.RoundToInt(g.dmg * falloff * (head ? 2.2f : 1f)).ToString(), head ? new Color(1f, 0.85f, 0.2f) : Color.white);
                        Fx.I.Impact(hit.point, hit.normal, true);
                    }
                    else if (av != null)
                    {
                        bool head = hit.point.y > av.transform.position.y + 1.5f;
                        float d = g.dmg * falloff * (head ? 2f : 1f);
                        if (Net.I != null && Net.I.Hit(av.id, d)) { Hud.I.Hitmarker(head, false); Hud.I.Pop(hit.point, Mathf.RoundToInt(d).ToString(), Color.white); }
                        Fx.I.Impact(hit.point, hit.normal, true);
                    }
                    else Fx.I.Impact(hit.point, hit.normal, false);
                }
                Fx.I.Shot(origin, end);
                lastShotEnd = end;
            }
            shotN++;
            Fx.I.Muzzle(origin);
            string snd = g.id == "minigun" ? "minigun" : g.id == "sniper" ? "sniper" : g.pellets > 1 ? "shotgun" : g.id == "smg" || g.id == "uzis" ? "smg" : g.slot == Slot.Primary ? "rifle" : "pistol";
            Sfx.I.PlayAt(snd, origin, 0.9f);
            Game.I.Noise(transform.position, g.id == "sniper" ? 70f : 45f);
            bloom = Mathf.Min(bloom + g.recoil * 0.5f, 5f);
            pitch -= g.recoil * (aiming ? 0.45f : 0.7f) * (crouch ? 0.7f : 1f);
            yaw += Random.Range(-1f, 1f) * g.recoil * 0.25f;
            Hud.I.Kick(g.recoil);
        }

        void Melee(GunDef g)
        {
            fireCd = 60f / g.rpm; swingT = 1f;
            Sfx.I.PlayAt("swing", transform.position, 0.8f, g.id == "saber" ? 0.7f : 1f);
            Vector3 fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var cols = Physics.OverlapSphere(transform.position + Vector3.up * 1f + fwd * (g.range * 0.5f), g.range * 0.65f, 1 << Gfx.EnemyLayer, QueryTriggerInteraction.Collide);
            var done = new HashSet<Object>();
            foreach (var c in cols)
            {
                var e = c.GetComponentInParent<Enemy>();
                if (e != null && !e.dead && done.Add(e))
                {
                    Vector3 to = e.transform.position - transform.position; to.y = 0;
                    if (Vector3.Angle(fwd, to) > 75f && to.magnitude > 1.2f) continue;
                    bool killed = e.Hit(g.dmg, fwd);
                    Hud.I.Hitmarker(false, killed);
                    Hud.I.Pop(e.transform.position + Vector3.up * 1.8f, g.dmg.ToString("0"), Color.white);
                    Fx.I.Impact(e.transform.position + Vector3.up * 1.2f, -fwd, true);
                }
                var av = c.GetComponentInParent<RemoteAvatar>();
                if (av != null && done.Add(av) && Net.I != null && Net.I.Hit(av.id, g.dmg)) Hud.I.Hitmarker(false, false);
            }
            Game.I.Noise(transform.position, 10f);
        }

        Container Nearest()
        {
            Container best = null; float bd = 2.8f;
            Vector3 fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            foreach (var c in Game.I.Lootables())
            {
                if (c == null) continue;
                Vector3 to = c.transform.position - transform.position; to.y = 0;
                float d = to.magnitude;
                if (d < bd && (d < 1.2f || Vector3.Dot(fwd, to.normalized) > 0.2f)) { bd = d; best = c; }
            }
            return best;
        }

        public Container Facing => Game.I != null && Game.I.state == Game.St.Raid ? Nearest() : null;

        void Interact()
        {
            if (searching != null) { searching = null; return; }
            var c = Nearest();
            if (c == null) return;
            if (c.searched) Hud.I.OpenLoot(c);
            else { searching = c; searchT = 0; }
        }

        void UpdateSearch(float dt)
        {
            if (searching == null) return;
            if (searching == null || Vector3.Distance(searching.transform.position, transform.position) > 3.3f || healT > 0) { searching = null; return; }
            searchT += dt;
            if (searchT >= searching.searchTime)
            {
                searching.searched = true;
                bool rare = false; foreach (var l in searching.items) if (l.Rarity >= 3) rare = true;
                Sfx.I.Play2D(rare ? "rare" : "pickup", 0.7f);
                Hud.I.OpenLoot(searching);
                searching = null;
            }
        }

        void UpdateExfil(float dt)
        {
            atExfil = null;
            foreach (var ex in Game.I.world.exfils)
                if (Vector3.Distance(new Vector3(ex.pos.x, transform.position.y, ex.pos.z), transform.position) < 4.5f) atExfil = ex;
            if (atExfil != null)
            {
                exfilT += dt;
                if (exfilT >= 6f) Game.I.EndRaid("extract");
            }
            else exfilT = 0;
        }

        public bool Take(Loot l)
        {
            if (Kg + l.Kg > MaxKg + 8f) { Hud.I.Feed("Bag is too heavy", new Color(1f, 0.5f, 0.4f)); return false; }
            bag.Add(l);
            Sfx.I.Play2D(l.Rarity >= 3 ? "rare" : "pickup", 0.6f);
            return true;
        }

        public void Hurt(float d, Vector3 from)
        {
            if (dead || Game.I.state != Game.St.Raid) return;
            if (proneT > 0 || diveT > 0) d *= 0.7f;
            hp -= d;
            Hud.I.Damage(d, from);
            Sfx.I.Play2D("hurt", 0.7f);
            if (hp <= 0) { hp = 0; dead = true; Game.I.EndRaid("kia"); }
        }

        public void PowerUp(int kind)
        {
            if (kind == 0) { miniT = 15f; aiming = false; reloadT = 0; Hud.I.Banner("MINIGUN", "15 seconds. Go.", new Color(1f, 0.6f, 0.2f)); }
            else if (kind == 1) { hp = 100; medkits++; Hud.I.Banner("FIELD MEDIC", "Full health and a medkit", new Color(0.4f, 1f, 0.5f)); }
            else { adrenT = 20f; stamina = 100; Hud.I.Banner("ADRENALINE", "20 seconds of speed", new Color(0.4f, 0.8f, 1f)); }
            Sfx.I.Play2D("power");
        }
    }
}
