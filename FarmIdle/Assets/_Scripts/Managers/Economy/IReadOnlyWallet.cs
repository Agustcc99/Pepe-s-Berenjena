using System;

namespace FarmIdle
{
    // Lo que la UI y el gameplay pueden hacer con la billetera: mirar el saldo y escuchar sus cambios.
    // Para ganar o gastar monedas hay que pasar por EconomyManager.
    public interface IReadOnlyWallet
    {
        long Balance { get; }

        // (saldo nuevo, variación). La variación es negativa cuando se gasta.
        event Action<long, long> BalanceChanged;
    }
}
