using System;

namespace FarmIdle
{
    // Precio final de una unidad = precio base x mejoras x buffs x prestigio.
    // Es C# puro, sin UnityEngine: se prueba sin escena. Vive en Managers porque la usa el EconomyManager, que es el
    // dueño de la economía.
    //   - El precio base sale de CropData.SellPrice.
    //   - Las mejoras (Fase 6, permanentes) y los buffs de anuncio (Fase 8, temporales) son dos multiplicadores
    //     separados que fijan quienes los manejan (un sistema de mejoras, el AdsManager); acá no se sabe de dónde
    //     vienen. Hoy valen 1: el precio final es el base.
    //   - Se redondea al entero más cercano (la mitad hacia arriba), porque la billetera cuenta monedas enteras.
    public sealed class PriceCalculator
    {
        private float _upgradeMultiplier = 1f;
        private float _buffMultiplier = 1f;
        private float _prestigeMultiplier = 1f;
        private float _charmMultiplier = 1f;

        // Multiplicador del encanto de las decoraciones compradas (1 = ninguna). Lo fija DecorationManager.
        public float CharmMultiplier => _charmMultiplier;

        public void SetCharmMultiplier(float multiplier)
        {
            _charmMultiplier = Validate(multiplier, nameof(multiplier));
        }

        // Multiplicador de las mejoras compradas (1 = sin mejoras).
        public float UpgradeMultiplier => _upgradeMultiplier;

        // Multiplicador permanente de las temporadas completadas (F-03; 1 = ninguna). Lo fija PrestigeManager.
        public float PrestigeMultiplier => _prestigeMultiplier;

        // Multiplicador de los buffs activos, por ejemplo "x2 por ver un anuncio" (1 = sin buff).
        public float BuffMultiplier => _buffMultiplier;

        public void SetUpgradeMultiplier(float multiplier)
        {
            _upgradeMultiplier = Validate(multiplier, nameof(multiplier));
        }

        public void SetBuffMultiplier(float multiplier)
        {
            _buffMultiplier = Validate(multiplier, nameof(multiplier));
        }

        public void SetPrestigeMultiplier(float multiplier)
        {
            _prestigeMultiplier = Validate(multiplier, nameof(multiplier));
        }

        // El precio de una unidad con los multiplicadores actuales.
        public long FinalPrice(int basePrice)
        {
            return FinalPrice(basePrice, _upgradeMultiplier, _buffMultiplier, _prestigeMultiplier * _charmMultiplier);
        }

        // La cuenta sola, sin estado: sirve para mostrar "cuánto pagaría" con otros multiplicadores (por ejemplo, el
        // precio con la mejora que se está por comprar).
        public static long FinalPrice(int basePrice, float upgradeMultiplier, float buffMultiplier)
        {
            return FinalPrice(basePrice, upgradeMultiplier, buffMultiplier, 1f);
        }

        public static long FinalPrice(int basePrice, float upgradeMultiplier, float buffMultiplier, float prestigeMultiplier)
        {
            if (basePrice < 0) throw new ArgumentOutOfRangeException(nameof(basePrice), "El precio base no puede ser negativo.");
            Validate(upgradeMultiplier, nameof(upgradeMultiplier));
            Validate(buffMultiplier, nameof(buffMultiplier));
            Validate(prestigeMultiplier, nameof(prestigeMultiplier));

            // Los multiplicadores se redondean a 6 decimales antes de la cuenta: un 1,3f guarda 1,29999995, y sin esto
            // 5 x 1,3 daría 6,4999997 y se pagaría 6 en vez de 7.
            double price = Math.Round((double)basePrice * Math.Round((double)upgradeMultiplier, 6) * Math.Round((double)buffMultiplier, 6) * Math.Round((double)prestigeMultiplier, 6), MidpointRounding.AwayFromZero);
            return price >= long.MaxValue ? long.MaxValue : (long)price;
        }

        private static float Validate(float multiplier, string parameter)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
            {
                throw new ArgumentOutOfRangeException(parameter, multiplier, "El multiplicador tiene que ser un número finito y no negativo.");
            }

            return multiplier;
        }
    }
}
