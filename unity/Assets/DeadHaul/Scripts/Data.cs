using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadHaul
{
    public enum Slot { Primary = 0, Secondary = 1, Melee = 2 }

    public class GunDef
    {
        public string id, name;
        public Slot slot;
        public float dmg, rpm, spread, range = 120f, reload, recoil, price;
        public int mag, pellets = 1;
        public bool auto, melee, scope;
        public float length = 0.6f;
        public Color tint = new Color(0.16f, 0.17f, 0.18f);
    }

    public class ItemDef
    {
        public string id, name;
        public int val, rarity;
        public float kg;
        public ItemDef(string id, string name, int val, float kg, int rarity)
        { this.id = id; this.name = name; this.val = val; this.kg = kg; this.rarity = rarity; }
    }

    public static class Defs
    {
        public static readonly GunDef[] Guns =
        {
            new GunDef{ id="pistol",  name="M9 Pistol",     slot=Slot.Secondary, dmg=26, rpm=320, spread=1.1f, mag=12, reload=1.2f, recoil=1.4f, price=0,    length=0.32f },
            new GunDef{ id="uzis",    name="Dual Uzis",     slot=Slot.Secondary, dmg=15, rpm=900, spread=3.2f, mag=50, reload=2.0f, recoil=0.9f, price=9000, auto=true, length=0.36f },
            new GunDef{ id="smg",     name="MP5",           slot=Slot.Primary,   dmg=19, rpm=800, spread=2.2f, mag=30, reload=1.8f, recoil=0.8f, price=6000, auto=true, length=0.55f },
            new GunDef{ id="shotgun", name="Pump Shotgun",  slot=Slot.Primary,   dmg=13, rpm=75,  spread=6.0f, mag=6,  reload=2.4f, recoil=4.5f, price=7000, pellets=9, range=45, length=0.85f, tint=new Color(0.25f,0.17f,0.11f) },
            new GunDef{ id="spas",    name="Auto SPAS-12",  slot=Slot.Primary,   dmg=12, rpm=240, spread=6.5f, mag=8,  reload=2.6f, recoil=3.6f, price=16000, pellets=8, auto=true, range=45, length=0.85f },
            new GunDef{ id="rifle",   name="M4 Rifle",      slot=Slot.Primary,   dmg=30, rpm=680, spread=0.9f, mag=30, reload=2.0f, recoil=1.1f, price=12000, auto=true, scope=true, length=0.8f },
            new GunDef{ id="ak",      name="AK-47",         slot=Slot.Primary,   dmg=37, rpm=600, spread=1.5f, mag=30, reload=2.2f, recoil=1.7f, price=15000, auto=true, length=0.82f, tint=new Color(0.36f,0.22f,0.12f) },
            new GunDef{ id="sniper",  name="Bolt Sniper",   slot=Slot.Primary,   dmg=120,rpm=48,  spread=0.08f,mag=5,  reload=2.8f, recoil=6f,   price=22000, scope=true, range=300, length=1.05f },
            new GunDef{ id="katana",  name="Katana",        slot=Slot.Melee,     dmg=60, rpm=100, range=2.9f, price=0,     melee=true, length=0.9f },
            new GunDef{ id="saber",   name="Plasma Saber",  slot=Slot.Melee,     dmg=95, rpm=120, range=3.2f, price=18000, melee=true, length=1.0f, tint=new Color(0.3f,0.8f,2.5f) },
        };

        // Power-up gun, never sold. 15 seconds of this from a lucky infected drop.
        public static readonly GunDef Minigun = new GunDef { id = "minigun", name = "MINIGUN", slot = Slot.Primary, dmg = 24, rpm = 1500, spread = 2.6f, mag = 9999, reload = 0, recoil = 0.35f, auto = true, length = 1.0f };

        public static GunDef Gun(string id)
        {
            if (id == "minigun") return Minigun;
            foreach (var g in Guns) if (g.id == id) return g;
            return null;
        }

        public static readonly ItemDef[] Items =
        {
            new ItemDef("bolts","Bolts",40,0.3f,0),
            new ItemDef("tape","Duct tape",55,0.3f,0),
            new ItemDef("beans","Canned beans",30,0.5f,0),
            new ItemDef("water","Water bottle",35,1.0f,0),
            new ItemDef("wire","Copper wire",90,1.2f,0),
            new ItemDef("pills","Painkillers",120,0.1f,0),
            new ItemDef("fuel","Fuel can",260,4.0f,1),
            new ItemDef("battery","Car battery",340,6.0f,1),
            new ItemDef("tools","Toolbox",300,3.5f,1),
            new ItemDef("radio","Field radio",420,1.4f,1),
            new ItemDef("ammo","Ammo crate",380,2.5f,1),
            new ItemDef("circuit","Circuit board",650,0.4f,2),
            new ItemDef("watch","Gold watch",900,0.2f,2),
            new ItemDef("nvg","Night vision",1400,0.9f,2),
            new ItemDef("whiskey","Rare whiskey",1100,1.2f,2),
            new ItemDef("gpu","Graphics card",2600,0.9f,3),
            new ItemDef("chain","Gold chain",2200,0.2f,3),
            new ItemDef("drive","Encrypted drive",3400,0.1f,3),
            new ItemDef("keycard","Lab keycard",8000,0.1f,4),
            new ItemDef("btc","Cold wallet",12000,0.1f,4),
        };

        public static ItemDef Item(string id)
        {
            foreach (var i in Items) if (i.id == id) return i;
            return null;
        }

        public static readonly Color[] RarityCol =
        {
            new Color(0.70f,0.72f,0.74f), new Color(0.40f,0.85f,0.35f), new Color(0.30f,0.62f,1.0f),
            new Color(0.75f,0.40f,1.0f),  new Color(1.0f,0.76f,0.20f)
        };

        public static ItemDef Roll(System.Random rng, int bonus)
        {
            // Weighted rarity, shifted up for better containers.
            float[] w = { 55, 28, 12, 4, 1 };
            for (int i = 0; i < bonus; i++) { w[0] *= 0.5f; w[2] *= 1.5f; w[3] *= 1.8f; w[4] *= 2f; }
            float t = 0; foreach (var x in w) t += x;
            float r = (float)rng.NextDouble() * t; int rar = 0;
            for (int i = 0; i < 5; i++) { if (r < w[i]) { rar = i; break; } r -= w[i]; }
            var pool = new List<ItemDef>();
            foreach (var it in Items) if (it.rarity == rar) pool.Add(it);
            return pool[rng.Next(pool.Count)];
        }
    }

    [Serializable]
    public class SaveData
    {
        public int cash = 1500;
        public List<string> owned = new List<string> { "pistol", "katana" };
        public string primary = "", secondary = "pistol", melee = "katana";
        public List<string> stash = new List<string>();
        public string nick = "Raider";
        public string lobby = "";
        public int quality = 3;
        public float sens = 1f;
        public bool pvp = true;
        public int raids, extracts, kills;

        static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, "deadhaul_save.json");

        public static SaveData Load()
        {
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    var s = JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(FilePath));
                    if (s != null) { s.Fix(); return s; }
                }
            }
            catch (Exception e) { Debug.LogWarning("Save load failed: " + e.Message); }
            var d = new SaveData(); d.Fix(); return d;
        }

        public void Save()
        {
            try { System.IO.File.WriteAllText(FilePath, JsonUtility.ToJson(this)); }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
        }

        public void Fix()
        {
            if (owned == null) owned = new List<string>();
            if (!owned.Contains("pistol")) owned.Add("pistol");
            if (!owned.Contains("katana")) owned.Add("katana");
            if (stash == null) stash = new List<string>();
            if (string.IsNullOrEmpty(secondary) || !owned.Contains(secondary)) secondary = "pistol";
            if (string.IsNullOrEmpty(melee) || !owned.Contains(melee)) melee = "katana";
            if (!string.IsNullOrEmpty(primary) && !owned.Contains(primary)) primary = "";
            if (string.IsNullOrEmpty(nick)) nick = "Raider";
            if (sens <= 0.05f) sens = 1f;
            quality = Mathf.Clamp(quality, 0, 3);
        }
    }
}
