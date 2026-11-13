# Traspaso al 2026-10-29 (Hitos 0 a 2 cerrados salvo DEC-2; Hito 3 en curso)

Leer antes: `../../CLAUDE.md`, `ARQUITECTURA.md` y la memoria `reference-unity-batch-workflow`. Nada está commiteado.

## Dónde estamos
- **Línea base:** EditMode 439 (+1 explícita) y PlayMode 245, en verde. Web release 8,5 MB (6 archivos).
- **Hito 0 y Hito 1:** cerrados (Q-03, el playtest externo, quedó `Descartado`).
- **Hito 2:** todo `Hecho` menos **DEC-2** (Yo). Nombre elegido: "Pepe's Berenjenas". Faltan el nombre del estudio, comprobar disponibilidad (CrazyGames, Play, dominio) y buscar en el INPI. Cerrarla destraba K-01, M-04 y D-09. El título visible ya es "Pepe's Berenjena" (singular, a pedido; ver `DECISIONES.md`).
- **Hito 3:**
  - **D-03 y D-04** (`En curso`): la parte de Claude está hecha y verificada con el SDK real en `localhost` (entorno `local`). Falta la prueba del usuario de `FarmIdle/Builds/Release/Web-CrazyGames` en el entorno `crazygames` del portal (la cuenta D-02 ya existe).
  - Abiertas de D-04: resoluciones de la lista de `CRAZYGAMES_REQUISITOS.md`, carga hasta el gameplay (≤ 20 s), Safari y Firefox, y que la revisión del portal acepte dónde está el botón de rewarded.
  - **D-14** (pad fijo de Turbo): `Hecho`. Sin verificar a ojo en la web ni en el portal.
  - Siguen `Pendiente`: Q-10 y D-05 (Yo), K-01 y M-04 (dependen de DEC-2), K-02 (Yo).
- **Hito T (arquitectura):** AR-01, AR-06 y AR-07 hechas con este traspaso; AR-02, AR-03 y AR-04 en los huecos de espera; AR-05 antes del Hito 6. Ojo con AR-03: `ArchitectureTests` ya vigila el grafo de capas y que Player, UI y Environment no se referencien; lo que falta es el tope de líneas por archivo y poco más.

## Trampas nuevas (además de la memoria y `HANDOFF_FASE_9.md`)
- Las pruebas que buscan texto en pantalla por un fragmento pueden confundirse con un cartel nuevo: el pad de Turbo dice "Turbo" y rompió la búsqueda de la fila del HUD (se arregló buscando `"Turbo  "`, con dos espacios, en `TurboHudRow()`).
- `RewardsIntegrationTests` distingue los dos pads de video por tipo de recompensa (`VideoPadOf(RewardKind...)`), no por tipo de clase: hay dos `RewardPad` exactos.
- PlayMode completo tarda unos 600 s: correrlo con `run_in_background`. Para confirmar un arreglo alcanza con `-testFilter "FarmIdle.Tests.<Clase>"` (~1 min).
- `python -m http.server` corta descargas grandes en esta PC: para probar la web usar un servidor estático de `node`. Chrome con perfil reusado cachea el adaptador JS: probar con `?nocache=N`.
- El SDK real de CrazyGames loguea "CrazySDK is not initialized yet" en cada lectura de `.game` antes de `init()`: no envolver el SDK en una prueba, verificar por sus líneas de consola.
- Los scripts temporales del scratchpad (`unity.ps1`, `results.py`) hay que recrearlos en cada sesión (patrón en la memoria).

## Qué sigue
1. **Usuario:** cerrar DEC-2 y probar `Web-CrazyGames` en el portal (D-03/D-04, después Q-10).
2. **Claude, en los huecos:** AR-02 (recetas), AR-03 (pruebas de arquitectura), AR-04 (ordenar `Managers`).
3. **Antes de A-04 (arte):** AR-05.
