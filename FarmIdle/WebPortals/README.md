# Adaptadores de portales web

El juego pide los videos con recompensa a través de `window.FarmIdleAdAdapter` (ver `Assets/Plugins/WebGL/FarmIdleWeb.jslib`). Si la página no lo define, el build **release** no muestra anuncios (el jslib solo simula videos en desarrollo o si la página pone `window.FarmIdleAllowSimAds = true`). Los adaptadores de esta carpeta NO traen el SDK del portal: lo carga el `index.html` que se sube.

| Archivo | Portal | SDK que tiene que cargar el index.html |
|---|---|---|
| `crazygames-adapter.js` | CrazyGames (recomendado) | `https://sdk.crazygames.com/crazygames-sdk-v3.js` |
| `poki-adapter.js` | Poki | `https://game-cdn.poki.com/scripts/v2/poki-sdk.js` |
| `test_adapters.js` | — | prueba de los dos adaptadores con SDK falso: `node WebPortals/test_adapters.js` |

## Contrato
`showRewarded(placement, done)` llama a `done()` **una sola vez** con `'rewarded'` (vio el video completo), `'closed'`, `'failed'` o `'nofill'`. La recompensa se otorga solo con `'rewarded'`. Mientras dura el anuncio el juego pasa a `AdShowing`: se congela y calla el audio.

## CrazyGames (D-03): la variante ya armada
`FarmIdle > Release > Build Web` deja, además de `Builds/Release/Web`, una copia **`Builds/Release/Web-CrazyGames`** con el SDK y el adaptador ya puestos en el `<head>` del `index.html` (SDK JS v3 desde `sdk.crazygames.com`, no se copia: no suma peso). Si el build ya existe, `FarmIdle > Release > Pack CrazyGames (desde el último Web)` la rearma sin recompilar. Esa carpeta es la que se sube al portal.

El adaptador de CrazyGames define dos cosas para el juego:
- `window.FarmIdleAdAdapter`: el rewarded (ver contrato). En **Basic Launch** el SDK responde `adsDisabled` y el juego muestra "Sin anuncio" (la compra de fertilizante con monedas queda como alternativa).
- `window.FarmIdlePortal`: `gameplay(playing)` → `game.gameplayStart()` / `gameplayStop()` (jugando = start; menú, pausa y anuncio = stop) y `locale()` → `user.systemInfo.locale`, que decide el idioma "Automático".

El SDK solo funciona en `crazygames.com`, en `localhost` / `127.0.0.1` o con `?useLocalSdk=true` en la URL (entorno `local`: los anuncios se reemplazan por un texto). En cualquier otro dominio el entorno es `disabled`, el SDK tira error al usarlo y el adaptador no lo toca.

## Cómo usarlos a mano (otro portal, o armar el index.html vos)
1. Construir el Web de release: `FarmIdle > Release > Build Web` (sale en `Builds/Release/Web`).
2. En `Builds/Release/Web/index.html`, antes del script del loader de Unity, agregar el SDK del portal y después el adaptador:
   ```html
   <script src="https://sdk.crazygames.com/crazygames-sdk-v3.js"></script>
   <script src="crazygames-adapter.js"></script>
   ```
   (copiar el `.js` junto al `index.html`).
3. Subir la carpeta al portal.

## Portales: qué piden (a 2026-10-19; confirmar en la documentación de cada uno antes de subir)
- **CrazyGames**: carga inicial ≤ 50 MB (20 MB para aparecer en el home móvil), rewarded por SDK. Nuestro release Brotli pesa 8,4 MB: sobra. **Es el recomendado.**
- **Poki**: pide < 8 MB iniciales; estamos en 8,4 MB, justo arriba (se puede bajar con texturas más chicas o quitando música). Además pide aclarar al jugador que verá un video a cambio de algo.
- **GameDistribution**: no pude confirmar sus requisitos; no hay adaptador.

## Guardado en la web
El guardado va a IndexedDB (IDBFS) con retraso corto (`webSaveDelaySeconds` = 1 s) y, además, un espejo síncrono en `localStorage` (`MirroredSaveStorage`) para no perder los últimos segundos si se cierra la pestaña de golpe. Se guarda también al ocultar la pestaña.
