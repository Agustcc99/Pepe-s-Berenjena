using System;
using System.Reflection;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Zonas y mejoras de prueba para las pruebas de EditMode: los campos de los ScriptableObjects son privados
    // ([SerializeField]) y se cablean por reflexión, como lo haría el Inspector.
    internal static class ProgressionTestData
    {
        private static void Set(object target, string fieldName, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;

                field.SetValue(target, value);
                return;
            }

            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        public static UpgradeData Upgrade(string id, UpgradeKind kind, params (long cost, float value)[] levels)
        {
            var upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            upgrade.name = id;
            Set(upgrade, "id", id);
            Set(upgrade, "displayName", id);
            Set(upgrade, "kind", kind);

            var table = new UpgradeLevel[levels.Length];
            for (int i = 0; i < levels.Length; i++) table[i] = new UpgradeLevel(levels[i].cost, levels[i].value);
            Set(upgrade, "levels", table);
            return upgrade;
        }

        public static PerkData Perk(string id, PerkKind kind, float valuePerPick, int maxPicks)
        {
            var perk = ScriptableObject.CreateInstance<PerkData>();
            perk.name = id;
            Set(perk, "id", id);
            Set(perk, "displayName", id);
            Set(perk, "shortLabel", id);
            Set(perk, "kind", kind);
            Set(perk, "valuePerPick", valuePerPick);
            Set(perk, "maxPicks", maxPicks);
            return perk;
        }

        // Una recompensa de anuncio de prueba (Fase 8).
        public static RewardData Reward(string id, RewardKind kind, float magnitude, float durationSeconds = 10f, float cooldownSeconds = 0f)
        {
            var reward = ScriptableObject.CreateInstance<RewardData>();
            reward.name = id;
            Set(reward, "id", id);
            Set(reward, "displayName", id);
            Set(reward, "shortLabel", id);
            Set(reward, "kind", kind);
            Set(reward, "magnitude", magnitude);
            Set(reward, "durationSeconds", durationSeconds);
            Set(reward, "cooldownSeconds", cooldownSeconds);
            return reward;
        }

        public static ZoneData Zone(string id, long cost)
        {
            var zone = ScriptableObject.CreateInstance<ZoneData>();
            zone.name = id;
            Set(zone, "id", id);
            Set(zone, "displayName", id);
            Set(zone, "unlockCost", cost);
            return zone;
        }
    }
}
