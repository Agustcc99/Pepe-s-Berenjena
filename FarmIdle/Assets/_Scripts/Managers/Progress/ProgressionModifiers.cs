using System;
using System.Collections.Generic;

namespace FarmIdle
{
    // El efecto total de las mejoras compradas, listo para repartir: un número por cada cosa que las mejoras cambian.
    // Es C# puro, sin UnityEngine: se calcula y se prueba sin escena. ProgressionManager lo recalcula en cada cambio y
    // cada capa toma lo suyo (la mochila, el jugador, el precio, las plantas); acá no se sabe cómo se aplica.
    //   - La capacidad suma lugares; los demás efectos multiplican (dos mejoras del mismo tipo se acumulan).
    //   - El nivel sin comprar es neutro: 0 lugares extra, multiplicadores en 1.
    //   - Los perks de nivel (Fase 7) suman a lo mismo: lugares extra de mochila, y un porcentaje de velocidad que se SUMA entre
    //     perks y después se multiplica con las mejoras de monedas. La velocidad permanente total tiene un tope (maxMoveSpeed)
    //     para que el buff temporal de la Fase 8 se siga notando y el movimiento siga siendo controlable.
    public readonly struct ProgressionModifiers
    {
        public readonly int BackpackBonus;       // lugares extra de la mochila
        public readonly float MoveSpeed;         // multiplicador de la velocidad de movimiento
        public readonly float SellPrice;         // multiplicador del precio de venta (entra en PriceCalculator)
        public readonly float GrowthSpeed;       // multiplicador de la velocidad de crecimiento
        public readonly int OfflineCapBonusSeconds;   // segundos extra sobre el tope base del progreso offline (B-01)

        public ProgressionModifiers(int backpackBonus, float moveSpeed, float sellPrice, float growthSpeed, int offlineCapBonusSeconds = 0)
        {
            BackpackBonus = backpackBonus;
            MoveSpeed = moveSpeed;
            SellPrice = sellPrice;
            GrowthSpeed = growthSpeed;
            OfflineCapBonusSeconds = offlineCapBonusSeconds;
        }

        // Sin mejoras.
        public static ProgressionModifiers None => new ProgressionModifiers(0, 1f, 1f, 1f);

        // Calcula los efectos de 'progress' con la tabla de 'upgrades' y, si se pasa, la de 'perks'. Los elementos nulos de
        // las tablas se saltean. 'maxMoveSpeed' > 0 acota el multiplicador total de velocidad (0 = sin tope).
        public static ProgressionModifiers Compute(IReadOnlyPlayerProgress progress, IReadOnlyList<UpgradeData> upgrades,
            IReadOnlyList<PerkData> perks = null, float maxMoveSpeed = 0f)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            int backpackBonus = 0;
            float moveSpeed = 1f;
            float sellPrice = 1f;
            float growthSpeed = 1f;
            int offlineCapBonus = 0;

            if (upgrades != null)
            {
                for (int i = 0; i < upgrades.Count; i++)
                {
                    UpgradeData upgrade = upgrades[i];
                    if (upgrade == null) continue;

                    float value = upgrade.ValueAtLevel(progress.LevelOf(upgrade.Id));
                    switch (upgrade.Kind)
                    {
                        case UpgradeKind.BackpackCapacity: backpackBonus += Math.Max(0, (int)Math.Round(value)); break;
                        case UpgradeKind.MoveSpeed: moveSpeed *= value; break;
                        case UpgradeKind.SellPrice: sellPrice *= value; break;
                        case UpgradeKind.GrowthSpeed: growthSpeed *= value; break;
                        case UpgradeKind.OfflineCap: offlineCapBonus += Math.Max(0, (int)Math.Round(value)); break;
                    }
                }
            }

            if (perks != null)
            {
                float perkSpeed = 0f;
                for (int i = 0; i < perks.Count; i++)
                {
                    PerkData perk = perks[i];
                    if (perk == null) continue;

                    float total = perk.TotalAt(progress.PerkPicksOf(perk.Id));
                    switch (perk.Kind)
                    {
                        case PerkKind.BackpackCapacity: backpackBonus += Math.Max(0, (int)Math.Round(total)); break;
                        case PerkKind.MoveSpeed: perkSpeed += total; break;
                    }
                }

                moveSpeed *= 1f + perkSpeed;
            }

            if (maxMoveSpeed > 0f) moveSpeed = Math.Min(moveSpeed, maxMoveSpeed);

            return new ProgressionModifiers(backpackBonus, moveSpeed, sellPrice, growthSpeed, offlineCapBonus);
        }
    }
}
