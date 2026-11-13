using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeadHaul
{
    // All UI, built from code with uGUI: raid HUD, loot window, inventory, pause, main menu with trader and stash, results.
    public class Hud : MonoBehaviour
    {
        public static Hud I;
        Font font;
        RectTransform canvasRt;
        GameObject menuRoot, raidRoot, resultRoot, lootRoot, pauseRoot;
        public bool lootOpen, pauseOpen;
        bool bagOnly;
        Container lootC;
        int closedFrame = -1;

        public bool Blocking => lootOpen || pauseOpen || closedFrame == Time.frameCount;

        static readonly Color Panel = new Color(0.06f, 0.065f, 0.07f, 0.86f);
        static readonly Color PanelLight = new Color(0.14f, 0.15f, 0.16f, 0.95f);
        static readonly Color Gold = new Color(1f, 0.78f, 0.28f);
        static readonly Color Green = new Color(0.45f, 0.95f, 0.5f);
        static readonly Color Red = new Color(1f, 0.35f, 0.3f);
        static readonly Color Dim = new Color(0.7f, 0.72f, 0.74f);

        // Raid HUD parts
        RectTransform hpFill, stFill, actionFill, actionBar, chT, chB, chL, chR;
        Text hpText, timerText, exfilText, bagText, promptText, onlineText, bannerBig, bannerSmall, medText, actionText, extractText;
        readonly Text[] slotName = new Text[3], slotAmmo = new Text[3];
        readonly Image[] slotBg = new Image[3];
        Image hitX1, hitX2, dmgFlash, crossDot;
        float hitT, dmgT, bannerT, kick;
        readonly List<(Text t, float life)> feed = new List<(Text, float)>();
        RectTransform feedRoot;
        readonly List<(Text t, Vector3 w, float life)> pops = new List<(Text, Vector3, float)>();
        readonly List<Text> tags = new List<Text>();
        RectTransform overlayRoot;

        // Loot window
        Text lootTitle, lootLeftTitle, bagTitle;
        RectTransform lootList, bagList;
        // Menu
        Text cashText, statsText, netText, sensText, pvpText;
        RectTransform traderList, stashList;
        readonly Button[] qualityBtns = new Button[4];
        InputField nickField, codeField;
        // Result
        Text resTitle, resBody;
        // Pause
        Text pauseSens;
        readonly Button[] pauseQ = new Button[4];

        void Awake()
        {
            I = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var cgo = new GameObject("UI", typeof(RectTransform));
            var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            canvasRt = cgo.GetComponent<RectTransform>();

            var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            BuildRaid(canvasRt);
            BuildLoot(canvasRt);
            BuildPause(canvasRt);
            BuildMenu(canvasRt);
            BuildResult(canvasRt);
            dmgFlash = Img(canvasRt, new Color(0.7f, 0f, 0f, 0f), V(0, 0), V(1, 1), V(0, 0), V(0, 0)); dmgFlash.raycastTarget = false;
            raidRoot.SetActive(false); lootRoot.SetActive(false); pauseRoot.SetActive(false); resultRoot.SetActive(false);
        }

        // ---------- helpers ----------
        static Vector2 V(float x, float y) => new Vector2(x, y);

        RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }

        Image Img(Transform parent, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var rt = Rect(parent, "img", aMin, aMax, oMin, oMax);
            var im = rt.gameObject.AddComponent<Image>(); im.color = c;
            return im;
        }

        Text Txt(Transform parent, string s, int size, TextAnchor a, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, bool bold = false)
        {
            var rt = Rect(parent, "txt", aMin, aMax, oMin, oMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.text = s; t.fontSize = size; t.alignment = a; t.color = c; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            if (bold) t.fontStyle = FontStyle.Bold;
            var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.7f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        Button Btn(Transform parent, string label, Action on, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, int size = 22, Color? col = null)
        {
            var im = Img(parent, col ?? new Color(0.2f, 0.22f, 0.24f, 0.95f), aMin, aMax, oMin, oMax);
            var b = im.gameObject.AddComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); b.colors = cb;
            b.onClick.AddListener(() => { Sfx.I.Play2D("click", 0.5f); on(); });
            Txt(im.transform, label, size, TextAnchor.MiddleCenter, Color.white, V(0, 0), V(1, 1), V(4, 0), V(-4, 0), true);
            return b;
        }

        void SetLabel(Button b, string s) { var t = b.GetComponentInChildren<Text>(); if (t) t.text = s; }

        InputField Field(Transform parent, string value, int max, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var im = Img(parent, new Color(0.02f, 0.02f, 0.03f, 0.9f), aMin, aMax, oMin, oMax);
            var f = im.gameObject.AddComponent<InputField>();
            var txt = Txt(im.transform, "", 22, TextAnchor.MiddleLeft, Color.white, V(0, 0), V(1, 1), V(10, 0), V(-10, 0));
            txt.supportRichText = false; txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            f.textComponent = txt; f.characterLimit = max; f.text = value;
            return f;
        }

        RectTransform ScrollList(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var bg = Img(parent, new Color(0, 0, 0, 0.35f), aMin, aMax, oMin, oMax);
            var sr = bg.gameObject.AddComponent<ScrollRect>();
            var vp = Rect(bg.transform, "viewport", V(0, 0), V(1, 1), V(0, 0), V(0, 0));
            vp.gameObject.AddComponent<RectMask2D>();
            var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(0, 0, 0, 0.01f);
            var content = Rect(vp, "content", V(0, 1), V(1, 1), V(0, 0), V(0, 0));
            content.pivot = V(0.5f, 1);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 4; vl.padding = new RectOffset(6, 6, 6, 6);
            vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content; sr.viewport = vp; sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 30; sr.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        static void Clear(RectTransform rt) { for (int i = rt.childCount - 1; i >= 0; i--) Destroy(rt.GetChild(i).gameObject); }

        // One list row: coloured label on the left, up to two buttons on the right.
        void Row(RectTransform list, string label, string sub, Color col, string b1, Action a1, string b2 = null, Action a2 = null)
        {
            var rt = Rect(list, "row", V(0, 1), V(1, 1), V(0, 0), V(0, 0));
            var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 42; le.minHeight = 42;
            var bg = rt.gameObject.AddComponent<Image>(); bg.color = PanelLight;
            Img(rt, col, V(0, 0), V(0, 1), V(0, 0), V(5, 0));
            float right = 0;
            if (b2 != null) { Btn(rt, b2, a2, V(1, 0), V(1, 1), V(-110, 4), V(-6, -4), 18); right = 110; }
            if (b1 != null) { Btn(rt, b1, a1, V(1, 0), V(1, 1), V(-right - 120, 4), V(-right - 6, -4), 18, new Color(0.18f, 0.32f, 0.2f, 0.95f)); right += 120; }
            Txt(rt, label, 20, TextAnchor.MiddleLeft, col, V(0, 0), V(1, 1), V(14, 0), V(-right - 100, 0), true);
            if (sub != null) Txt(rt, sub, 17, TextAnchor.MiddleRight, Dim, V(0, 0), V(1, 1), V(14, 0), V(-right - 12, 0));
        }

        // ---------- raid HUD ----------
        void BuildRaid(RectTransform c)
        {
            raidRoot = Rect(c, "Raid", V(0, 0), V(1, 1), V(0, 0), V(0, 0)).gameObject;
            var r = raidRoot.transform;
            overlayRoot = Rect(r, "overlay", V(0, 0), V(1, 1), V(0, 0), V(0, 0));

            // Crosshair
            var ch = Rect(r, "cross", V(0.5f, 0.5f), V(0.5f, 0.5f), V(0, 0), V(0, 0));
            chT = Img(ch, Color.white, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-1.5f, 8), V(1.5f, 20)).rectTransform;
            chB = Img(ch, Color.white, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-1.5f, -20), V(1.5f, -8)).rectTransform;
            chL = Img(ch, Color.white, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-20, -1.5f), V(-8, 1.5f)).rectTransform;
            chR = Img(ch, Color.white, V(0.5f, 0.5f), V(0.5f, 0.5f), V(8, -1.5f), V(20, 1.5f)).rectTransform;
            crossDot = Img(ch, Color.white, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-2, -2), V(2, 2));
            hitX1 = Img(ch, new Color(1, 1, 1, 0), V(0.5f, 0.5f), V(0.5f, 0.5f), V(-14, -2), V(14, 2)); hitX1.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            hitX2 = Img(ch, new Color(1, 1, 1, 0), V(0.5f, 0.5f), V(0.5f, 0.5f), V(-14, -2), V(14, 2)); hitX2.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);

            promptText = Txt(r, "", 24, TextAnchor.MiddleCenter, Color.white, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-400, -110), V(400, -70), true);
            actionBar = Img(r, new Color(0, 0, 0, 0.6f), V(0.5f, 0.5f), V(0.5f, 0.5f), V(-160, -150), V(160, -138)).rectTransform;
            actionFill = Img(actionBar, Gold, V(0, 0), V(0, 1), V(0, 0), V(0, 0)).rectTransform;
            actionText = Txt(actionBar, "", 18, TextAnchor.MiddleCenter, Color.white, V(0, 1), V(1, 1), V(0, 4), V(0, 30));
            extractText = Txt(r, "", 40, TextAnchor.MiddleCenter, Green, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-400, 120), V(400, 180), true);

            // Bottom-left: health, stamina, hotbar
            var bl = Img(r, Panel, V(0, 0), V(0, 0), V(24, 24), V(560, 230)).rectTransform;
            hpText = Txt(bl, "100", 26, TextAnchor.MiddleLeft, Color.white, V(0, 1), V(0, 1), V(16, -44), V(120, -10), true);
            medText = Txt(bl, "", 18, TextAnchor.MiddleRight, Dim, V(1, 1), V(1, 1), V(-200, -44), V(-16, -10));
            var hpBg = Img(bl, new Color(0, 0, 0, 0.6f), V(0, 1), V(1, 1), V(16, -66), V(-16, -50)).rectTransform;
            hpFill = Img(hpBg, new Color(0.85f, 0.2f, 0.18f), V(0, 0), V(1, 1), V(0, 0), V(0, 0)).rectTransform;
            var stBg = Img(bl, new Color(0, 0, 0, 0.6f), V(0, 1), V(1, 1), V(16, -80), V(-16, -72)).rectTransform;
            stFill = Img(stBg, new Color(0.95f, 0.8f, 0.3f), V(0, 0), V(1, 1), V(0, 0), V(0, 0)).rectTransform;
            for (int i = 0; i < 3; i++)
            {
                float x0 = 16 + i * 176;
                slotBg[i] = Img(bl, PanelLight, V(0, 0), V(0, 0), V(x0, 14), V(x0 + 168, 112));
                Txt(slotBg[i].transform, (i + 1).ToString(), 16, TextAnchor.UpperLeft, Dim, V(0, 0), V(1, 1), V(8, -6), V(-6, -6));
                slotName[i] = Txt(slotBg[i].transform, "", 19, TextAnchor.MiddleCenter, Color.white, V(0, 0), V(1, 1), V(4, 6), V(-4, -4), true);
                slotAmmo[i] = Txt(slotBg[i].transform, "", 18, TextAnchor.LowerRight, Gold, V(0, 0), V(1, 1), V(6, 6), V(-8, -6), true);
            }

            // Top
            timerText = Txt(r, "15:00", 34, TextAnchor.MiddleCenter, Color.white, V(0.5f, 1), V(0.5f, 1), V(-200, -64), V(200, -20), true);
            exfilText = Txt(r, "", 20, TextAnchor.MiddleCenter, Green, V(0.5f, 1), V(0.5f, 1), V(-400, -96), V(400, -66));
            onlineText = Txt(r, "", 18, TextAnchor.UpperLeft, Dim, V(0, 1), V(0, 1), V(24, -60), V(600, -20));
            bannerBig = Txt(r, "", 64, TextAnchor.MiddleCenter, Gold, V(0.5f, 1), V(0.5f, 1), V(-700, -230), V(700, -150), true);
            bannerSmall = Txt(r, "", 24, TextAnchor.MiddleCenter, Color.white, V(0.5f, 1), V(0.5f, 1), V(-700, -270), V(700, -230));
            bagText = Txt(r, "", 22, TextAnchor.LowerRight, Color.white, V(1, 0), V(1, 0), V(-600, 24), V(-28, 120), true);
            feedRoot = Rect(r, "feed", V(1, 1), V(1, 1), V(-520, -400), V(-24, -24));
            Txt(r, "WASD move  Shift sprint  C crouch  Space jump  Z dive  RMB aim  R reload  H heal  E loot  Tab bag  V view  Esc menu", 15, TextAnchor.LowerCenter, new Color(1, 1, 1, 0.45f), V(0.5f, 0), V(0.5f, 0), V(-700, 6), V(700, 26));
        }

        public void ShowRaid()
        {
            menuRoot.SetActive(false); resultRoot.SetActive(false); pauseRoot.SetActive(false); lootRoot.SetActive(false);
            raidRoot.SetActive(true);
            lootOpen = pauseOpen = false;
        }

        void Update()
        {
            var G = Game.I; if (G == null) return;
            float dt = Time.unscaledDeltaTime;
            dmgT = Mathf.Max(0, dmgT - dt * 1.6f);
            dmgFlash.color = new Color(0.7f, 0, 0, dmgT * 0.45f);
            if (Gfx.Vig != null) { Gfx.Vig.color.value = Color.Lerp(Color.black, new Color(0.6f, 0, 0), Mathf.Clamp01(dmgT * 2f)); Gfx.Vig.intensity.value = 0.3f + dmgT * 0.25f; }
            if (G.state != Game.St.Raid) { if (menuRoot.activeSelf && netText != null) RefreshNet(); return; }

            // Keys
            if (Inp.Down(KeyCode.Escape))
            {
                if (lootOpen) CloseLoot();
                else { pauseOpen = !pauseOpen; pauseRoot.SetActive(pauseOpen); RefreshPause(); }
            }
            else if (Inp.Down(KeyCode.Tab) && !pauseOpen)
            {
                if (lootOpen) CloseLoot(); else OpenBag();
            }
            else if (Inp.Down(KeyCode.E) && lootOpen) CloseLoot();

            var P = G.player; if (P == null) return;
            if (lootOpen && lootC != null && Vector3.Distance(lootC.transform.position, P.transform.position) > 3.6f) CloseLoot();

            // Bars and slots
            hpFill.anchorMax = V(Mathf.Clamp01(P.hp / 100f), 1);
            stFill.anchorMax = V(Mathf.Clamp01(P.stamina / 100f), 1);
            hpText.text = Mathf.CeilToInt(P.hp).ToString();
            hpText.color = P.hp < 30 ? Red : Color.white;
            medText.text = "Medkits " + P.medkits + "  (H)";
            for (int i = 0; i < 3; i++)
            {
                var g = Defs.Gun(P.slots[i]);
                bool active = i == P.cur;
                if (P.miniT > 0 && active) { slotName[i].text = "MINIGUN"; slotAmmo[i].text = P.miniT.ToString("0.0") + "s"; slotBg[i].color = new Color(0.55f, 0.28f, 0.08f, 0.95f); continue; }
                slotName[i].text = g != null ? g.name : "-";
                slotAmmo[i].text = g == null || g.melee ? "" : P.ammo[i] + "/" + g.mag;
                slotBg[i].color = active ? new Color(0.32f, 0.27f, 0.12f, 0.95f) : PanelLight;
            }

            // Action bar: search, reload, heal, extract
            float frac = -1; string what = "";
            var cg = P.Cur;
            if (P.searching != null) { frac = P.searchT / P.searching.searchTime; what = "Searching " + P.searching.Label; }
            else if (P.Healing) { frac = P.HealFrac; what = "Healing"; }
            else if (P.Reloading) { frac = P.ReloadFrac(cg); what = "Reloading"; }
            actionBar.gameObject.SetActive(frac >= 0);
            if (frac >= 0) { actionFill.anchorMax = V(Mathf.Clamp01(frac), 1); actionText.text = what; }
            extractText.text = P.atExfil != null ? "EXTRACTING  " + Mathf.Max(0, 6f - P.exfilT).ToString("0.0") : "";

            var f = P.Facing;
            promptText.text = lootOpen || P.searching != null ? "" : f != null ? "[E] " + (f.searched ? "Open " : "Search ") + f.Label + (f.searched && f.items.Count == 0 ? " (empty)" : "") : "";

            // Timer and exfil direction
            int secs = Mathf.Max(0, Mathf.CeilToInt(G.raidTime));
            timerText.text = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
            timerText.color = secs < 60 ? Red : Color.white;
            Exfil best = null; float bd = 1e9f;
            foreach (var ex in G.world.exfils) { float d = Vector3.Distance(ex.pos, P.transform.position); if (d < bd) { bd = d; best = ex; } }
            if (best != null)
            {
                Vector3 to = best.pos - P.transform.position;
                float ang = Mathf.DeltaAngle(P.yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                string dir = Mathf.Abs(ang) < 25 ? "ahead" : Mathf.Abs(ang) > 150 ? "behind" : ang > 0 ? (ang < 80 ? "ahead right" : "right") : (ang > -80 ? "ahead left" : "left");
                exfilText.text = "Exfil: " + best.name + "  " + Mathf.RoundToInt(bd) + " m " + dir;
            }
            bagText.text = "Bag  $" + P.BagValue.ToString("N0") + "\n" + P.Kg.ToString("0.0") + " / " + Player.MaxKg + " kg" + (P.Kg > Player.MaxKg ? "  OVERWEIGHT" : "") + "\nKills " + G.raidKills;
            onlineText.text = Net.I != null ? Net.I.status + (Net.I.connected ? "  |  " + Net.I.online + " other player" + (Net.I.online == 1 ? "" : "s") : "") : "";

            // Crosshair spread and hitmarker
            kick = Mathf.MoveTowards(kick, 0, dt * 30f);
            bool melee = cg != null && cg.melee;
            float gap = 8 + (cg != null && !melee ? (cg.spread * (P.aiming ? 0.45f : 1f) + P.Spread) * 6f : 0) + kick * 2f + (P.moving ? 6 : 0);
            gap = Mathf.Min(gap, 70);
            chT.anchoredPosition = V(0, gap + 6); chB.anchoredPosition = V(0, -gap - 6);
            chL.anchoredPosition = V(-gap - 6, 0); chR.anchoredPosition = V(gap + 6, 0);
            bool showLines = !melee && !(P.aiming && cg != null && cg.scope);
            chT.gameObject.SetActive(showLines); chB.gameObject.SetActive(showLines); chL.gameObject.SetActive(showLines); chR.gameObject.SetActive(showLines);
            crossDot.color = P.aiming && cg != null && cg.id == "rifle" ? new Color(1f, 0.15f, 0.1f) : Color.white;
            hitT = Mathf.Max(0, hitT - dt * 4f);
            var hc = hitX1.color; hc.a = hitT; hitX1.color = hc; hitX2.color = hc;

            // Banner
            bannerT -= dt;
            float ba = Mathf.Clamp01(bannerT);
            var bc = bannerBig.color; bc.a = ba; bannerBig.color = bc;
            var sc = bannerSmall.color; sc.a = ba; bannerSmall.color = sc;

            // Kill feed
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                var (t, life) = feed[i];
                life -= dt; feed[i] = (t, life);
                if (life <= 0) { Destroy(t.gameObject); feed.RemoveAt(i); continue; }
                var c = t.color; c.a = Mathf.Clamp01(life); t.color = c;
                t.rectTransform.anchoredPosition = V(0, -(feed.Count - 1 - i) * 32);
            }

            // Damage numbers
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (t, w, life) = pops[i];
                life -= dt; w += Vector3.up * dt * 1.2f; pops[i] = (t, w, life);
                if (life <= 0) { Destroy(t.gameObject); pops.RemoveAt(i); continue; }
                if (!Place(t.rectTransform, w)) t.enabled = false; else t.enabled = true;
                var c = t.color; c.a = Mathf.Clamp01(life * 2f); t.color = c;
            }

            // Other players' name tags
            int n = 0;
            if (Net.I != null)
                foreach (var av in Net.I.Avatars)
                {
                    if (n >= tags.Count) tags.Add(Txt(overlayRoot, "", 18, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.5f), V(0, 0), V(0, 0), V(-100, -12), V(100, 12), true));
                    var t = tags[n++];
                    t.text = av.nick + (av.pvp && G.S.pvp ? "" : "  (friendly)");
                    t.enabled = Vector3.Distance(av.transform.position, P.transform.position) < 80f && Place(t.rectTransform, av.transform.position + Vector3.up * 2.3f);
                }
            for (int i = n; i < tags.Count; i++) tags[i].enabled = false;
        }

        bool Place(RectTransform rt, Vector3 world)
        {
            var cam = Game.I.cam;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0.1f) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out var local);
            rt.anchorMin = rt.anchorMax = V(0.5f, 0.5f);
            rt.anchoredPosition = local;
            return true;
        }

        public void Hitmarker(bool head, bool kill)
        {
            hitT = 1f;
            var c = kill ? Red : head ? Gold : Color.white;
            hitX1.color = c; hitX2.color = c;
            Sfx.I.Play2D(kill ? "kill" : head ? "head" : "hit", 0.5f);
        }

        public void Kick(float k) { kick = Mathf.Min(kick + k * 3f, 14f); }

        public void Damage(float d, Vector3 from) { dmgT = Mathf.Min(1.2f, dmgT + d / 25f); }

        public void Feed(string s, Color c)
        {
            var t = Txt(feedRoot, s, 22, TextAnchor.UpperRight, c, V(0, 1), V(1, 1), V(0, -30), V(0, 0), true);
            feed.Add((t, 4f));
            if (feed.Count > 7) { Destroy(feed[0].t.gameObject); feed.RemoveAt(0); }
        }

        public void Banner(string big, string small, Color c)
        {
            bannerBig.text = big; bannerSmall.text = small;
            bannerBig.color = c; bannerSmall.color = Color.white;
            bannerT = 3f;
        }

        public void Pop(Vector3 world, string s, Color c)
        {
            var t = Txt(overlayRoot, s, 26, TextAnchor.MiddleCenter, c, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-60, -16), V(60, 16), true);
            var w = world + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0, UnityEngine.Random.Range(-0.3f, 0.3f));
            pops.Add((t, w, 0.9f));
            if (pops.Count > 30) { Destroy(pops[0].t.gameObject); pops.RemoveAt(0); }
        }

        // ---------- loot window ----------
        void BuildLoot(RectTransform c)
        {
            lootRoot = Img(c, new Color(0, 0, 0, 0.45f), V(0, 0), V(1, 1), V(0, 0), V(0, 0)).gameObject;
            var p = Img(lootRoot.transform, Panel, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-560, -330), V(560, 330)).rectTransform;
            lootTitle = Txt(p, "", 34, TextAnchor.MiddleLeft, Gold, V(0, 1), V(1, 1), V(24, -60), V(-24, -12), true);
            lootLeftTitle = Txt(p, "", 20, TextAnchor.MiddleLeft, Dim, V(0, 1), V(0.5f, 1), V(24, -96), V(-12, -64));
            bagTitle = Txt(p, "", 20, TextAnchor.MiddleLeft, Dim, V(0.5f, 1), V(1, 1), V(12, -96), V(-24, -64));
            lootList = ScrollList(p, V(0, 0), V(0.5f, 1), V(24, 80), V(-12, -100));
            bagList = ScrollList(p, V(0.5f, 0), V(1, 1), V(12, 80), V(-24, -100));
            Btn(p, "Take all", TakeAll, V(0, 0), V(0, 0), V(24, 20), V(224, 64), 22, new Color(0.18f, 0.32f, 0.2f, 0.95f));
            Btn(p, "Close (E)", CloseLoot, V(1, 0), V(1, 0), V(-224, 20), V(-24, 64));
        }

        public void OpenLoot(Container c)
        {
            lootC = c; bagOnly = false; lootOpen = true; lootRoot.SetActive(true); RefreshLoot();
        }

        void OpenBag()
        {
            lootC = null; bagOnly = true; lootOpen = true; lootRoot.SetActive(true); RefreshLoot();
        }

        void CloseLoot() { lootOpen = false; lootRoot.SetActive(false); lootC = null; closedFrame = Time.frameCount; }

        void TakeAll()
        {
            var P = Game.I.player; if (P == null || lootC == null) return;
            foreach (var l in new List<Loot>(lootC.items)) if (P.Take(l)) lootC.items.Remove(l); else break;
            RefreshLoot();
        }

        void RefreshLoot()
        {
            var P = Game.I.player; if (P == null) return;
            Clear(lootList); Clear(bagList);
            if (bagOnly)
            {
                lootTitle.text = "BAG";
                lootLeftTitle.text = "Raid";
                Row(lootList, "Health " + Mathf.CeilToInt(P.hp), null, Green, null, null);
                Row(lootList, "Medkits " + P.medkits, "H to use", Dim, null, null);
                Row(lootList, "Kills " + Game.I.raidKills, null, Dim, null, null);
                Row(lootList, "Time left " + Mathf.CeilToInt(Game.I.raidTime / 60f) + " min", null, Dim, null, null);
            }
            else
            {
                lootTitle.text = lootC.Label.ToUpperInvariant();
                lootLeftTitle.text = lootC.items.Count == 0 ? "Empty" : lootC.items.Count + " item" + (lootC.items.Count == 1 ? "" : "s") + ". Click Take.";
                foreach (var l in lootC.items)
                {
                    var item = l;
                    Row(lootList, l.Name, "$" + l.Value.ToString("N0") + "  " + l.Kg.ToString("0.#") + "kg", Defs.RarityCol[l.Rarity], "Take", () => { if (P.Take(item)) { lootC.items.Remove(item); RefreshLoot(); } });
                }
            }
            bagTitle.text = "Your bag  $" + P.BagValue.ToString("N0") + "   " + P.Kg.ToString("0.0") + " / " + Player.MaxKg + " kg";
            foreach (var l in P.bag)
            {
                var item = l;
                Row(bagList, l.Name, "$" + l.Value.ToString("N0"), Defs.RarityCol[l.Rarity], "Drop", () => { P.bag.Remove(item); Drop(item); RefreshLoot(); });
            }
        }

        void Drop(Loot l)
        {
            if (lootC != null) { lootC.items.Add(l); return; }
            var P = Game.I.player;
            var go = new GameObject("Dropped");
            go.transform.position = P.transform.position + P.transform.forward * 0.9f;
            Gfx.Box(go.transform, new Vector3(0, 0.15f, 0), new Vector3(0.6f, 0.3f, 0.4f), new Color(0.3f, 0.35f, 0.22f));
            var c = go.AddComponent<Container>();
            c.kind = ContainerKind.Crate; c.searched = true; c.items.Add(l);
            Game.I.AddLootable(c); Game.I.Track(go);
        }

        // ---------- pause ----------
        void BuildPause(RectTransform c)
        {
            pauseRoot = Img(c, new Color(0, 0, 0, 0.6f), V(0, 0), V(1, 1), V(0, 0), V(0, 0)).gameObject;
            var p = Img(pauseRoot.transform, Panel, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-320, -300), V(320, 300)).rectTransform;
            Txt(p, "PAUSED", 40, TextAnchor.MiddleCenter, Gold, V(0, 1), V(1, 1), V(0, -80), V(0, -20), true);
            Txt(p, "The raid keeps running.", 18, TextAnchor.MiddleCenter, Dim, V(0, 1), V(1, 1), V(0, -110), V(0, -80));
            Btn(p, "Resume", () => { pauseOpen = false; pauseRoot.SetActive(false); closedFrame = Time.frameCount; }, V(0, 1), V(1, 1), V(60, -180), V(-60, -125), 24, new Color(0.18f, 0.32f, 0.2f, 0.95f));
            Txt(p, "Graphics", 20, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(60, -220), V(-60, -190));
            for (int i = 0; i < 4; i++)
            {
                int q = i; float w = 520f / 4f;
                pauseQ[i] = Btn(p, Gfx.QualityNames[i], () => SetQuality(q), V(0, 1), V(0, 1), V(60 + i * w, -270), V(60 + (i + 1) * w - 6, -226), 20);
            }
            pauseSens = Txt(p, "", 20, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(60, -320), V(-60, -290));
            Btn(p, "-", () => { Game.I.S.sens = Mathf.Max(0.2f, Game.I.S.sens - 0.1f); Game.I.S.Save(); RefreshPause(); }, V(1, 1), V(1, 1), V(-160, -322), V(-112, -288));
            Btn(p, "+", () => { Game.I.S.sens = Mathf.Min(4f, Game.I.S.sens + 0.1f); Game.I.S.Save(); RefreshPause(); }, V(1, 1), V(1, 1), V(-106, -322), V(-58, -288));
            Btn(p, "Leave raid (lose bag and primary)", () => { pauseOpen = false; pauseRoot.SetActive(false); Game.I.EndRaid("mia"); }, V(0, 0), V(1, 0), V(60, 100), V(-60, 150), 20, new Color(0.45f, 0.15f, 0.12f, 0.95f));
            Btn(p, "Quit game", Application.Quit, V(0, 0), V(1, 0), V(60, 36), V(-60, 86), 20);
        }

        void RefreshPause()
        {
            pauseSens.text = "Mouse sensitivity  " + Game.I.S.sens.ToString("0.0");
            for (int i = 0; i < 4; i++) pauseQ[i].GetComponent<Image>().color = i == Game.I.S.quality ? new Color(0.45f, 0.36f, 0.12f, 0.95f) : new Color(0.2f, 0.22f, 0.24f, 0.95f);
        }

        void SetQuality(int q)
        {
            Game.I.S.quality = q; Game.I.S.Save();
            Gfx.ApplyQuality(q, Game.I.cam);
            RefreshPause(); RefreshMenu();
        }

        // ---------- main menu ----------
        void BuildMenu(RectTransform c)
        {
            menuRoot = Rect(c, "Menu", V(0, 0), V(1, 1), V(0, 0), V(0, 0)).gameObject;
            var m = menuRoot.transform;
            Img(m, new Color(0, 0, 0, 0.35f), V(0, 0), V(1, 1), V(0, 0), V(0, 0));

            var left = Img(m, Panel, V(0, 0), V(0, 1), V(40, 40), V(560, -40)).rectTransform;
            Txt(left, "DEAD HAUL", 64, TextAnchor.MiddleLeft, Gold, V(0, 1), V(1, 1), V(30, -110), V(-20, -30), true);
            Txt(left, "Loot. Survive. Extract.", 20, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(32, -140), V(-20, -110));
            cashText = Txt(left, "", 34, TextAnchor.MiddleLeft, Green, V(0, 1), V(1, 1), V(30, -200), V(-20, -150), true);
            statsText = Txt(left, "", 18, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(32, -232), V(-20, -204));

            Txt(left, "Name", 18, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(30, -270), V(-20, -244));
            nickField = Field(left, Game.I.S.nick, 14, V(0, 1), V(1, 1), V(30, -316), V(-30, -274));
            nickField.onEndEdit.AddListener(s => { Game.I.S.nick = string.IsNullOrWhiteSpace(s) ? "Raider" : s.Trim(); Game.I.S.Save(); });

            Txt(left, "Lobby code (empty = public lobby)", 18, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(30, -352), V(-20, -326));
            codeField = Field(left, Game.I.S.lobby.ToUpperInvariant(), 12, V(0, 1), V(1, 1), V(30, -398), V(-170, -356));
            Btn(left, "Join", () => Net.I.Join(codeField.text), V(1, 1), V(1, 1), V(-160, -398), V(-30, -356));
            netText = Txt(left, "", 18, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(30, -432), V(-20, -404));
            var pvpBtn = Btn(left, "", () => { Game.I.S.pvp = !Game.I.S.pvp; Game.I.S.Save(); RefreshMenu(); }, V(0, 1), V(1, 1), V(30, -484), V(-30, -442), 20);
            pvpText = pvpBtn.GetComponentInChildren<Text>();

            Txt(left, "Graphics", 18, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(30, -520), V(-20, -494));
            for (int i = 0; i < 4; i++)
            {
                int q = i; float w = 460f / 4f;
                qualityBtns[i] = Btn(left, Gfx.QualityNames[i], () => SetQuality(q), V(0, 1), V(0, 1), V(30 + i * w, -568), V(30 + (i + 1) * w - 6, -526), 20);
            }
            sensText = Txt(left, "", 18, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(30, -612), V(-20, -580));
            Btn(left, "-", () => { Game.I.S.sens = Mathf.Max(0.2f, Game.I.S.sens - 0.1f); Game.I.S.Save(); RefreshMenu(); }, V(1, 1), V(1, 1), V(-140, -616), V(-88, -578));
            Btn(left, "+", () => { Game.I.S.sens = Mathf.Min(4f, Game.I.S.sens + 0.1f); Game.I.S.Save(); RefreshMenu(); }, V(1, 1), V(1, 1), V(-82, -616), V(-30, -578));

            Btn(left, "START RAID", () => { Game.I.S.nick = string.IsNullOrWhiteSpace(nickField.text) ? "Raider" : nickField.text.Trim(); Game.I.StartRaid(); }, V(0, 0), V(1, 0), V(30, 100), V(-30, 180), 34, new Color(0.62f, 0.42f, 0.1f, 0.97f));
            Btn(left, "Quit", Application.Quit, V(0, 0), V(1, 0), V(30, 30), V(-30, 80), 20);

            var mid = Img(m, Panel, V(0, 0), V(0, 1), V(590, 40), V(1180, -40)).rectTransform;
            Txt(mid, "TRADER AND LOADOUT", 30, TextAnchor.MiddleLeft, Gold, V(0, 1), V(1, 1), V(24, -76), V(-20, -24), true);
            Txt(mid, "One gun per slot: primary, secondary, melee. Dying loses your primary.", 17, TextAnchor.MiddleLeft, Dim, V(0, 1), V(1, 1), V(24, -104), V(-20, -78));
            traderList = ScrollList(mid, V(0, 0), V(1, 1), V(20, 20), V(-20, -116));

            var right = Img(m, Panel, V(0, 0), V(1, 1), V(1210, 40), V(-40, -40)).rectTransform;
            Txt(right, "STASH", 30, TextAnchor.MiddleLeft, Gold, V(0, 1), V(1, 1), V(24, -76), V(-20, -24), true);
            Btn(right, "Sell all", SellAll, V(1, 1), V(1, 1), V(-170, -72), V(-20, -30), 20, new Color(0.18f, 0.32f, 0.2f, 0.95f));
            stashList = ScrollList(right, V(0, 0), V(1, 1), V(20, 20), V(-20, -90));
        }

        public void ShowMenu()
        {
            raidRoot.SetActive(false); resultRoot.SetActive(false); lootRoot.SetActive(false); pauseRoot.SetActive(false);
            lootOpen = pauseOpen = false;
            menuRoot.SetActive(true);
            RefreshMenu();
        }

        void RefreshNet()
        {
            netText.text = Net.I.status + (Net.I.connected ? "  |  " + Net.I.online + " other player" + (Net.I.online == 1 ? "" : "s") : "");
        }

        void RefreshMenu()
        {
            var S = Game.I.S;
            cashText.text = "$" + S.cash.ToString("N0");
            statsText.text = "Raids " + S.raids + "   Extracts " + S.extracts + "   Kills " + S.kills;
            pvpText.text = S.pvp ? "PvP: ON  (other players can hurt you)" : "PvP: OFF";
            sensText.text = "Mouse sensitivity  " + S.sens.ToString("0.0");
            for (int i = 0; i < 4; i++) qualityBtns[i].GetComponent<Image>().color = i == S.quality ? new Color(0.45f, 0.36f, 0.12f, 0.95f) : new Color(0.2f, 0.22f, 0.24f, 0.95f);
            if (Net.I != null) RefreshNet();

            Clear(traderList);
            foreach (var g in Defs.Guns)
            {
                var gun = g;
                string slot = g.slot == Slot.Primary ? "Primary" : g.slot == Slot.Secondary ? "Secondary" : "Melee";
                bool owned = S.owned.Contains(g.id);
                bool equipped = S.primary == g.id || S.secondary == g.id || S.melee == g.id;
                string stats = g.melee ? slot + "  dmg " + g.dmg : slot + "  dmg " + g.dmg + (g.pellets > 1 ? "x" + g.pellets : "") + "  " + g.rpm + " rpm  mag " + g.mag;
                Color col = equipped ? Gold : owned ? Color.white : Dim;
                if (!owned)
                    Row(traderList, g.name, stats, col, "Buy $" + (g.price / 1000f).ToString("0.#") + "k", () => Buy(gun));
                else if (equipped)
                    Row(traderList, g.name + "  (equipped)", stats, col, g.slot == Slot.Primary ? "Unequip" : null, () => { S.primary = ""; S.Save(); RefreshMenu(); });
                else
                    Row(traderList, g.name, stats, col, "Equip", () => Equip(gun));
            }
            Row(traderList, "Medkits: 2 come with every raid", null, Green, null, null);

            Clear(stashList);
            if (S.stash.Count == 0) Row(stashList, "Empty. Extract with loot to fill it.", null, Dim, null, null);
            for (int i = 0; i < S.stash.Count; i++)
            {
                int idx = i; string id = S.stash[i];
                if (id.StartsWith("gun:"))
                {
                    var g = Defs.Gun(id.Substring(4)); if (g == null) continue;
                    var l = new Loot { gun = g.id };
                    bool have = S.owned.Contains(g.id);
                    Row(stashList, g.name, "$" + l.Value.ToString("N0"), Defs.RarityCol[2], "Sell", () => Sell(idx), have ? null : "Keep", () => { S.owned.Add(g.id); S.stash.RemoveAt(idx); S.Save(); RefreshMenu(); });
                }
                else
                {
                    var it = Defs.Item(id); if (it == null) continue;
                    Row(stashList, it.name, "$" + it.val.ToString("N0"), Defs.RarityCol[it.rarity], "Sell", () => Sell(idx));
                }
            }
        }

        int StashValue(string id)
        {
            if (id.StartsWith("gun:")) { var g = Defs.Gun(id.Substring(4)); return g == null ? 0 : new Loot { gun = g.id }.Value; }
            var it = Defs.Item(id); return it == null ? 0 : it.val;
        }

        void Sell(int idx)
        {
            var S = Game.I.S; if (idx < 0 || idx >= S.stash.Count) return;
            S.cash += StashValue(S.stash[idx]); S.stash.RemoveAt(idx); S.Save();
            Sfx.I.Play2D("pickup"); RefreshMenu();
        }

        void SellAll()
        {
            var S = Game.I.S; int total = 0;
            foreach (var id in S.stash) total += StashValue(id);
            S.cash += total; S.stash.Clear(); S.Save();
            if (total > 0) Sfx.I.Play2D("rare");
            RefreshMenu();
        }

        void Buy(GunDef g)
        {
            var S = Game.I.S;
            if (S.cash < g.price) { Sfx.I.Play2D("hurt", 0.5f); return; }
            S.cash -= (int)g.price; S.owned.Add(g.id);
            Equip(g);
            Sfx.I.Play2D("rare");
        }

        void Equip(GunDef g)
        {
            var S = Game.I.S;
            if (g.slot == Slot.Primary) S.primary = g.id; else if (g.slot == Slot.Secondary) S.secondary = g.id; else S.melee = g.id;
            S.Save(); RefreshMenu();
        }

        // ---------- results ----------
        void BuildResult(RectTransform c)
        {
            resultRoot = Img(c, new Color(0, 0, 0, 0.55f), V(0, 0), V(1, 1), V(0, 0), V(0, 0)).gameObject;
            var p = Img(resultRoot.transform, Panel, V(0.5f, 0.5f), V(0.5f, 0.5f), V(-480, -280), V(480, 280)).rectTransform;
            resTitle = Txt(p, "", 64, TextAnchor.MiddleCenter, Gold, V(0, 1), V(1, 1), V(0, -130), V(0, -40), true);
            resBody = Txt(p, "", 26, TextAnchor.UpperCenter, Color.white, V(0, 0), V(1, 1), V(40, 120), V(-40, -150));
            Btn(p, "Continue", () => Game.I.BackToMenu(), V(0.5f, 0), V(0.5f, 0), V(-160, 34), V(160, 96), 28, new Color(0.62f, 0.42f, 0.1f, 0.97f));
        }

        public void ShowResult()
        {
            var G = Game.I;
            raidRoot.SetActive(false); lootRoot.SetActive(false); pauseRoot.SetActive(false);
            lootOpen = pauseOpen = false;
            resultRoot.SetActive(true);
            bool ok = G.lastResult == "extract";
            resTitle.text = ok ? "EXTRACTED" : G.lastResult == "kia" ? "KILLED IN ACTION" : "MISSING IN ACTION";
            resTitle.color = ok ? Green : Red;
            var s = ok ? "Loot sent to your stash: $" + G.lastValue.ToString("N0") : "Bag lost: $" + G.lastValue.ToString("N0");
            s += "\nKills " + G.raidKills + "  (hunters " + G.raidHunterKills + ")";
            if (ok) s += "\nKill bounty: $" + (G.raidKills * 25 + G.raidHunterKills * 225).ToString("N0");
            if (G.lastLost.Count > 0) s += "\nLost: " + string.Join(", ", G.lastLost);
            s += "\n\nCash: $" + G.S.cash.ToString("N0");
            resBody.text = s;
        }
    }
}
