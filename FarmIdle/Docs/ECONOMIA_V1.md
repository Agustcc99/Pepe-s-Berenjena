# Economía v1.0 (B-02 y F-04)

Resultado de calibrar el bot de balance (`Assets/_Tests/EditMode/BalanceBot.cs`) contra el plan de ritmo `EconomyPlan.cs`. Los costos viven en las tablas de `ProgressionData` (se aplican con `FarmIdle > Fase 7 > Aplicar balance de cultivos y mejoras`) y `levelBaseXp` en `Phase7SceneBuilder.LevelBaseXp`.

## Supuesto central (a verificar)

El bot es perfecto; un jugador real tarda **~2,5 veces más** (`EconomyPlan.PlayerSlowdown`). Evidencia: el bot se compraba la economía original en 13 min y en el Galaxy A70 se terminó en 30 a 41 min (Q-02). **A verificar** con Q-01, Q-03 y los datos del portal (N-04). Si el factor cambia, se corrige la constante y se recalibra (ver al final).

## Qué suma F-04

- **Crecimiento** y **Precio** pasan de 3 a **6 niveles** (hasta ×3 cada uno; la prueba de datos no admite más de ×3).
- **Velocidad** se queda en 3: con los perks al máximo ya da ×1,495 y el tope permanente es ×1,5 (`TheSpeedUpgrade_PlusMaxedSpeedPerks_StaysUnderThePermanentCap`). Un nivel más no se notaría y el tope de 11 u/s con el Turbo no se toca.
- **Mochila** se queda en 3: llega a 10 + 10 + 10 de perks = 30 lugares, que es lo que aguanta la pila de la espalda sin apretarse (decisión de la Fase 7, `GameConfig_PurchaseTimingAndMaxCapacity_AreSane`).
- Resultado: **23 compras** en total (5 zonas y 18 niveles de mejora), no las ~12 más que estimaba B-02.

## Tabla de compras (minutos de jugador real)

| # | Compra | Minuto | Hueco | Costo |
|---|---|---|---|---|
| 1 | Crecimiento 1 | 1,7 | 1,7 | 50 |
| 2 | Precio 1 | 4,2 | 2,5 | 290 |
| 3 | Zona Huerta del Este | 7,4 | 3,2 | 410 |
| 4 | Velocidad 1 | 10,3 | 2,9 | 600 |
| 5 | Crecimiento 2 | 13,2 | 3,0 | 900 |
| 6 | Mochila 1 | 16,8 | 3,6 | 950 |
| 7 | Precio 2 | 20,8 | 4,0 | 1.300 |
| 8 | Velocidad 2 | 25,3 | 4,4 | 1.800 |
| 9 | Mochila 2 | 30,3 | 5,0 | 2.000 |
| 10 | Crecimiento 3 | 36,4 | 6,1 | 2.500 |
| 11 | Zona Huerto Dorado | 43,2 | 6,9 | 3.400 |
| 12 | Velocidad 3 | 52,1 | 8,9 | 6.600 |
| 13 | Precio 3 | 62,5 | 10,4 | 8.600 |
| 14 | Mochila 3 | 73,8 | 11,4 | 11.000 |
| 15 | Zona Huerta del Norte | 90,4 | 16,6 | 16.000 |
| 16 | Crecimiento 4 | 105,9 | 15,4 | 23.000 |
| 17 | Precio 4 | 124,6 | 18,7 | 29.000 |
| 18 | Zona Chacra del Noroeste | 144,5 | 19,9 | 36.000 |
| 19 | Crecimiento 5 | 167,7 | 23,2 | 60.000 |
| 20 | Precio 5 | 191,3 | 23,6 | 69.000 |
| 21 | Zona Quinta del Nordeste | 216,2 | 24,9 | 83.000 (antes 85.000: con la plaza por sectores, que alarga el camino a los pads, el hueco pasó a 25,1 min; ver `DECISIONES.md`) |
| 22 | Crecimiento 6 | 238,0 | 21,8 | 112.000 |
| 23 | Precio 6 | 260,2 | 22,2 | 135.000 |

- **Todo comprado** (proxy del primer prestigio, que todavía no existe): a las 4 h 20 min de jugador real.
- **Nivel 20**: minuto ~182 (`levelBaseXp` 325; antes 125). El nivel 2 llega hacia el minuto 4 (estimado), el 5 hacia el 30.
- **Sin muros**: el hueco más largo es 24,9 min. La primera compra sale casi regalada (50 monedas, minuto 1,7) para enganchar; los huecos arrancan en 2 a 4 min y crecen a medida que el ingreso se aplana.
- El bot compra siempre lo más barato y llega al orden del plan sin forzarlo (lo verifica `ThePurchases_FollowThePlayerSchedule_InOrder_WithNoLongWalls`).

## Lo que este contenido NO alcanza

Con 23 compras el juego se agota a las ~4 h 20 min de jugador real. Para la meta del roadmap (hora 5 a 8 con una compra cada 10 a 20 min) faltan del orden de **12 a 15 compras más** (estimación: unos 220 min hasta la hora 8, a 15 a 20 min cada una). Fuentes previstas:
- **F-03** (prestigio): reinicia la curva con un multiplicador permanente y reutiliza todas las compras. Es la palanca principal.
- Más niveles solo si se rompe un tope de diseño (velocidad permanente, 30 lugares de mochila, ×3 de multiplicadores), o más zonas o cultivos.

Y "una compra cada 2 a 4 min durante toda la primera hora" no se cumple: solo hasta el minuto ~25. Las 12 compras de la primera hora tienen un hueco medio de 5 min.

## El ingreso del bot se aplana

A partir de la hora 3 el bot camina el 80% del tiempo y tiene casi todas las parcelas listas: el ingreso deja de crecer con las mejoras y lo limita cuánto se puede caminar, cosechar y vender. Por eso los últimos costos suben más despacio que la capacidad de producir. El bot elige entre parcelas listas la que más paga por segundo de viaje (un jugador de verdad va primero a lo que rinde); sin esa regla su ingreso se desplomaba al saturarse.

## Qué no modela el bot

Perks de nivel (+1 mochila, +1,5% de velocidad por elección), Turbo por video y fertilizante comprado con monedas. Los tres ayudan al jugador real, así que el factor de 2,5 los absorbe de forma burda.

## Cómo recalibrar

1. Ajustar `EconomyPlan.Entries` (minutos) o `PlayerSlowdown`.
2. Correr el test explícito `FarmIdle.Tests.EconomyCalibration.PrintTheCostTable` (filtro por nombre; no corre con la suite). Imprime el costo de cada compra, lo ganado y la XP, y el `levelBaseXp` sugerido para el nivel 20 a la hora 3.
3. Pasar los costos a las tablas de `ProgressionData` (y `LevelBaseXp` de `Phase7SceneBuilder`), correr `Phase7SceneBuilder.ApplyBalance` y la suite. Los costos tienen que quedar estrictamente crecientes en el orden del plan; si salen dos casi iguales, hay que separarlos a mano.

## B-01 · Almacén (tope offline 3 h y 4 h)

Dos compras nuevas de fin de juego (`upgrade_offline`, niveles 1 y 2 a 140.000 y 158.000 monedas). Van al final del plan porque el bot compra siempre lo más barato primero: tienen que costar más que Precio 6 (135.000). Con el ingreso del final (~6,5 a 7 k/min de jugador real) tardan 20 y 24 min, dentro del límite de 25. Resultado del bot: Almacén 1 en el min ~280 y Almacén 2 en el ~303; todo comprado a las ~5 h 5 min. Como cuenta para "todo comprado", el primer prestigio llega recién ahí. Si el factor de 2,5× del jugador real cambia (Q-01/Q-03/N-04), recalibrar con `EconomyCalibration`.

## B-08 · Fertilizante: el precio sube con las zonas abiertas

El fertilizante termina de crecer todos los campos, así que lo que entrega crece con cada granja y el precio fijo de 1.500 se quedaba corto al final. `GameConfig.FertilizerPriceFor(zonas)` multiplica el precio base (`fertilizerPrice` = 1.500) por `fertilizerPriceStepPerZone` (**×1,8**) con cada zona abierta y redondea a 50:

| Zonas abiertas | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| Precio | 1.500 | 2.700 | 4.850 | 8.750 | 15.750 | 28.350 |

- El último precio equivale a unos 4 min del ingreso del final (~6,75 mil por minuto de jugador real). **Número provisorio**: sale de la cuenta, no de una medición.
- El fertilizante por video no cambia (gratis, hasta 3 en stock). Sin ProgressionManager (pruebas con rigs) cuenta 0 zonas y vale el precio base.
- `ProgressionManager.UnlockedZoneCount` cuenta solo zonas de `GameConfig.Zones`, que son las compradas (la granja inicial no está). Una temporada nueva (F-03) vuelve a cerrar las zonas, así que el precio también vuelve a bajar.
- El `BalanceBot` no modela la compra con monedas, así que no hay cifras de balance nuevas; se recalibra junto con el resto cuando haya datos del portal (B-05).

## F-11 · Tienda de prestigio (semillas doradas)

Cada temporada cerrada da `1 + n/2` semillas (n = número de la temporada que se cierra). Seis mejoras de tres niveles, datos en `GameConfig.prestigeShop`:

| Mejora | Costo (n1/n2/n3) | Efecto total por nivel |
|---|---|---|
| Monedas iniciales | 1/1/2 | +300 / +1.000 / +3.000 al arrancar la temporada |
| Cultivos veloces | 1/2/3 | crecimiento ×1,10 / 1,20 / 1,35 |
| Depósito grande | 1/1/2 | tope offline +1 h / +2 h / +4 h |
| Buen precio | 2/3/4 | venta ×1,10 / 1,20 / 1,30 (se multiplica con el prestigio) |
| Fertilizante de regalo | 1/1/2 | 1 / 2 / 3 al arrancar la temporada |
| Aprendiz veloz | 1/2/2 | XP ×1,15 / 1,30 / 1,50 |

- Total 32 semillas: se completa en la temporada 9 (1+2+2+3+3+4+4+5+5 = 29; 10ª: 35). Venta × crecimiento al máximo = ×1,755, bajo el tope de ×2 que vigila `PrestigeShopTests`.
- **Números provisorios**: no hay bot de prestigio, es cuenta analítica. Se ajustan con datos del portal (B-05) cambiando el asset.


## U-08 · Los primeros 3 minutos

Medido con `BalanceBot` (prueba `TheFirstMinutes_TutorialSteps_HaveNoDeadZones`), en segundos de jugador real (x2,5): sembrar x3 a los 9 s, 1.a cosecha 60 s, 3.a cosecha 63 s, 1.a venta 96 s (54 monedas), 1.a compra 110 s (50 monedas, mejora de crecimiento = destino de la flecha Buy). La unica espera real es la del primer cultivo (trigo); no hay zonas muertas ni textos que ajustar (la guia son flechas). Limites que vigila la prueba: sembrar <= 30 s, sembrar->3.a cosecha <= 60 s, cosecha->venta <= 45 s, venta->compra <= 60 s, primera compra <= 180 s.

## F-17 · Sumideros de monedas, tanda 1: decoraciones

Ocho decoraciones cosméticas (sin ventaja), en orden de compra: farol 25k, espantapájaros 35k, banco 45k, parterre 55k, estanque 70k, molino 80k, bandera 90k, fardo de heno 100k. **Total 500.000** (~1,4 h con el ingreso del final, ~360.000 por hora real). Un solo pad las vende en orden y se abre con todas las zonas compradas. No afectan al bot ni al plan de compras: son cosméticas y no tocan el ingreso. Las tandas 2 (ayudantes, 100k/250k/500k) y 3 (skins) siguen pendientes y sí se recalibran con el bot.

### Ayudantes automaticos (F-17, tanda 2)
Tres niveles, en orden: Peon 100.000 (+10 %), Capataz 250.000 (+20 %), Cosechadora 500.000 (+35 %); total **850.000** (~2,4 h con ~360.000 por hora). Efecto: mientras se juega suman solos monedas = producción de las parcelas (`OfflineManager.ProductionPerSecond`) x el efecto del nivel; el tope de diseño es +50 %. Se reinician con cada temporada (como las mejoras): el sumidero vuelve en cada vuelta. **Números provisorios**: el bot de balance NO los compra (no son una mejora de la plaza), así que la curva de 4 h 20 min no cambia; se ajustan con datos reales (B-05). El efecto real depende de cuánto de la producción teórica cobra hoy el jugador activo.

### Skins del jugador (F-17, tanda 3)
Seis colores, en orden de compra: Tomate 60.000, Cielo 80.000, Hoja 100.000, Uva 120.000, Rosa 150.000, Oro 190.000; total **700.000** (~2 h). El naranja de fábrica es gratis. Cosméticas, sin ventaja, permanentes (no se reinician con la temporada). Un solo pad: compra la siguiente si alcanzan las monedas (y se la pone) o, si no, cambia entre las que ya tiene. Con las tres tandas, F-17 suma **2.050.000** monedas de sumidero (~5,7 h con el ingreso del final).

## J-04 · Cosecha rara (2026-11-05)
3 % de chance por cosecha de dar 3x el precio final al instante: en promedio +9 % de ingreso por cosecha. El bot de balance (`BalanceBot.CreditRareHarvest`) acredita ese valor esperado, sin azar, para que la simulacion siga siendo determinista. Valores en `GameConfig` (`rareHarvestChance`, `rareHarvestMultiplier`).

## F-18 · Pedidos del pueblo, tanda 1 (2026-11-05)
Cartel de 3 pedidos (el tercero es el grande), disponible con 2 zonas compradas. Entrega a mano desde la mochila (en varios viajes si hace falta); sale otro pedido 60 s después de cumplir uno normal y 600 s después del grande. Todo en `GameConfig` (cabecera "Pedidos del pueblo"); lógica en `OrderBoard` y `OrdersManager`.

| | Normal | Grande |
|---|---|---|
| Cantidad | 80 % de la mochila (4 a 24) | el doble (hasta 40) |
| Premio en monedas | unidades x precio final x 1,25 | unidades x precio final x 1,40 |
| Semilla dorada | no | 1 por pedido, máximo 2 por temporada |

Cultivos: solo los de zonas abiertas (`orderCrops`), con más peso los que más pagan (1,1,1,2,2,3,3,3). El bot de balance no entrega: supone que el 30 % de lo que vende iría a pedidos y acredita el +25 % sobre eso (`BalanceBot.OrdersDeliveredShare`), o sea ~+7,5 % de ingreso con el cartel abierto. Techo teórico si se entregara todo: +25 %. Números provisorios (B-05).
