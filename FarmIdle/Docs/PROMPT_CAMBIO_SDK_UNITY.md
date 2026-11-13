# Prompt: pasar a Unity 6 + paquete oficial de CrazyGames (backup primero)

Pegar el bloque de abajo en una sesión nueva de Claude Code abierta en `C:\Users\Agus Tacconi\Downloads\aa`. Creado el 2026-10-29.

```
Leé primero `CLAUDE.md`, la entrada "Motor en el formulario de CrazyGames: HTML5, no Unity 6" de `FarmIdle/Docs/DECISIONES.md` y las secciones 8 y 9 de `FarmIdle/Docs/CRAZYGAMES_REQUISITOS.md`. Después seguí este plan en orden y frená donde te digo. Respondé en español rioplatense, corto, sin relleno.

CONTEXTO
- Juego Unity 6000.6.6f1 (URP) en `C:\Users\Agus Tacconi\Downloads\aa\FarmIdle`. Línea base: EditMode 441 (+1 explícita) y PlayMode 246 en verde. Nada está commiteado.
- Hoy el build web (`Builds/Release/Web-CrazyGames`) habla con CrazyGames por un adaptador propio en JavaScript: `WebPortals/crazygames-adapter.js` + `Assets/Plugins/WebGL/FarmIdleWeb.jslib` + `Managers/Rewards/PortalManager.cs`, `WebPortal.cs`, `GameplaySignals`, `WebAdService.cs`. `ReleaseBuilder.BuildWeb` y `PortalPackager` arman la carpeta.
- En el portal, con el motor "Unity 6" el portal arma su propia página (`.../unity/unity2020/...`) y NO carga nuestro `index.html` ni el adaptador: el QA decía "SDK not currently detected". Con el motor "HTML5" sí corre, pero el QA exige que ande en iOS y Android y no hay iPhone para probar (Unity en iOS está apagado por defecto en CrazyGames). Por eso quiero pasar a Unity 6 con el paquete oficial.
- Ya está hecho y no se rehace: web en inglés por defecto (`Loc.AutoIsEnglishOnly`) con botón de idioma en el menú, pads de video apagados en la web durante el Basic Launch (`AdPolicy`, `FARMIDLE_WEB_ADS=on` para Full Launch), build sin respaldo de descompresión (`.br`), plaza por sectores, título "Pepe's Berenjena".
- Tengo cuenta de CrazyGames y el preview del Developer Portal. NO se envió nada a revisión y no hay que enviarlo.

PERMISOS Y LÍMITES
- Doy el OK para agregar el paquete oficial de CrazyGames (https://sdk.crazygames.com/UnityCrazySDK.unitypackage). Ningún otro SDK ni plugin sin preguntarme.
- Nada de commit, push, secretos, envío a revisión ni aceptar términos. No toques la copia de seguridad una vez hecha.
- Las escenas se generan con `Phase2SceneBuilder.BuildAll`: no se editan a mano. Todo texto nuevo va en `Localization.csv`. Arquitectura por capas con asmdef; servicios detrás de interfaces.
- Cada archivo nuevo de terceros se registra en `FarmIdle/THIRD_PARTY.md` con su licencia.

PASO 1 — BACKUP (hacelo antes de cualquier cambio y avisame cuando esté)
- Copiá `FarmIdle` a una carpeta hermana `C:\Users\Agus Tacconi\Downloads\aa_backup_2026-10-29\FarmIdle` con robocopy, sin `Library`, `Temp`, `Logs`, `obj` ni `Builds` (esa se regenera). Incluí `Assets`, `Packages`, `ProjectSettings`, `Docs`, `WebPortals`, `THIRD_PARTY.md` y los `.md` de la raíz.
- Verificá la copia: contá archivos y tamaño de origen y copia, y comprobá que `ProjectSettings/ProjectVersion.txt` existe. Mostrame las cifras.
- Aparte, copiá también `ROADMAP.md` y `CLAUDE.md`.

PASO 2 — PRUEBA EN UNA COPIA (no en el proyecto real)
- Hacé otra copia de trabajo `C:\Users\Agus Tacconi\Downloads\aa_spike\FarmIdle` (desde la del paso 1). Solo una instancia de Unity puede abrir un proyecto a la vez.
- Descargá el paquete en una carpeta nueva y vacía (no ejecutes nada de ahí), leé https://sdk.crazygames.com/UnityChangelog.md, y las páginas https://docs.crazygames.com/sdk/video-ads/, /sdk/game/ y /sdk/data/ en su versión Unity, y https://docs.crazygames.com/resources/unity-custom-build/ y /resources/issues/.
- Antes de importar, borrá `CrazySDK` y `CrazyOptimizer` si existieran. Importá el paquete en la copia, compilá con 6000.6.6f1 y anotá: ¿compila?, ¿qué archivos agregó?, ¿cuánto pesa (en Assets y en el build web)?, ¿qué plantilla WebGL usa o pide?
- Armá una build web con su menú "CrazySDK" (o el equivalente por código) y medí el peso de la carpeta de salida.
- Mostrame un resumen corto con esas cifras y mi decisión pendiente. FRENÁ ACÁ y esperá mi "seguí" antes de tocar el proyecto real.

PASO 3 — INTEGRAR EN EL PROYECTO REAL (solo si el paso 2 salió bien y dije "seguí")
- Importá el paquete en `FarmIdle` real y reemplazá nuestro puente JS por C# detrás de interfaces: iniciar el SDK (`CrazySDK.Init`), inicio y fin de gameplay (solo `Playing` cuenta; menú, pausa y anuncio cortan; no cortar por perder el foco), el idioma del portal si lo da y el guardado (la doc dice que el progreso se puede guardar con PlayerPrefs; mantené el espejo actual solo si no choca). Los videos siguen apagados en la web durante el Basic Launch (`AdPolicy`); dejá lista la implementación del video con recompensa para el Full Launch.
- La escena de carga que pide el SDK tiene que salir del builder (`Phase2SceneBuilder`) y ser la primera de la build.
- Ajustá `ReleaseBuilder`/`PortalPackager` para que la salida web sea la que acepta CrazyGames con el motor Unity 6, sin `.unityweb`. Conservá lo que siga sirviendo (marco 9:16, foco del teclado) y avisame si hay que renunciar a algo.
- Pruebas: actualizá o agregá las necesarias (tests de las señales de gameplay y del servicio nuevo). La línea base no puede bajar. PlayMode completo con `run_in_background` (~10 min); `TheMonkey_...(3)` es al azar: repetirlo antes de darlo por roto.
- Documentá en `DECISIONES.md` (reemplaza la entrada del motor HTML5), `ARQUITECTURA.md`, `RELEASE.md`, `CRAZYGAMES_REQUISITOS.md` y el "Registro de cambios" de `ROADMAP.md`, y actualizá la línea base de `CLAUDE.md` si cambia.

PASO 4 — VERIFICACIÓN CON EL PORTAL (la hago yo)
- Dejame la carpeta lista y los pasos: en "Submit a game" elegir motor "Unity 6", marcar "Yes, using the Data Module..." o la opción de PlayerPrefs según corresponda, subir la carpeta, abrir el preview, jugar un minuto y mirar en el QA que aparezcan detectadas "Gameplay Start" y la de datos, que la carga sea ≤ 20 s y que el Log no tenga errores. Yo te paso capturas.
- Si algo falla, no inventes: pedime el texto del Log y de la consola.

Al cerrar cada paso: resumen de una línea con cifras y "HECHO / PENDIENTE".
```

## Por qué este orden
1. **Backup**: el paquete oficial modifica `Assets` y `ProjectSettings` y no está commiteado nada, así que sin copia no hay vuelta atrás.
2. **Prueba en una copia**: la doc no dice si el paquete anda con Unity 6000.6.6f1 ni cuánto pesa. Se mide antes de tocar el proyecto real.
3. **Integración**: recién con datos y con un "seguí" tuyo.
