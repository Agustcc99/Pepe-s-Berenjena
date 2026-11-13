using UnityEngine;

namespace FarmIdle
{
    // El pad de una mejora: parado encima se paga el próximo nivel (ver PurchasePad). El cartel muestra el nombre, el efecto del
    // nivel que se compra, cuántos niveles lleva y las monedas que faltan; al llegar al máximo dice MAX y el pad deja de cobrar.
    // El efecto no se aplica acá: ProgressionManager lo reparte a la capa que corresponde cuando se completa el pago.
    public sealed class UpgradePad : PurchasePad
    {
        [SerializeField, Tooltip("La mejora que compra este pad.")]
        private UpgradeData upgrade;

        public UpgradeData Upgrade => upgrade;

        protected override bool IsPurchasable()
        {
            return upgrade != null && Progression != null && !Progression.IsMaxed(upgrade);
        }

        protected override void ReadPrice(out long cost, out long paid)
        {
            cost = Progression.CostOfNextLevel(upgrade);
            paid = Progression.PaidFor(upgrade);
        }

        protected override long Pay(long coins)
        {
            return Progression.TryPay(upgrade, coins);
        }

        // Dos líneas: el nombre y, abajo, lo que da el próximo nivel con cuántos lleva: "Mochila" / "+4 lugares (1/3)". Al máximo, "Mochila" / "(3/3)".
        protected override string BuildTitle()
        {
            if (upgrade == null) return Loc.Get("pad.upgrade");
            if (Progression == null) return upgrade.DisplayName;

            int level = Progression.LevelOf(upgrade);
            char newLine = (char)10;
            if (level >= upgrade.MaxLevel) return upgrade.DisplayName + newLine + "(" + level + "/" + upgrade.MaxLevel + ")";

            return upgrade.DisplayName + newLine + upgrade.Describe(level + 1) + " (" + level + "/" + upgrade.MaxLevel + ")";
        }

        protected override bool IsCompletion(ProgressChange change)
        {
            return upgrade != null && change.Kind == ProgressChangeKind.UpgradeLevelChanged && change.Id == upgrade.Id;
        }
    }
}
