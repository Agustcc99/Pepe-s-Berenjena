# Arquitectura de Farm Idle (mapa de 5 minutos)

Roadmap AR-01. Releído contra el código el 2026-10-29. Si algo de acá no coincide con el código, manda el código: corregí este archivo en la misma tarea.

## 1. Capas (una `.asmdef` por capa)

```
Core  Data            <- no referencian a nadie
   \  /
  Managers            <- Core, Data
  /   |   \
Environment  Player  UI   <- Core, Data, Managers. NO se ven entre sí.
         \    |    /
          Editor          <- todo (solo Editor: builders de escenas, release)
Tests: EditMode (38 archivos) y PlayMode (43)
```

| Capa | Qué vive ahí |
|---|---|
| **Core** | Tipos e interfaces compartidos: `GameState`, `PlotState`, `IActor`, `IInteractable`, `IProductionSource`, `IFertilizable`, `ITutorialTarget`, `SceneNames`. |
| **Data** | ScriptableObjects de solo lectura (`GameConfig`, `CropData`, `ZoneData`, `UpgradeData`, `PerkData`, `RewardData`, `MissionData`, `AchievementData`, `AudioBank`, `AdMobConfig`) y `Loc` (texto localizado). |
| **Managers** | El estado y las reglas: servicios (`EconomyManager`, `ProgressionManager`, `AdsManager`...), guardado, anuncios, analítica. Es la capa más grande (72 archivos), en subcarpetas por dominio (sin cambiar namespace): `Economy/` (billetera, mochila, precios), `Progress/` (nivel, prestigio, misiones, logros, login diario, tutorial, offline), `Rewards/` (anuncios, ofertas, Turbo, fertilizante, consentimiento, portal), `Save/` (guardado y almacenes), `Audio/` (sonido, vibración, ajustes) y, sueltos en la raíz, arranque, pool, censo de parcelas y analítica. Las pruebas buscan los archivos por nombre, no por carpeta. |
| **Environment** | Lo que está en el mundo: parcelas, pads, zonas, camión de venta. |
| **Player** | El jugador: movimiento, entrada, mochila visible, efectos del Turbo. |
| **UI** | HUD y paneles (ajustes, misiones, logros, premio diario, offline, depuración). |
| **Editor** | Builders que generan las escenas, los datos y los assets; `ReleaseBuilder`; configuración de AdMob y del proyecto. |

## 2. Cómo se hablan las capas

- El estado vive en los **Managers**; Environment, Player y UI no se ven entre sí y se comunican **por eventos C#** (`public event`) de los managers y por lecturas de solo lectura (`IReadOnlyWallet`, `IReadOnlyInventory`, `IBackpackOwner`).
- Puntos de encuentro: `PlotCensus` (las parcelas, para gameplay y UI), la billetera de `EconomyManager` y la mochila de `BackpackManager`.
- Estados del juego (`GameManager` define las transiciones): `Boot` → `Ready` (esperando "Tap to Play") → `Playing` ⇄ `Paused` / `AdShowing` (el mundo queda congelado).
- Servicios detrás de interfaces: `IAdService` (Mock, Web, AdMob, No), `IAnalytics` (Null, Console), `IVibrator`, `ISaveStorage` (archivo, memoria, espejado). AdMob solo compila en Android con `FARMIDLE_ADMOB`.
- Web: `WebPortal.cs` es el puente con la jslib; el adaptador JS está en `FarmIdle/WebPortals/crazygames-adapter.js`; `PortalManager` avisa cuándo empieza y se corta el gameplay.

## 3. Arranque (`Bootstrapper.CreateServices`)

La escena **Bootstrap** crea los servicios **en orden explícito** bajo `[Services]` (persistente). Cada servicio solo puede depender de los que están arriba:

`GameManager, PoolManager, EconomyManager, PlotCensus, BackpackManager, ProgressionManager, BuffManager, FertilizerManager, PrestigeManager, OfflineManager, MissionManager, AchievementManager, DailyRewardManager, DecorationManager, HelperManager, SkinManager, OrdersManager, SettingsManager, TutorialManager, SaveManager` (registra las 16 secciones y **carga la partida acá, una sola vez**), `AdsManager, OfferManager, AudioManager, HapticsManager, AnalyticsManager, PortalManager`.

Después carga `MainLevel`, pasa a `Ready` y descarga Bootstrap. El orden y la cantidad de secciones guardables (12) los vigila `RewardsIntegrationTests.TheGame_BootsWithTheNewServices_InOrder_AndTheRewardsAreLinked`.

## 4. Datos y textos

- Los datos son ScriptableObjects en `Assets/ScriptableObjects/{Achievements,Audio,Config,Missions,Perks,Plants,Rewards,Upgrades,Zones}`; `GameConfig` es la raíz (listas y números de balance). Los builders **no pisan** datos existentes: los números nuevos para assets viejos van por un `ApplyBalance`.
- Todo texto visible va en `Assets/Resources/Localization.csv` (clave, es, en) y se usa con `Loc.Get("clave")`. Una prueba falla si queda texto escrito a mano.

## 5. Guardado

- Cada sistema con estado implementa `ISaveable` (`SaveId`, `Capture`, `Restore`, `SaveRequested`) y se registra en `SaveManager` en `Bootstrapper`; **el orden de registro es el de restauración** (el progreso antes que la mochila).
- Archivo: un sobre versionado (`SaveData.CurrentVersion`, hoy 1) con una sección por sistema, cada una con su versión. `SaveMigrator` sube de versión un paso por vez (hoy no hay pasos). `SaveFile`: partida principal + respaldo, escritura segura por temporal, y un archivo dañado o de una versión futura se aparta (`save.corrupt`, `save.future`) en vez de romper.
- Se guarda diferido (con reloj sin escala) y al instante en los eventos importantes. En la web la partida se espeja en `localStorage` y en el módulo Data del SDK de CrazyGames (D-03).

## 6. Escenas, builders y release

- Solo hay dos escenas, **Bootstrap** y **MainLevel**, y se generan con `Phase2SceneBuilder.BuildAll` (que llama a las demás piezas `PhaseNSceneBuilder`). **No se editan a mano**: se cambia el builder y se regenera.
- Los builders se llaman por fase (`Phase1` a `Phase12`, menos la 6) porque así nacieron; es deuda conocida y AR-05 los reordena por responsabilidad, una tanda por vez. **Ya reordenado (tanda 1):** la Fase 6 son `ProgressionData` (tablas y datos de zonas y mejoras, `ApplyBalance`), `PlazaBuilder` (medidas, sectores, vallas y pads de mejora), `ZoneBuilder` (terreno cerrado, portón y campo de cada zona) y `PadBuilder` (lo común de todos los pads: medidas, barra de pago, objeto y cableado). Cada uno tiene `CreateAssets` (lo llama `BuildAll`) y, si toca la escena, `Build`.
- Release: `ReleaseBuilder` (menú FarmIdle > Release: APK + AAB, Web, y la variante `Web-CrazyGames` que arma `PortalPackager`).

## 7. Qué vigila cada regla

| Regla | Quién la vigila |
|---|---|
| Cada capa solo referencia a las de abajo; Player, UI y Environment no se referencian | `ArchitectureTests` (y el compilador, por las `.asmdef`) |
| La lógica pura no usa `UnityEngine` | `ArchitectureTests.PureLogicClasses_DoNotUseUnityEngine` |
| Solo `AdsManager` pide mostrar un anuncio; solo los gestos del jugador piden un rewarded (pad, tarjeta de regreso) | `ArchitectureTests` |
| Cada pad tiene su propia forma (escala de grises) | `PadShapeTests` |
| Sin asignaciones por cuadro, un material de paleta, pocos draw calls | `PerformanceBudgetTests` y afines |
| Sin texto escrito a mano | `LocalizationTests` |

## 8. Dónde va un cambio

| Quiero... | Toco |
|---|---|
| Cambiar un número de balance, un cultivo, una zona, una mejora | el ScriptableObject (y el builder de datos si es nuevo) + las pruebas de datos y balance |
| Una regla nueva del juego | un Manager o una clase pura `...Rules` + prueba EditMode |
| Algo nuevo en el mundo (pad, parcela, zona) | `Environment`/`Player` + su builder + `BuildAll` |
| Una pantalla o un botón | `UI` + el builder de la UI |
| Un texto | `Localization.csv` |
| Un servicio nuevo | `Bootstrapper.CreateServices` (en su lugar del orden) + la prueba de orden |
| Algo que se guarde | `ISaveable` + `save.Register(...)` + la cuenta de secciones de la prueba |
| Un SDK o una plataforma | una interfaz en `Managers` y una implementación por plataforma (nunca el SDK suelto en el juego) |

Las recetas paso a paso de estos cambios están en `RECETAS.md` (AR-02). Las decisiones tomadas y su porqué están en `DECISIONES.md`; las convenciones de código, en `CONVENCIONES.md`.
