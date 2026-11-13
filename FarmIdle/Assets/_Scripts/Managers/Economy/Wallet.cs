using System;

namespace FarmIdle
{
    // La billetera: guarda el saldo y es la autoridad sobre él (nunca baja de 0).
    // Es C# puro, sin UnityEngine y sin conocer a la UI: cuando el saldo cambia solo dispara BalanceChanged,
    // y quien quiera mostrarlo (el HUD) se suscribe. Toda operación es instantánea; las animaciones son cosméticas.
    public sealed class Wallet : IReadOnlyWallet
    {
        public long Balance { get; private set; }

        public event Action<long, long> BalanceChanged;

        public Wallet(long initialBalance = 0)
        {
            Balance = Math.Max(0L, initialBalance);
        }

        public bool CanAfford(long amount)
        {
            return amount >= 0 && Balance >= amount;
        }

        public void Add(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Add no admite montos negativos; para gastar usá TrySpend.");
            if (amount == 0) return;

            // Suma saturada: si algún día se acerca al máximo, se queda ahí en vez de dar la vuelta a negativo.
            long next = amount > long.MaxValue - Balance ? long.MaxValue : Balance + amount;
            long delta = next - Balance;
            if (delta == 0) return;

            Balance = next;
            BalanceChanged?.Invoke(Balance, delta);
        }

        // Fija el saldo de golpe (partida guardada o nueva). Nunca baja de 0. Avisa como cualquier otro cambio, con la variación.
        public void Restore(long balance)
        {
            long next = Math.Max(0L, balance);
            long delta = next - Balance;
            if (delta == 0) return;

            Balance = next;
            BalanceChanged?.Invoke(Balance, delta);
        }

        // Devuelve false (sin tocar nada) si no alcanza el saldo.
        public bool TrySpend(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "TrySpend no admite montos negativos.");
            if (amount == 0) return true;
            if (Balance < amount) return false;

            Balance -= amount;
            BalanceChanged?.Invoke(Balance, -amount);
            return true;
        }
    }
}
