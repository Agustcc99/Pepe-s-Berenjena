# Farm Idle — reglas del proyecto

Juego hipercasual "arcade idle" de granja, vertical, Android + WebGL. Unity 6000.6.6f1 (URP, Input System, Cinemachine). Proyecto: `FarmIdle/`. Plan de trabajo: `ROADMAP.md` (usar `/roadmap-status` y `/roadmap-next`). Índice de documentos: `FarmIdle/Docs/README.md`. Último traspaso: `FarmIdle/Docs/HANDOFF_HITO_2.md`; mapa del código: `FarmIdle/Docs/ARQUITECTURA.md`; estilo: `CONVENCIONES.md`; decisiones tomadas: `DECISIONES.md` (anotar ahí toda decisión nueva); release: `RELEASE.md`.

## Cómo trabajar (el usuario tiene pocos tokens: ahorrar)
- Responder en español rioplatense, directo, sin relleno. El usuario es dev web (React/Node), primer juego.
- Hacer vos la implementación (no guías manuales), salvo lo de la columna "Yo" del roadmap.
- No explorar el proyecto entero: Grep y lecturas acotadas. No pegar logs largos: reportar resumen (cantidad en verde, fallos en una línea cada uno).
- Cada tarea cierra actualizando solo las filas afectadas de `ROADMAP.md` y su "Registro de cambios" (Edit, no reescribir el archivo).
- Si descubrís algo no obvio que vale la pena para la próxima sesión, anotarlo en un `HANDOFF_*.md` corto o en la memoria; no repetir lo que ya dice el código.

## Arquitectura (no romper)
- Una `.asmdef` por capa: Core, Data, Managers, Environment, Player, UI, Editor. UI y gameplay no se conocen; se comunican por eventos.
- Datos en ScriptableObjects de solo lectura; el estado vive en los managers y se guarda con el sistema de guardado versionado.
- Servicios detrás de interfaces (`IAdService`: Mock/Web/AdMob/No; la analítica futura va igual como `IAnalytics`).
- Escenas Bootstrap + MainLevel generadas por el builder de editor (`Phase2SceneBuilder.BuildAll`): no editar las escenas a mano; cambiar el builder y regenerar.
- Presupuesto de rendimiento: sin asignaciones de memoria por cuadro, sin `Instantiate` en juego (pools), pocos draw calls, objetos repetidos con UN material de paleta (las pruebas lo exigen). Un cambio de arte que altere esto se discute antes (ROADMAP A-05).
- Todo texto visible al jugador debe poder localizarse (ROADMAP U-03): todo texto nuevo va en `Assets/Resources/Localization.csv` (clave, es, en) y se usa con `Loc.Get("clave")` o `"@clave"` en los builders; un test falla si queda texto escrito a mano en UI/Environment/Player/Data o en los builders (DebugPanel exento).

## Pruebas
- Línea base: **EditMode 509 y PlayMode 270 en verde** (`StabilityTests.TheMonkey_...(n)` es al azar: a veces falló en la corrida completa y pasó sola; repetirlo antes de dar la suite por rota). Una tarea no se marca `Hecho` si baja esa cifra o hay fallos. Si la línea base cambia, actualizar este número.
- Para correr Unity en modo batch, builds y pruebas, leer antes la memoria `reference-unity-batch-workflow` (rutas, banderas, trampas). Resumen de las trampas que más duelen:
  - **Nunca usar `cd`** (cambia el directorio primario de la sesión). Rutas absolutas.
  - Solo una instancia de Unity puede abrir el proyecto; no matar un Unity que no inició esta sesión.
  - Sin `try/finally` en iteradores de `[UnityTest]`; sin bucles infinitos en pruebas; sin `ScreenCapture`/`WaitForEndOfFrame` en batch.
  - No editar scripts mientras corre un build o una suite.
  - Editar C# con la herramienta Edit (conserva CRLF); para scripts de Python, escribir el `.py` con Write y correrlo con `python -I`.
  - Una llamada en primer plano tiene tope de 600 s: suites largas con `run_in_background`.

## Límites (del usuario, no de Claude)
- **Nunca** manejar ni escribir secretos: keystore y contraseñas, IDs reales de AdMob, claves de API, pagos. Solo preparar el proyecto para recibirlos (variables de entorno, `AdMobConfig`).
- No publicar ni subir nada a tiendas o portales, no aceptar términos, no hacer commit ni push salvo pedido explícito.
- Plugin de AdMob y cualquier SDK nuevo: pedir OK antes de agregarlo (peso del build).
- Datos de mercado (requisitos de Play/CrazyGames, ingresos): marcarlos "a verificar" con su fuente; no darlos por ciertos.

## Licencias de assets
- Hoy: modelos y audio Kenney (CC0) + loop de música generado. Todo asset nuevo se registra en `THIRD_PARTY.md` (ROADMAP L-01) con su licencia antes de usarse.
