using System;
using System.Collections.Generic;
using Battle.Enemies;
using Battle.Enemies.Behaviours;
using Cruciball;
using Data;
using HarmonyLib;
using Map;
using PeglinBalatroScore.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Worldmap;

namespace PeglinBalatroScore
{
    /// <summary>
    /// The score to aim for: the max HP of this act's big boss (not its minions). The map picks the boss when the act is
    /// generated, so this is known in every battle of the act, not just the boss fight.
    /// </summary>
    internal static class BossTarget
    {
        private static readonly AccessTools.FieldRef<MapController, CruciballManager> CruciballRef =
            AccessTools.FieldRefAccess<MapController, CruciballManager>("_cruciballManager");

        private static readonly AccessTools.FieldRef<MapController, int> FloorRef =
            AccessTools.FieldRefAccess<MapController, int>("floorCount");

        private static string _cachedKey;
        private static long _cachedTarget;

        /// <summary>Which node of the act this is (the "Floor N" the game shows), or 1 if there's no map.</summary>
        public static int FloorInAct()
        {
            MapController map = MapController.instance;
            return map != null ? FloorRef(map) : 1;
        }

        /// <summary>The boss's max HP, or 0 if there's no map (e.g. a scenario) or the boss can't be read.</summary>
        public static long Get()
        {
            try
            {
                MapController map = MapController.instance;
                MapNode bossNode = map != null ? map.bossNode : null;
                if (bossNode == null || bossNode.potentialMapData == null
                    || bossNode.selectedBossIndex < 0 || bossNode.selectedBossIndex >= bossNode.potentialMapData.Length)
                {
                    return 0;
                }
                MapDataBattle fight = bossNode.potentialMapData[bossNode.selectedBossIndex] as MapDataBattle;
                if (fight == null)
                {
                    return 0;
                }
                CruciballManager cruciball = CruciballRef(map);
                int cruciballLevel = cruciball != null ? Math.Max(0, cruciball.currentCruciballLevel) : 0;
                string key = $"{map.Act}|{fight.name}|{cruciballLevel}";
                if (key == _cachedKey)
                {
                    return _cachedTarget;
                }

                List<string> described = new List<string>();
                _cachedTarget = BossHealthRules.TargetHealth(ReadSpawns(fight, described), cruciballLevel);
                _cachedKey = key;
                ModLog.Info($"Act {map.Act} boss is {fight.name} ({fight.bossType}): score target {_cachedTarget} HP at Cruciball {cruciballLevel}. " +
                    $"Spawns: {(described.Count > 0 ? string.Join("; ", described) : "none found")}");
                return _cachedTarget;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Couldn't read the act boss's HP, flames will use the enemies on screen instead: {e.Message}");
                _cachedKey = null;
                return 0;
            }
        }

        /// <summary>Every enemy the boss fight spawns, in spawn order, with the HP its prefab starts on.</summary>
        private static List<BossFightSpawn> ReadSpawns(MapDataBattle fight, List<string> described)
        {
            List<BossFightSpawn> spawns = new List<BossFightSpawn>();
            if (fight.starterSpawns != null)
            {
                foreach (StarterSpawn starter in fight.starterSpawns)
                {
                    AddSpawn(spawns, described, 0, starter?.spawnData);
                }
            }
            if (fight.waveGroups != null)
            {
                foreach (WaveGroup group in fight.waveGroups)
                {
                    if (group?.waveData == null)
                    {
                        continue;
                    }
                    foreach (WaveData wave in group.waveData)
                    {
                        if (wave != null)
                        {
                            AddSpawn(spawns, described, wave.turnNumber, wave.spawnData);
                        }
                    }
                }
            }
            return spawns;
        }

        private static void AddSpawn(List<BossFightSpawn> spawns, List<string> described, int turn, SpawnData spawnData)
        {
            if (spawnData == null)
            {
                return;
            }
            AddSpawn(spawns, described, turn, spawnData.enemyAssetReference);
            if (spawnData.randomizedAssets != null)
            {
                foreach (AssetReferenceGameObject randomized in spawnData.randomizedAssets)
                {
                    AddSpawn(spawns, described, turn, randomized);
                }
            }
        }

        private static void AddSpawn(List<BossFightSpawn> spawns, List<string> described, int turn, AssetReferenceGameObject reference)
        {
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                return;
            }
            // Load by key rather than through the AssetReference: loading the same AssetReference twice is an error,
            // and the game loads these itself when the fight starts.
            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(reference.RuntimeKey);
            try
            {
                GameObject prefab = handle.WaitForCompletion();
                // Some prefabs keep the Enemy on a child object rather than the root.
                Enemy enemy = prefab != null ? prefab.GetComponentInChildren<Enemy>(true) : null;
                if (enemy != null)
                {
                    bool isBoss = enemy.enemyTypes.HasFlag(Enemy.EnemyType.Boss);
                    float health = enemy.StartingHealth;
                    float cruciballHealth = enemy.StartingHealthFirstCruciball;
                    // Some enemies swap in per-act HP when they spawn, so the prefab's own numbers aren't the real ones.
                    ScaleHealthAndDamageByAct byAct = enemy.GetComponent<ScaleHealthAndDamageByAct>();
                    if (byAct != null)
                    {
                        ActHealth(byAct, MapController.instance.Act, ref health, ref cruciballHealth);
                    }
                    spawns.Add(new BossFightSpawn(turn, isBoss, health, cruciballHealth));
                    described.Add($"{prefab.name} turn {turn}{(isBoss ? " boss" : "")} {health}/{cruciballHealth} HP{(byAct != null ? " (scaled by act)" : "")}");
                }
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        /// <summary>The same per-act HP ScaleHealthAndDamageByAct.Awake gives the enemy when it spawns.</summary>
        private static void ActHealth(ScaleHealthAndDamageByAct byAct, int act, ref float health, ref float cruciballHealth)
        {
            switch (act)
            {
                case 1:
                    health = byAct.act1Health;
                    cruciballHealth = byAct.act1CruciballHealth;
                    break;
                case 2:
                    health = byAct.act2Health;
                    cruciballHealth = byAct.act2CruciballHealth;
                    break;
                case 3:
                    health = byAct.act3Health;
                    cruciballHealth = byAct.act3CruciballHealth;
                    break;
                case 4:
                    health = byAct.act4Health;
                    cruciballHealth = byAct.act4CruciballHealth;
                    break;
            }
        }
    }
}
