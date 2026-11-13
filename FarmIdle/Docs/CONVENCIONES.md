# Convenciones de código

Roadmap AR-06. Lo que ya hace el código, escrito una vez. Las reglas de capas y de proceso están en `CLAUDE.md` y `ARQUITECTURA.md`; esto es el estilo del día a día.

## Nombres y archivos
- **Identificadores en inglés, comentarios y textos en español.** Los comentarios explican el porqué y, si viene de una tarea, citan su ID del roadmap (`// D-14`, `// U-04`).
- Una clase por archivo, con el nombre de la clase; `PascalCase`; interfaces con `I`.
- Sufijos que ya se usan: `...Manager` (servicio con estado), `...Rules` / `...Math` / `...Tracker` / `...Session` (lógica pura), `...Data` (ScriptableObject), `...View` (UI), `...Pad` (pad del mundo).
- Namespaces: `FarmIdle` (juego), `FarmIdle.EditorTools` (Editor), `FarmIdle.Tests` (pruebas).

## Servicios y lógica
- Un servicio es un `MonoBehaviour` que crea `Bootstrapper` en un orden explícito, con `Instance` y un `Initialize(...)`. Solo depende de los que están arriba en ese orden.
- La lógica con reglas va en clases puras (sin `UnityEngine`) y el servicio solo la conecta: así se prueba en EditMode. Una prueba de arquitectura lo exige.
- Lo externo (anuncios, analítica, vibración, almacenamiento) va detrás de una interfaz con su implementación por plataforma; el SDK nunca se usa suelto.
- Comunicación entre capas: eventos `public event Action<...>` de los managers y lecturas de solo lectura. Environment, Player y UI no se llaman entre sí.

## Datos y textos
- Los datos son ScriptableObjects de solo lectura; el estado vive en los managers y se guarda con `ISaveable`.
- Campos serializados: `[SerializeField] private` con `[Tooltip]` si no es obvio.
- Todo texto visible: clave `grupo.nombre` en `Assets/Resources/Localization.csv` (clave, es, en) y `Loc.Get("clave")`; en los builders, `"@clave"`. Nada de textos escritos a mano (lo comprueba una prueba; `DebugPanel` está exento).

## Rendimiento
- Sin asignaciones por cuadro: reusar `StringBuilder` y listas, y refrescar un texto solo cuando cambia su estado (patrón `StateKey()` de los pads).
- Sin `Instantiate` en juego: pools (`PoolManager`). Objetos repetidos con UN material de paleta.

## Escenas y builders
- Las escenas no se editan a mano: se cambia el builder y se regenera con `Phase2SceneBuilder.BuildAll`.
- Los builders **no pisan** datos que ya existen; un número nuevo para un asset viejo va por un `ApplyBalance`.
- Cada asset nuevo de terceros se anota en `THIRD_PARTY.md` con su licencia antes de usarse.

## Pruebas
- Nombre: `Cosa_Condicion_Resultado` en inglés con guiones bajos. EditMode para lógica pura y datos; PlayMode para integración (con `GameBoot`).
- Sin `try/finally` en los iteradores de `[UnityTest]` (cuelga Unity); sin bucles infinitos; sin `ScreenCapture` ni `WaitForEndOfFrame` en modo batch.
- Una tarea no se cierra si baja la línea base (hoy EditMode 439 y PlayMode 245; el número vigente está en `CLAUDE.md`).

## Proceso
- Editar C# con la herramienta Edit (conserva CRLF). No editar scripts mientras corre un build o una suite.
- Sin commit ni push salvo pedido explícito. Sin secretos en el repositorio (keystore, IDs reales de AdMob, claves).
