# Recetas de cambio (AR-02)

Pasos para los cambios que más se repiten: qué tocar, qué regenerar, qué pruebas te cuidan y dónde están las trampas. Cada receta dice su estado:

- **Probada**: la hice de verdad (cambio real, regenerar, pruebas) y quedó en verde.
- **Leída**: armada leyendo el código y las pruebas, sin ejecutarla. Si un paso falla, corregí la receta acá.

## Reglas comunes

- **Regenerar** es `FarmIdle > Fase 2 > Construir assets y escenas` (`Phase2SceneBuilder.BuildAll`, ~25 s). En batch: `-executeMethod FarmIdle.EditorTools.Phase2SceneBuilder.BuildAll -quit`. Las escenas no se editan a mano.
- Los builders **no pisan** assets que ya existen (respetan lo ajustado a mano). Lo nuevo entra al **final** de la lista del `GameConfig` (`Phase7SceneBuilder.AppendMissing`) aunque lo pongas en el medio del arreglo del builder: ponelo siempre al final del arreglo, así una base limpia da el mismo orden.
- Para **cambiar un número** de un asset que ya existe no alcanza con editar el builder: hay que correr también `FarmIdle > Fase 7 > Aplicar balance de cultivos y mejoras` (`Phase7SceneBuilder.ApplyBalance`: cultivos, zonas, mejoras y perks) o `FarmIdle > Fase 8 > Aplicar balance de recompensas`. Misiones y logros no tienen "aplicar": se ajusta el asset.
- Todo dato (`GameDataAsset`) necesita su nombre en el CSV con la clave `data.<id>`. El **Id es un contrato** (se guarda en la partida): una vez usado, no se cambia.
- Un enum que usan los assets guarda el número: agregá valores **al final**, nunca en el medio.
- Al terminar: EditMode completo (~20 s) y, si tocaste el mundo o la UI, PlayMode (~10 min, en segundo plano; para confirmar un arreglo alcanza con `-testFilter "FarmIdle.Tests.<Clase>"`). Después, `ROADMAP.md` y, si fue una decisión nueva, `DECISIONES.md`.

---

## 1. Texto localizado — Leída

**Tocás:** `Assets/Resources/Localization.csv`, una fila `clave,es,en`. Prefijo según la pantalla (ver el comentario del encabezado del CSV: `menu.`, `settings.`, `hud.`, `pad.`, `ad.`, `fmt.`, `data.<id>`...). Celdas con comas entre comillas dobles; `{0}`, `{1}` se completan al mostrar y **tienen que estar en los dos idiomas**.
**Usarlo:** en código `Loc.Get("clave")` o `Loc.Format("clave", ...)`; en un builder, `"@clave"` (queda enlazado con `LocalizedText`).
**Regenerar:** solo si usaste `"@clave"` en un builder.
**Te cuidan** (`LocalizationTests`): `TheTable_HasASpanishAndAnEnglishTextForEveryKey_WithTheSamePlaceholders`, `EveryKeyUsedInTheCode_ExistsInTheTable`, `TheGameCode_HasNoHardCodedPlayerFacingText` (texto escrito a mano en UI, Environment, Player, Data o builders).
**Ojo:** el inglés lo redacta Claude, hay que revisarlo antes de publicar. Hay pruebas de PlayMode que buscan texto en pantalla por un fragmento: un cartel nuevo que lo contenga las rompe (pasó con "Turbo"; ver `HANDOFF_HITO_2.md`).

## 2. Logro — Probada (2026-10-29, logro de prueba `ach_harvest_4`, ya revertido)

**Tocás:**
1. `Phase11SceneBuilder.Achievements`: un `AchievementSpec` (`File`, `Id`, `Kind`, `Target`, `Minutes`, `Fertilizer`) **al final** del arreglo. Dentro de su tipo la meta tiene que crecer.
2. `Localization.csv`: `data.<id>,<texto con {0}>,<texto con {0}>` (la meta reemplaza al `{0}`).
3. Tres números a mano en las pruebas, porque comprueban la cantidad exacta (hoy 18): `AchievementTests.TheConfig_HasEighteenAchievements...` (EditMode), y en `AchievementIntegrationTests` (PlayMode) el `achievements.Count` **y** el `rowsShown` (más el mensaje "Con 18 logros..."). El roadmap pide 15 a 20 logros.

**Regenerar:** `BuildAll` (arma el asset, lo engancha al `GameConfig` y la tarjeta del HUD).
**Te cuidan:** `AchievementTests` (ids únicos, metas crecientes por tipo, un logro de cada tipo), `LocalizationTests.EveryGameDataAsset_HasItsNameInTheTable`, `AchievementIntegrationTests`.
**Cómo se probó:** con el logro agregado, `BuildAll` (28 s) + EditMode 439 en verde; el PlayMode de logros **falló** por el `18` de `rowsShown`, que no estaba en mi lista: se corrigió y pasó 2/2. Después se revirtió todo (asset, fila del CSV, `GameConfig`, números) y se regeneró: EditMode 439 en verde.
**Ojo:** un **tipo nuevo** (`AchievementKind`) es otra receta: va al final del enum, tiene que escucharlo `AchievementManager` y la prueba exige al menos un logro de cada tipo. Los premios son minutos de producción con `missionRewardScale`.

## 3. Misión diaria — Leída

**Tocás:** `Phase10SceneBuilder.Missions` (`MissionSpec`: `File`, `Id`, `Kind`, `Target`, `MinLevel`, `Fertilizer`, `Minutes`) y la fila `data.<id>` del CSV (con `{0}` por la meta).
**Regenerar:** `BuildAll`.
**Te cuidan** (`MissionTests`): el `GameConfig` tiene que listar **todas** las plantillas del proyecto, con ids únicos y al menos 6; el sorteo elige una de cada tipo por día, así que con nivel 1 tienen que existir misiones de cosechar, vender y comprar de `MinLevel` 1; `MissionIntegrationTests` en PlayMode.
**Ojo:** no hay conteo duro, pero sí la regla de "una de cada tipo": si agregás un tipo nuevo (`MissionKind`) hace falta que `MissionManager` lo escuche y, con nivel 1, que haya 3 tipos posibles.

## 4. Cultivo nuevo — Leída (modelo: F-01, girasol)

**Tocás:**
1. `Phase2SceneBuilder.CropSpecs`: `CropSpec(file, id, nombre, segundos de crecimiento, precio)` al final. Tiene que ser más lento, más caro y más rentable por segundo que el anterior.
2. `Phase3SceneBuilder`: malla de la planta (en crecimiento y madura) y su entrada en la lista de tuplas `("Sunflower", BuildSunflower)`.
3. `Phase4SceneBuilder`: malla del producto apilado y su entrada en la lista.
4. `Localization.csv`: `data.<id>`.
5. Si necesita un color nuevo: una celda en `Palette.cs` (Editor). **Sigue siendo UN material de paleta**: no agregues materiales (la regla de arte se discute antes, ROADMAP A-05).
6. Para que se juegue: ponelo en las `Rows` de una zona (receta 5). Los nombres de fila son los de los builders (`Carrot`, `Sunflower`...).

**Regenerar:** `BuildAll` (`Phase7SceneBuilder.CreateAssets` lo agrega al `GameConfig`). Después de cambiar sus números: `Aplicar balance de cultivos y mejoras`.
**Te cuidan:** `GameDataTests` (cantidad de cultivos —hoy 8— y que cada uno sea más lento, caro y rentable que el anterior: **actualizar el 8**), `PerformanceBudgetTests` (un material), `LocalizationTests`, y los tests de pools del PlayMode.
**Ojo:** cada cultivo prewarmea su pool de productos (+144 objetos inactivos) aunque no esté en ninguna parcela. Meterlo en una zona cambia la economía: recalibrá con el bot (receta 5).

## 5. Zona nueva — Leída (modelo: F-02)

**Tocás:**
1. `ProgressionData.Zones`: `ZoneSpec` (`File`, `Id`, `Name`, `Cost`, `Center`, `Gate`, `Rows` con 3 cultivos). El portón tiene que quedar **dentro de una zona ya abierta**, así siempre se llega pasando por una abierta.
2. `Localization.csv`: `data.<id>`.
3. Bot de balance: `BalanceBot.cs` (`FieldSpec`: `ZoneId`, `RequiresZoneId`, `Center`, `Gate`, `Rows` con ids en minúscula) y el plan de compras (`EconomyPlan`).
4. `ProgressionIntegrationTests`: la cantidad de zonas (hoy 5) y la lista de cultivos por zona.
5. Documentar los costos en `ECONOMIA_V1.md`.

**Regenerar:** `BuildAll`; el costo en una zona existente va por `Aplicar balance de cultivos y mejoras`.
**Te cuidan:** `ProgressionDataTests` (límites de costo y de salto entre niveles), los tests del bot (`ThePurchases_FollowThePlayerSchedule...`, `TheBot_ReachesLevelTwenty_NearHourThree`) —fallan hasta recalibrar—, `ProgressionIntegrationTests`.
**Ojo:** cuenta sola para "todo comprado" del prestigio y se cierra con cada temporada. Sin verificar a ojo: el encuadre de cámara de una zona nueva. Los costos de una zona nueva nacen PROVISORIOS hasta calibrar (B-05).

## 6. Mejora nueva, o un nivel más de una existente — Leída (modelo: B-01 Almacén, F-04)

**Un nivel más de una mejora:** agregá `(costo, valor)` a `Levels` en `ProgressionData.Upgrades` y corré `Aplicar balance de cultivos y mejoras`. Límites que exige `ProgressionDataTests`: costo hasta 500.000, salto entre niveles hasta ×10, y Crecimiento/Precio hasta ×3. Velocidad y Mochila ya están en sus topes de diseño.
**Una mejora nueva — tocás:**
1. `UpgradeKind` (en `UpgradeData.cs`, al final) y el efecto en `ProgressionModifiers` más quien lo consume.
2. `ProgressionData.Upgrades`: `UpgradeSpec` (`File`, `Id`, `Name`, `Short`, `Kind`, `Levels`, `Accent`, `X`, `Row`). El pad se ubica con `PlazaColumnPitch` / `PlazaRowPitch` (renglón 0 = fertilizante y Turbo, 1 y 2 = mejoras); no muevas los otros.
3. Un dibujo propio en `BuildUpgradePad` y su `"SM_UpgradePad_<Kind>"` en `PadShapeTests.PadMeshes` (los pads no pueden depender del color).
4. `Localization.csv`: `data.<id>` y, si el pad lleva rótulo, su `pad.*`.
5. Bot: costos y plan de compras (`BalanceBot`, `EconomyPlan`, `ECONOMIA_V1.md`).

**Regenerar:** `BuildAll`.
**Te cuidan:** `PadShapeTests`, `ProgressionDataTests`, `ProgressionIntegrationTests` (cantidad de mejoras, hoy 5), tests del bot.
**Ojo:** entra sola en "todo comprado" y se reinicia con cada temporada. Si la plaza se agranda, `PlazaBuilder.PlazaHalfWidth` manda el ancho de la valla.

## 7. Recompensa de video con pad fijo — Leída (modelo: D-14, pad de Turbo)

**Tocás:**
1. Si el efecto ya existe: nada de código nuevo. Si es nuevo: un `RewardKind` (en `RewardData.cs`, al final) y quien lo aplique (seguí el recorrido de `RewardKind.MoveSpeedBuff` con Grep).
2. `Phase8SceneBuilder.Rewards`: `RewardSpec` (`File`, `Id`, `Name`, `Short`, `Kind`, `Magnitude`, `Duration`, `Cooldown`).
3. El ícono: un valor en el enum `PadIcon` y su malla en `BuildMesh` (`SM_Reward_<Icono>`); agregalo a `PadShapeTests.PadMeshes`.
4. El pad: en `Phase8SceneBuilder.BuildWorldPieces`, `BuildDwellPad<RewardPad>(...)` en la fila de la plaza (`RowZ`, columnas de `RowX`) y `EditorUi.Wire(pad, ("reward", datos))`. Con una columna más, se re-centra la fila y se ensancha la valla (`PlazaHalfWidth`).
5. `Localization.csv`: `data.<id>`; `REWARDED_REGLAS.md` (una prueba ata la tabla a los datos).

**Regenerar:** `BuildAll`; los números nuevos en una recompensa existente, `Aplicar balance de recompensas`.
**Te cuidan:** `ArchitectureTests` (solo `AdsManager` pide mostrar un anuncio y solo un gesto del jugador pide un rewarded: el pad **no** llama al servicio de anuncios, pasa por `AdsManager.TryRequest`), `PadShapeTests`, `RewardsIntegrationTests`.
**Ojo:** en esa clase los pads de video se distinguen por tipo de recompensa (`VideoPadOf(RewardKind...)`), no por clase: hay dos `RewardPad` exactos. En la web no puede haber un botón de video flotante con el juego activo (D-04).

## 8. Algo que se guarda — Leída

**Tocás:**
1. Una clase en `Managers` que implemente `ISaveable` (`SaveId`, `Capture`, `Restore`, `SaveRequested`). `SaveId` único; la sección lleva **su propia versión**.
2. `Bootstrapper.CreateServices`: `save.Register(x)` **al final** de los registros (el orden es el de restauración: lo que depende de otro va después).
3. La prueba de arranque `RewardsIntegrationTests.TheGame_BootsWithTheNewServices_InOrder...`: `SaveManager.Instance.SaveableCount` (hoy 15) y, si es un servicio nuevo, la lista de nombres.
4. Tu prueba de EditMode: capturar y restaurar, y **partida vieja sin la sección = valores en cero** (tomá `DailyRewardTests` o `AchievementTests` de modelo).

**Regenerar:** no (el Bootstrapper arma los servicios en tiempo de ejecución).
**Te cuidan:** la prueba de arranque, y las de guardado de la partida vieja.
**Ojo:** no subas `SaveData.CurrentVersion` ni toques `SaveMigrator` por una sección nueva: eso es solo si cambia el sobre del archivo. Una sección desconocida se conserva tal cual al reescribir.

## 9. Servicio nuevo — Leída

**Tocás:** `Bootstrapper.CreateServices` en su lugar del orden (hoy 25; `AnalyticsManager` y `PortalManager` van últimos porque escuchan a los demás) y la lista de nombres de `TheGame_BootsWithTheNewServices_InOrder...`.
**Reglas:** el servicio va en `Managers` (o detrás de una interfaz de `Core` si es un SDK o una plataforma: nunca el SDK suelto en el juego); UI y gameplay no se conocen, se hablan por eventos.
**Te cuidan:** `ArchitectureTests` (grafo de capas), la prueba de arranque.
**Ojo:** si un servicio nace después del guardado y necesita enganchar a otro (como `AdsManager` con los logros), se engancha con un `Bind...()` posterior.
