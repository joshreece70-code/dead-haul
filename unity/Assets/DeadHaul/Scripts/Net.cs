using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DeadHaul
{
    // Multiplayer through the same Cloudflare server as the browser game.
    // Each player sends its own state; the server relays everyone's latest state to the room.
    // Unity players use their own rooms (dhu-...) because the Unity map differs from the browser map.
    public class Net : MonoBehaviour
    {
        public static Net I;
        public const string Server = "wss://dead-haul.joshreece70.workers.dev/room/";

        public string status = "Offline";
        public bool connected;
        public string code = "";
        public string myId;
        public int online;

        ClientWebSocket ws;
        CancellationTokenSource cts;
        int gen;
        bool sending;
        float sendT;
        string lastSent = "";

        class Peer
        {
            public string id, nick = "Raider";
            public Dictionary<string, object> p;
            public RemoteAvatar av;
            public double dmSeen = -1, kbSeen = -1, shotSeen = -1;
        }
        readonly Dictionary<string, Peer> peers = new Dictionary<string, Peer>();
        readonly Dictionary<string, double> dm = new Dictionary<string, double>();
        string lastHitBy; float lastHitT;
        int kbN; string kbTarget;
        public IEnumerable<RemoteAvatar> Avatars { get { foreach (var p in peers.Values) if (p.av != null && p.av.gameObject.activeSelf) yield return p.av; } }

        public static int SeedNow() => (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1800000L);

        void Awake() { I = this; }

        void Start() { Join(Game.I.S.lobby); }

        public void Join(string c)
        {
            c = (c ?? "").ToLowerInvariant();
            var sb = new StringBuilder(); foreach (char ch in c) if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
            code = sb.ToString(); if (code.Length > 12) code = code.Substring(0, 12);
            Game.I.S.lobby = code; Game.I.S.Save();
            Close();
            foreach (var p in peers.Values) if (p.av != null) Destroy(p.av.gameObject);
            peers.Clear(); dm.Clear(); online = 0; myId = null; lastSent = "";
            int g = ++gen;
            Run(code == "" ? "dhu-open" : "dhu-" + code, g);
        }

        void Close()
        {
            try { cts?.Cancel(); } catch { }
            try { ws?.Abort(); ws?.Dispose(); } catch { }
            ws = null; connected = false;
        }

        async void Run(string room, int g)
        {
            int tries = 0;
            while (g == gen && this != null)
            {
                status = "Connecting...";
                var sock = new ClientWebSocket();
                var token = new CancellationTokenSource();
                ws = sock; cts = token;
                try
                {
                    await sock.ConnectAsync(new Uri(Server + room), token.Token);
                    if (g != gen) { sock.Abort(); return; }
                    connected = true; tries = 0;
                    status = code == "" ? "Online (public lobby)" : "Online, lobby " + code.ToUpperInvariant();
                    var buf = new byte[16384];
                    var msg = new StringBuilder();
                    while (sock.State == WebSocketState.Open && g == gen)
                    {
                        var r = await sock.ReceiveAsync(new ArraySegment<byte>(buf), token.Token);
                        if (r.MessageType == WebSocketMessageType.Close) break;
                        msg.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                        if (r.EndOfMessage) { if (g == gen) { try { Handle(msg.ToString()); } catch (Exception he) { Debug.LogWarning("Dead Haul net message: " + he.Message); } } msg.Clear(); }
                    }
                }
                catch (Exception e)
                {
                    if (g == gen) Debug.Log("Dead Haul net: " + e.Message);
                }
                connected = false;
                try { sock.Dispose(); } catch { }
                if (g != gen) return;
                tries++;
                status = "Server unreachable, retrying...";
                foreach (var p in peers.Values) if (p.av != null) Destroy(p.av.gameObject);
                peers.Clear(); online = 0;
                await Task.Delay(Mathf.Min(8000, 600 * tries));
            }
        }

        void Handle(string text)
        {
            var d = Json.Parse(text) as Dictionary<string, object>;
            if (d == null) return;
            string t = Json.Str(d, "t");
            if (t == "hi") { myId = Json.Str(d, "id"); lastSent = ""; sendT = 0; return; }
            if (t != "s") return;
            var all = Json.Obj(d, "p");
            if (all == null) return;
            var seen = new HashSet<string>();
            int n = 0;
            foreach (var kv in all)
            {
                if (kv.Key == myId) continue;
                var pr = kv.Value as Dictionary<string, object>;
                if (pr == null || Json.Str(pr, "nk") == null) continue;
                seen.Add(kv.Key); n++;
                if (!peers.TryGetValue(kv.Key, out var peer)) { peer = new Peer { id = kv.Key }; peers[kv.Key] = peer; }
                peer.p = pr;
                var nk = Json.Str(pr, "nk"); peer.nick = string.IsNullOrEmpty(nk) ? "Raider" : (nk.Length > 14 ? nk.Substring(0, 14) : nk);
                Apply(peer);
            }
            var gone = new List<string>();
            foreach (var k in peers.Keys) if (!seen.Contains(k)) gone.Add(k);
            foreach (var k in gone) { if (peers[k].av != null) Destroy(peers[k].av.gameObject); peers.Remove(k); dm.Remove(k); }
            online = n;
        }

        void Apply(Peer peer)
        {
            var pr = peer.p;
            var G = Game.I;
            bool live = G.state == Game.St.Raid && G.world != null && Json.Num(pr, "r") == 1 && (int)Json.Num(pr, "sd", -1) == G.world.seed;
            if (live)
            {
                if (peer.av == null) peer.av = RemoteAvatar.Create(peer.id);
                var av = peer.av;
                av.gameObject.SetActive(true);
                av.nick = peer.nick;
                av.target = new Vector3((float)Json.Num(pr, "x"), (float)Json.Num(pr, "y"), (float)Json.Num(pr, "z"));
                av.targetYaw = (float)Json.Num(pr, "yaw") * Mathf.Rad2Deg;
                av.moving = Json.Num(pr, "mv") == 1;
                av.pvp = Json.Num(pr, "pv", 1) != 0;
                var w = Json.Str(pr, "w"); if (w != null && Defs.Gun(w) != null) av.rig.SetGun(w);

                // Their shots: f = [shot number, end x, end y, end z]
                if (pr.TryGetValue("f", out var fo) && fo is List<object> f && f.Count == 4 && f[0] is double fn)
                {
                    if (peer.shotSeen >= 0 && fn != peer.shotSeen)
                    {
                        var end = new Vector3(f[1] is double a1 ? (float)a1 : 0f, f[2] is double a2 ? (float)a2 : 0f, f[3] is double a3 ? (float)a3 : 0f);
                        var from = av.rig.muzzle != null ? av.rig.muzzle.position : av.transform.position + Vector3.up * 1.4f;
                        Fx.I.Shot(from, end, true);
                        Sfx.I.PlayAt("rifle", from, 0.7f);
                    }
                    peer.shotSeen = fn;
                }
                // Damage they've dealt to me, as a running total.
                double mine = myId != null ? Json.Num(Json.Obj(pr, "dm"), myId, 0) : 0;
                if (peer.dmSeen >= 0 && mine > peer.dmSeen && G.player != null && G.S.pvp && av.pvp)
                {
                    float delta = Mathf.Min((float)(mine - peer.dmSeen), 150f);
                    lastHitBy = peer.id; lastHitT = Time.time;
                    G.player.Hurt(delta, av.transform.position);
                }
                peer.dmSeen = mine;
            }
            else if (peer.av != null) peer.av.gameObject.SetActive(false);

            var kb = Json.Obj(pr, "kb");
            if (kb != null)
            {
                double n = Json.Num(kb, "n", -1);
                if (peer.kbSeen >= 0 && n != peer.kbSeen && Json.Str(kb, "p") == myId && G.state == Game.St.Raid)
                {
                    Hud.I.Banner("RAIDER DOWN", peer.nick + " is out of the raid", new Color(1f, 0.8f, 0.3f));
                    Sfx.I.Play2D("extract");
                }
                peer.kbSeen = n;
            }
            else if (peer.kbSeen < 0) peer.kbSeen = 0;
        }

        // Returns true if the hit counts (PvP on for both sides).
        public bool Hit(string id, float dmg)
        {
            if (!Game.I.S.pvp || !peers.TryGetValue(id, out var peer) || peer.av == null || !peer.av.pvp) return false;
            dm.TryGetValue(id, out var cur);
            dm[id] = Math.Round(cur + dmg);
            sendT = 0;
            return true;
        }

        public void OnDeath()
        {
            if (lastHitBy != null && Time.time - lastHitT < 6f) { kbN++; kbTarget = lastHitBy; sendT = 0; }
        }

        public int SharedSeed()
        {
            foreach (var p in peers.Values)
                if (p.p != null && Json.Num(p.p, "r") == 1 && p.p.TryGetValue("sd", out var sd) && sd is double s) return (int)s;
            return SeedNow();
        }

        void Update()
        {
            if (!connected || ws == null || myId == null || sending) return;
            sendT -= Time.unscaledDeltaTime;
            if (sendT > 0) return;
            sendT = 0.066f;
            var G = Game.I; var P = G.player;
            var sb = new StringBuilder("{\"t\":\"p\",\"p\":{");
            sb.Append("\"nk\":").Append(Json.Q(G.S.nick)).Append(",\"pv\":").Append(G.S.pvp ? 1 : 0);
            if (G.state == Game.St.Raid && P != null && !P.dead)
            {
                var pos = P.transform.position;
                sb.Append(",\"r\":1,\"sd\":").Append(G.world.seed)
                  .Append(",\"x\":").Append(Json.N(pos.x)).Append(",\"y\":").Append(Json.N(pos.y)).Append(",\"z\":").Append(Json.N(pos.z))
                  .Append(",\"yaw\":").Append(Json.N(P.yaw * Mathf.Deg2Rad))
                  .Append(",\"w\":").Append(Json.Q(P.Cur != null ? P.Cur.id : "pistol"))
                  .Append(",\"mv\":").Append(P.moving ? 1 : 0)
                  .Append(",\"f\":[").Append(P.shotN).Append(',').Append(Json.N(P.lastShotEnd.x)).Append(',').Append(Json.N(P.lastShotEnd.y)).Append(',').Append(Json.N(P.lastShotEnd.z)).Append(']');
            }
            else sb.Append(",\"r\":0");
            sb.Append(",\"dm\":{");
            bool first = true;
            foreach (var kv in dm) { if (!first) sb.Append(','); first = false; sb.Append(Json.Q(kv.Key)).Append(':').Append(Json.N(kv.Value)); }
            sb.Append('}');
            if (kbTarget != null) sb.Append(",\"kb\":{\"p\":").Append(Json.Q(kbTarget)).Append(",\"n\":").Append(kbN).Append('}');
            sb.Append("}}");
            var js = sb.ToString();
            if (js == lastSent) return;
            lastSent = js;
            Send(js);
        }

        async void Send(string js)
        {
            var sock = ws; if (sock == null || sock.State != WebSocketState.Open) return;
            sending = true;
            try { await sock.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(js)), WebSocketMessageType.Text, true, CancellationToken.None); }
            catch (Exception) { }
            sending = false;
        }

        void OnDestroy() { gen++; Close(); }
        void OnApplicationQuit() { gen++; Close(); }
    }

    public class RemoteAvatar : MonoBehaviour
    {
        public string id, nick;
        public Rig rig;
        public Vector3 target;
        public float targetYaw;
        public bool moving, pvp = true;
        bool placed;

        public static RemoteAvatar Create(string id)
        {
            var go = new GameObject("Raider_" + id);
            var a = go.AddComponent<RemoteAvatar>();
            a.id = id;
            a.rig = Rig.Build(go.transform, new Color(0.8f, 0.62f, 0.5f), new Color(0.55f, 0.35f, 0.12f), new Color(0.18f, 0.2f, 0.25f), new Color(0.7f, 0.15f, 0.12f));
            var cap = go.AddComponent<CapsuleCollider>();
            cap.height = 1.8f; cap.radius = 0.4f; cap.center = new Vector3(0, 0.9f, 0); cap.isTrigger = true;
            Gfx.SetLayer(go, Gfx.EnemyLayer);
            Game.I.Track(go);
            return a;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!placed) { transform.position = target; placed = true; }
            float k = 1f - Mathf.Exp(-dt * 12f);
            var before = transform.position;
            transform.position = Vector3.Lerp(transform.position, target, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, targetYaw, 0), k);
            float sp = (transform.position - before).magnitude / Mathf.Max(dt, 0.0001f);
            rig.Animate(dt, moving ? Mathf.Max(sp, 3f) : 0f, true, rig.gunId != null && Defs.Gun(rig.gunId) != null && Defs.Gun(rig.gunId).melee, 0, 0, 0);
        }
    }
}
