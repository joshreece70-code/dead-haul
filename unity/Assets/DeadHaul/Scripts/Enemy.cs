using UnityEngine;

namespace DeadHaul
{
    // Infected chase and claw; hunters keep their distance and shoot in bursts.
    public class Enemy : MonoBehaviour
    {
        public bool hunter, runner;
        public float hp, maxHp;
        public bool dead;
        CharacterController cc;
        Rig rig;
        Vector3 vel, wanderTo, lastKnown, moveDir;
        float thinkT, attackCd, attackAnim, alertT, reactT, fireCd, groanT, losT, stuckT, deadT, strafeT;
        int burst; bool los, aggro; float steerSide = 1f; Vector3 stuckPos;

        public float HeadY => transform.position.y + 1.5f;

        public static Enemy Spawn(Vector3 p, bool hunter)
        {
            var go = new GameObject(hunter ? "Hunter" : "Infected");
            go.transform.position = p + Vector3.up * 0.1f;
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var e = go.AddComponent<Enemy>();
            e.hunter = hunter;
            e.cc = go.AddComponent<CharacterController>();
            e.cc.height = 1.8f; e.cc.radius = 0.36f; e.cc.center = new Vector3(0, 0.9f, 0); e.cc.stepOffset = 0.4f;
            if (hunter)
            {
                e.rig = Rig.Build(go.transform, new Color(0.7f, 0.55f, 0.45f), new Color(0.16f, 0.17f, 0.15f), new Color(0.13f, 0.13f, 0.14f), new Color(0.08f, 0.08f, 0.08f));
                e.rig.SetGun(Random.value < 0.5f ? "ak" : "rifle");
                e.maxHp = 130f;
            }
            else
            {
                float k = Random.Range(0.75f, 1f);
                var skin = new Color(0.48f * k, 0.55f * k, 0.42f * k);
                var shirt = Color.HSVToRGB(Random.value, Random.Range(0.2f, 0.5f), Random.Range(0.25f, 0.5f));
                var pants = Color.HSVToRGB(Random.value, 0.2f, Random.Range(0.15f, 0.35f));
                e.rig = Rig.Build(go.transform, skin, shirt, pants, skin * 0.6f);
                e.runner = Random.value < 0.22f;
                e.maxHp = e.runner ? 45f : 70f;
            }
            e.hp = e.maxHp;
            Gfx.SetLayer(go, Gfx.EnemyLayer);
            e.wanderTo = go.transform.position;
            e.groanT = Random.Range(2f, 8f);
            return e;
        }

        public void Alert(Vector3 p)
        {
            if (dead) return;
            lastKnown = p; alertT = 9f;
            if (hunter && !aggro) { aggro = true; reactT = 1.1f; }
        }

        void Update()
        {
            if (Game.I == null || Game.I.state != Game.St.Raid) return;
            float dt = Time.deltaTime;
            if (dead) { DeadUpdate(dt); return; }
            var P = Game.I.player;
            if (P == null) return;
            Vector3 to = P.transform.position - transform.position; to.y = 0;
            float dist = to.magnitude;

            losT -= dt;
            if (losT <= 0)
            {
                losT = 0.3f;
                los = dist < (hunter ? 55f : 30f) && !Physics.Linecast(transform.position + Vector3.up * 1.5f, P.transform.position + Vector3.up * 1.3f, Gfx.WorldMask, QueryTriggerInteraction.Ignore);
            }
            alertT -= dt;
            float speed = 0; Vector3 want = Vector3.zero; bool faceTarget = false;

            if (!hunter)
            {
                bool chase = (los && dist < (P.crouch ? 11f : 19f)) || alertT > 0;
                if (los && dist < 19f) { lastKnown = P.transform.position; if (alertT < 2f) alertT = 2f; }
                if (chase)
                {
                    Vector3 tgt = los ? P.transform.position : lastKnown;
                    want = tgt - transform.position; want.y = 0;
                    speed = runner ? 5.3f : 3.3f;
                    if (want.magnitude < 0.8f && !los) alertT = 0;
                }
                else
                {
                    thinkT -= dt;
                    if (thinkT <= 0) { thinkT = Random.Range(3f, 7f); wanderTo = transform.position + new Vector3(Random.Range(-9f, 9f), 0, Random.Range(-9f, 9f)); }
                    want = wanderTo - transform.position; want.y = 0;
                    speed = want.magnitude > 0.6f ? 0.9f : 0f;
                }
                attackCd -= dt;
                if (attackAnim > 0)
                {
                    float before = attackAnim;
                    attackAnim -= dt * 2.2f;
                    if (before > 0.55f && attackAnim <= 0.55f && dist < 1.9f) P.Hurt(runner ? 7f : 10f, transform.position);
                }
                if (dist < 1.45f && attackCd <= 0) { attackCd = 1.2f; attackAnim = 1f; speed = 0; }
                if (dist < 1.1f) speed = 0;
                groanT -= dt;
                if (groanT <= 0) { groanT = Random.Range(5f, 12f); if (dist < 35f) Sfx.I.PlayAt("groan", transform.position + Vector3.up * 1.6f, chase ? 0.55f : 0.3f, Random.Range(0.8f, 1.15f)); }
            }
            else
            {
                if (los && dist < 48f && !aggro) { aggro = true; reactT = 1.2f; }
                if (los) lastKnown = P.transform.position;
                if (aggro)
                {
                    faceTarget = los;
                    Vector3 toK = lastKnown - transform.position; toK.y = 0;
                    if (!los || dist > 26f) { want = toK; speed = 3.6f; }
                    else if (dist < 11f) { want = -to; speed = 3f; }
                    else
                    {
                        strafeT -= dt;
                        if (strafeT <= 0) { strafeT = Random.Range(1.2f, 3f); steerSide = Random.value < 0.5f ? -1 : 1; }
                        want = Vector3.Cross(Vector3.up, to.normalized) * steerSide; speed = 2.2f;
                    }
                    reactT -= dt; fireCd -= dt;
                    if (los && reactT <= 0 && fireCd <= 0 && dist < 60f) Shoot(P, dist);
                }
                else
                {
                    thinkT -= dt;
                    if (thinkT <= 0) { thinkT = Random.Range(4f, 9f); wanderTo = transform.position + new Vector3(Random.Range(-14f, 14f), 0, Random.Range(-14f, 14f)); }
                    want = wanderTo - transform.position; want.y = 0;
                    speed = want.magnitude > 0.6f ? 1.6f : 0f;
                }
            }

            Steer(want, speed, dt);
            vel.y = cc.isGrounded ? -2f : vel.y - 20f * dt;
            cc.Move((moveDir * speed + Vector3.up * vel.y) * dt);

            Vector3 face = faceTarget ? to : moveDir;
            if (!hunter && dist < 3f) face = to;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-dt * 8f));

            if (hunter) rig.Animate(dt, speed, aggro, false, 0, 0, 0);
            else rig.AnimateInfected(dt, speed, attackAnim);
        }

        // Head for want; if a wall is in the way, slide around it.
        void Steer(Vector3 want, float speed, float dt)
        {
            if (want.sqrMagnitude < 0.01f || speed <= 0) { moveDir = Vector3.zero; return; }
            Vector3 d = want.normalized;
            Vector3 o = transform.position + Vector3.up * 0.9f;
            if (Physics.SphereCast(o, 0.35f, d, out _, 1.6f, Gfx.WorldMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 a = Quaternion.Euler(0, 70f * steerSide, 0) * d;
                if (!Physics.SphereCast(o, 0.35f, a, out _, 1.6f, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) d = a;
                else
                {
                    Vector3 b = Quaternion.Euler(0, -70f * steerSide, 0) * d;
                    if (!Physics.SphereCast(o, 0.35f, b, out _, 1.6f, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) { d = b; steerSide = -steerSide; }
                    else d = Quaternion.Euler(0, 120f * steerSide, 0) * d;
                }
            }
            stuckT += dt;
            if (stuckT > 1.2f)
            {
                if ((transform.position - stuckPos).sqrMagnitude < 0.3f) steerSide = -steerSide;
                stuckT = 0; stuckPos = transform.position;
            }
            moveDir = Vector3.Slerp(moveDir.sqrMagnitude < 0.01f ? d : moveDir, d, 1f - Mathf.Exp(-dt * 10f));
            moveDir.y = 0;
        }

        void Shoot(Player P, float dist)
        {
            burst++;
            fireCd = burst % 3 == 0 ? Random.Range(1.4f, 2.2f) : 0.16f;
            Vector3 from = rig.muzzle != null ? rig.muzzle.position : transform.position + Vector3.up * 1.4f;
            float chance = 0.5f - dist / 130f - (P.moving ? 0.14f : 0f) - (P.crouch ? 0.08f : 0f) - (P.diveT > 0 || P.proneT > 0 ? 0.2f : 0f);
            chance = Mathf.Clamp(chance, 0.06f, 0.55f);
            Vector3 target = P.transform.position + Vector3.up * 1.2f;
            if (Random.value < chance) P.Hurt(8f, transform.position);
            else target += new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.3f, 1.2f), Random.Range(-1.5f, 1.5f));
            Fx.I.Shot(from, target, true);
            Fx.I.Muzzle(from);
            Sfx.I.PlayAt("rifle", from, 0.8f, 0.92f);
        }

        // Returns true if this hit killed it.
        public bool Hit(float dmg, Vector3 dir)
        {
            if (dead) return false;
            hp -= dmg;
            Alert(Game.I.player != null ? Game.I.player.transform.position : transform.position);
            if (hunter) reactT = Mathf.Min(reactT, 0.4f);
            if (hp > 0) { cc.Move(dir.normalized * 0.12f); return false; }
            Die(dir);
            return true;
        }

        void Die(Vector3 dir)
        {
            dead = true; deadT = 0;
            cc.enabled = false;
            Sfx.I.PlayAt("kill", transform.position, 0.6f);
            Game.I.OnKill(this);
            var c = gameObject.AddComponent<Container>();
            c.Init(hunter ? ContainerKind.HunterBody : ContainerKind.Body, Random.Range(0, int.MaxValue));
            Game.I.AddLootable(c);
            if (!hunter && Random.value < 0.07f) PowerUp.Spawn(transform.position + Vector3.up * 0.8f, Random.Range(0, 3));
            if (hunter && Random.value < 0.25f) PowerUp.Spawn(transform.position + Vector3.up * 0.8f, 1);
            var flat = new Vector3(dir.x, 0, dir.z);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
        }

        void DeadUpdate(float dt)
        {
            if (deadT >= 1f) return;
            deadT = Mathf.Min(1f, deadT + dt * 3.5f);
            float k = 1f - (1f - deadT) * (1f - deadT);
            rig.root.localRotation = Quaternion.Euler(k * 88f, 0, 0);
            rig.root.localPosition = new Vector3(0, k * 0.2f, 0);
        }
    }

    public class PowerUp : MonoBehaviour
    {
        public int kind;
        float t;
        static readonly Color[] Cols = { new Color(5f, 2f, 0.4f), new Color(0.6f, 5f, 1f), new Color(0.6f, 2.2f, 5f) };

        public static void Spawn(Vector3 p, int kind)
        {
            var go = Gfx.Prim(PrimitiveType.Cube, null, p, Vector3.one * 0.45f, Gfx.Glow(Cols[kind]));
            go.name = "PowerUp";
            var pu = go.AddComponent<PowerUp>(); pu.kind = kind;
            var l = new GameObject("glow").AddComponent<Light>();
            l.transform.SetParent(go.transform, false); l.type = LightType.Point; l.range = 5f; l.intensity = 3f;
            l.color = new Color(Cols[kind].r, Cols[kind].g, Cols[kind].b) / 5f; l.shadows = LightShadows.None;
            Game.I.Track(go);
        }

        void Update()
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Euler(t * 40f, t * 90f, 0);
            var P = Game.I != null ? Game.I.player : null;
            if (P == null || P.dead) return;
            if (Vector3.Distance(P.transform.position + Vector3.up * 0.8f, transform.position) < 1.6f) { P.PowerUp(kind); Destroy(gameObject); }
            if (t > 60f) Destroy(gameObject);
        }
    }
}
