using System.Collections.Generic;
using UnityEngine;

namespace DeadHaul
{
    public enum ContainerKind { Crate, Locker, Dumpster, Safe, Trunk, Body, HunterBody }

    // A loot item in a container or the player's bag. Guns found in the world carry a gun id.
    public class Loot
    {
        public ItemDef item;
        public string gun;
        public int cash;

        public string Name => gun != null ? Defs.Gun(gun).name : cash > 0 ? "Loose cash" : item.name;
        public int Value => gun != null ? Mathf.RoundToInt(Mathf.Max(800, Defs.Gun(gun).price * 0.45f)) : cash > 0 ? cash : item.val;
        public float Kg => gun != null ? 3f : cash > 0 ? 0f : item.kg;
        public int Rarity => gun != null ? 2 : cash > 0 ? (cash > 300 ? 1 : 0) : item.rarity;
        public string SaveId => gun != null ? "gun:" + gun : item != null ? item.id : null;
    }

    public class Container : MonoBehaviour
    {
        public ContainerKind kind;
        public List<Loot> items = new List<Loot>();
        public bool searched;
        public float searchTime = 2f;

        public string Label
        {
            get
            {
                switch (kind)
                {
                    case ContainerKind.Body: return "Infected";
                    case ContainerKind.HunterBody: return "Hunter";
                    case ContainerKind.Trunk: return "Car trunk";
                    default: return kind.ToString();
                }
            }
        }

        public void Init(ContainerKind k, int seed)
        {
            kind = k;
            var rng = new System.Random(seed);
            int n, bonus = 0;
            switch (k)
            {
                case ContainerKind.Safe: n = rng.Next(1, 3); bonus = 3; searchTime = 4f; break;
                case ContainerKind.Dumpster: n = rng.Next(1, 4); searchTime = 2.6f; break;
                case ContainerKind.Locker: n = rng.Next(2, 4); bonus = 1; searchTime = 2f; break;
                case ContainerKind.Trunk: n = rng.Next(1, 4); bonus = 1; searchTime = 2.2f; break;
                case ContainerKind.Body: n = rng.Next(0, 3); searchTime = 1.4f; break;
                case ContainerKind.HunterBody: n = rng.Next(2, 4); bonus = 2; searchTime = 1.8f; break;
                default: n = rng.Next(2, 5); searchTime = 2f; break;
            }
            for (int i = 0; i < n; i++) items.Add(new Loot { item = Defs.Roll(rng, bonus) });
            if (rng.NextDouble() < (k == ContainerKind.Safe ? 0.8 : 0.35))
                items.Add(new Loot { cash = rng.Next(40, k == ContainerKind.Safe ? 1800 : 400) });
            // Guns turn up on hunters and, rarely, in crates and trunks.
            double gunChance = k == ContainerKind.HunterBody ? 0.45 : k == ContainerKind.Crate || k == ContainerKind.Trunk || k == ContainerKind.Locker ? 0.06 : 0;
            if (rng.NextDouble() < gunChance)
            {
                var pool = new List<string>();
                foreach (var g in Defs.Guns) if (!g.melee && g.id != "pistol") pool.Add(g.id);
                items.Add(new Loot { gun = pool[rng.Next(pool.Count)] });
            }
        }
    }
}
