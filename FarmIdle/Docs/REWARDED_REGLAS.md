# Reglas de rewarded v1.0 (B-03)

Todo vive en datos: una `RewardData` por ubicación (`Assets/ScriptableObjects/Rewards`) y el tope entre anuncios en `GameConfig`. Cambios de balance: `Phase8SceneBuilder.ApplyBalance`.

| Ubicación | Recompensa | Cómo se pide (gesto del jugador) | Enfriamiento propio (tiempo de juego) | Se ofrece solo si |
|---|---|---|---|---|
| Fertilizante | +1 al stock | Pararse en el pad de video | 120 s | el stock no está lleno |
| Turbo | x3 de velocidad, 30 s | Pararse en el pad fijo de Turbo (`TurboPad_Video`, primera columna de la fila del fertilizante en la plaza; D-14) o en el símbolo de oferta (aparece cada 2 a 3 min en uno de 4 lugares). **En la web no aparece el símbolo** (D-04: la regla de CrazyGames no admite una oferta que aparece sola con el juego activo; `OfferManager.SymbolsAllowedOn`), así que ahí el Turbo se pide solo desde el pad. El pad y el símbolo comparten el mismo enfriamiento. | 90 s | el buff no está activo |
| Duplicar ganancia offline | x2 de lo ganado fuera | Botón "Duplicar" de la tarjeta de bienvenida | 0 (una vez por regreso) | hay ganancia pendiente |

- **Mínimo entre dos anuncios cualquiera:** `GameConfig.adMinIntervalSeconds` = **180 s** (reloj real). Es el mismo piso de ~3 min del SDK de CrazyGames, que cuenta también los rewarded (ver `CRAZYGAMES_REQUISITOS.md`, sección 4). Mientras corre, los pads muestran "Esperá N s" y el botón Duplicar queda apagado.
- **Nunca forzado:** el único código que pide un anuncio son `RewardPad` (pararse) y `OfflineRewardView` (tocar un botón). Lo garantiza `ArchitectureTests.OnlyPlayerGestures_RequestARewardedAd`. El símbolo de oferta solo aparece cuando `Evaluate` da `Ready`.
- **Sin botones sin efecto:** si no hay anuncio listo (`NoAd`) o no serviría (`NotUseful`) el pad lo dice y no pide nada.
- **Pendiente:** "doble premio de misión" no existe todavía (haría falta una `RewardKind` nueva, botón en la tarjeta de misiones y textos). Queda como fila nueva propuesta, no hecha.
- **Orden con CrazyGames:** en *Basic Launch* los anuncios están apagados; las reglas de arriba rigen para *Full Launch* (y Android/AdMob). El ajuste del orden D-03/B-03 sigue sin aplicar (ver `CRAZYGAMES_REQUISITOS.md`).
