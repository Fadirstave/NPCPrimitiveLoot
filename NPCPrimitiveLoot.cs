using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NPCPrimitiveLoot", "FadirStave", "7.1.1")]
    [Description("Drops a configurable stash with weighted primitive loot for HumanNPCs.")]
    public class NPCPrimitiveLoot : RustPlugin
    {
        private ConfigData config;

        // ------------------ CONFIG STRUCTS ------------------

        public class LootEntry
        {
            public string shortname;
            public int min;
            public int max;
            public float weight;
        }

        public class ConfigData
        {
            public List<LootEntry> LootPool;
            public int ItemsPerDrop;
            public float StashDespawnSeconds;
        }

        // ------------------ DEFAULT CONFIG ------------------

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating new default NPCPrimitiveLoot config...");

            ConfigData defaultConfig = new ConfigData()
            {
                ItemsPerDrop = 3,
                StashDespawnSeconds = 300f,

                LootPool = new List<LootEntry>()
                {
                    new LootEntry { shortname = "wood", min = 25, max = 75, weight = 5f },
                    new LootEntry { shortname = "stones", min = 25, max = 75, weight = 5f },
                    new LootEntry { shortname = "cloth", min = 5, max = 20, weight = 3f },
                    new LootEntry { shortname = "bone.fragments", min = 5, max = 20, weight = 3f },
                    new LootEntry { shortname = "scrap", min = 3, max = 10, weight = 1f },
                    new LootEntry { shortname = "arrow.wooden", min = 5, max = 20, weight = 2f },
                    new LootEntry { shortname = "arrow.bone", min = 3, max = 10, weight = 1.5f },
                    new LootEntry { shortname = "torch", min = 1, max = 1, weight = 1.5f },
                    new LootEntry { shortname = "stone.pickaxe", min = 1, max = 1, weight = 0.8f },
                    new LootEntry { shortname = "stonehatchet", min = 1, max = 1, weight = 0.8f },
                    new LootEntry { shortname = "mace", min = 1, max = 1, weight = 0.5f }
                }
            };

            Config.WriteObject(defaultConfig, true);
        }

        private void LoadConfigValues()
        {
            config = Config.ReadObject<ConfigData>();
            if (config == null || config.LootPool == null)
            {
                PrintError("Config invalid, regenerating...");
                LoadDefaultConfig();
                config = Config.ReadObject<ConfigData>();
            }
        }

        private void Init()
        {
            LoadConfigValues();
        }

        // ------------------ NPC DETECTION ------------------

        private bool IsFakeNPC(BasePlayer player)
        {
            return player != null && !player.IsConnected && player.userID < 10000000000000000UL;
        }

        // ------------------ NPC DEATH HANDLER ------------------

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer npc = entity as BasePlayer;
            if (npc == null || !IsFakeNPC(npc))
                return;

            SpawnStash(npc.transform.position);
        }

        // ------------------ SPAWN LOOT STASH ------------------

        private void SpawnStash(Vector3 position)
        {
            const string prefab = "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";

            var stash = GameManager.server.CreateEntity(prefab, position, Quaternion.identity);
            if (stash == null)
            {
                PrintError("[NPCPrimitiveLoot] Failed to spawn stash!");
                return;
            }

            stash.Spawn();

            var storage = stash.GetComponent<StorageContainer>();
            if (storage?.inventory == null)
            {
                PrintError("[NPCPrimitiveLoot] Stash missing StorageContainer!");
                return;
            }

            storage.inventory.Clear();

            AddWeightedLoot(storage.inventory);

            // Timed despawn
            timer.Once(config.StashDespawnSeconds, () =>
            {
                if (stash != null && !stash.IsDestroyed)
                    stash.Kill();
            });
        }

        // ------------------ DESPAWN WHEN LOOTED EMPTY ------------------

        private void OnLootEntityEnd(BasePlayer player, BaseEntity entity)
        {
            StorageContainer stash = entity as StorageContainer;
            if (stash == null)
                return;

            if (!stash.ShortPrefabName.Contains("small_stash"))
                return;

            if (stash.inventory.IsEmpty())
                stash.Kill();
        }

        // ------------------ WEIGHTED LOOT LOGIC ------------------

        private void AddWeightedLoot(ItemContainer container)
        {
            for (int i = 0; i < config.ItemsPerDrop; i++)
            {
                LootEntry entry = GetRandomWeightedItem();
                if (entry == null)
                    continue;

                int amount = UnityEngine.Random.Range(entry.min, entry.max + 1);
                GiveItem(container, entry.shortname, amount);
            }
        }

        private LootEntry GetRandomWeightedItem()
        {
            float total = 0f;
            foreach (var e in config.LootPool)
                total += e.weight;

            float roll = UnityEngine.Random.Range(0f, total);
            float sum = 0f;

            foreach (var e in config.LootPool)
            {
                sum += e.weight;
                if (roll <= sum)
                    return e;
            }

            return null;
        }

        private void GiveItem(ItemContainer container, string shortname, int amount)
        {
            var def = ItemManager.FindItemDefinition(shortname);
            if (def == null)
            {
                PrintError($"[NPCPrimitiveLoot] Unknown item: {shortname}");
                return;
            }

            var item = ItemManager.Create(def, amount);
            if (item != null)
                item.MoveToContainer(container);
        }
    }
}
