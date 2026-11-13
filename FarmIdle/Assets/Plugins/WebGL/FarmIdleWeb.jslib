// Puente de FarmIdle con la página web (solo se compila en el build de WebGL). El lado C# es Managers/WebPersistence.cs.
mergeInto(LibraryManager.library, {

  // Sube Application.persistentDataPath (un sistema de archivos en memoria, IDBFS) a IndexedDB. Unity trae la
  // sincronización automática APAGADA (autoSyncPersistentDataPath), así que sin esto un archivo guardado no sobrevive a
  // recargar la página. Se usa el mismo pedido agrupado que usa Unity por dentro; si no existiera, el FS.syncfs público.
  FI_SyncFileSystem: function () {
    try {
      if (typeof IDBFS !== 'undefined' && IDBFS.queuePersist && Module.__unityIdbfsMount) {
        IDBFS.queuePersist(Module.__unityIdbfsMount.mount);
      } else if (typeof FS !== 'undefined' && FS.syncfs) {
        FS.syncfs(false, function (err) {
          if (err) console.warn('[FarmIdle] FS.syncfs: ' + err);
        });
      }
    } catch (e) {
      console.warn('[FarmIdle] No se pudo sincronizar IndexedDB: ' + e);
    }
  },

  // Almacén SÍNCRONO clave/valor: la segunda copia de la partida (ver MirroredSaveStorage). Si el navegador lo bloquea (modo privado, lleno)
  // todo devuelve "no pasó nada" y el juego sigue con IndexedDB. Con un portal que define window.FarmIdlePortal.dataSet/dataGet/dataRemove
  // (CrazyGames: módulo Data del SDK, que en los jugadores logueados es la nube) se usa primero ese; si el portal no lo puede usar (sin
  // SDK, entorno 'disabled', error) se cae a localStorage. Al leer, el portal manda y si no tiene nada se mira localStorage.
  FI_LocalSet: function (keyPtr, valuePtr) {
    var key = UTF8ToString(keyPtr);
    var value = UTF8ToString(valuePtr);
    try {
      var portal = window.FarmIdlePortal;
      if (portal && typeof portal.dataSet === 'function' && portal.dataSet(key, value)) return 1;
    } catch (e) {
      // el portal falló: sigue localStorage
    }
    try {
      window.localStorage.setItem(key, value);
      return 1;
    } catch (e) {
      return 0;
    }
  },

  // Devuelve un puntero a un texto UTF-8 nuevo, o 0 si no hay (el juego lo lee una sola vez al arrancar: la memoria no importa).
  FI_LocalGet: function (keyPtr) {
    try {
      var key = UTF8ToString(keyPtr);
      var value = null;
      try {
        var portal = window.FarmIdlePortal;
        if (portal && typeof portal.dataGet === 'function') value = portal.dataGet(key);
      } catch (e) {
        value = null;
      }
      if (typeof value !== 'string') value = window.localStorage.getItem(key);
      if (value === null) return 0;
      var size = lengthBytesUTF8(value) + 1;
      var buffer = _malloc(size);
      stringToUTF8(value, buffer, size);
      return buffer;
    } catch (e) {
      return 0;
    }
  },

  FI_LocalRemove: function (keyPtr) {
    var key = UTF8ToString(keyPtr);
    try {
      var portal = window.FarmIdlePortal;
      if (portal && typeof portal.dataRemove === 'function') portal.dataRemove(key);
    } catch (e) {
      // el portal falló: igual se borra localStorage
    }
    try {
      window.localStorage.removeItem(key);
    } catch (e) {
      // sin localStorage no hay nada que borrar
    }
  },

  // Avisa a un GameObject (por SendMessage) cuando la pestaña se oculta, se cierra o se recarga. En la web no hay
  // OnApplicationQuit confiable: 'visibilitychange' llega primero en celulares y 'pagehide' / 'beforeunload' al cerrar.
  FI_RegisterPageHide: function (gameObjectNamePtr, methodNamePtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);
    if (window.__farmIdlePageHideRegistered) return;
    window.__farmIdlePageHideRegistered = true;

    var notify = function () {
      try {
        SendMessage(gameObjectName, methodName, '');
      } catch (e) {
        // El juego puede estar cerrándose: no hay nada más que hacer.
      }
    };

    document.addEventListener('visibilitychange', function () {
      if (document.visibilityState === 'hidden') notify();
    });
    window.addEventListener('pagehide', notify);
    window.addEventListener('beforeunload', notify);
  },

  // Vibración corta (U-06). Solo en pantallas táctiles: en una PC con mouse no se hace nada. El navegador exige un toque reciente del
  // jugador, que en este juego siempre hay (se maneja con el dedo); si no lo hay o no está soportado, no pasa nada.
  FI_Vibrate: function (milliseconds) {
    try {
      if (navigator.vibrate && window.matchMedia && window.matchMedia('(pointer: coarse)').matches) {
        navigator.vibrate(milliseconds);
      }
    } catch (e) {
      // sin vibración: el juego sigue igual
    }
  },

  // ---------- Portal (D-03) ----------
  // El lado C# es Managers/WebPortal.cs. La página que aloja el juego define window.FarmIdlePortal (lo hace WebPortals/crazygames-adapter.js);
  // sin él no pasa nada y el juego sigue igual:
  //     window.FarmIdlePortal = {
  //       gameplay: function (playing) { ... },   // true = empezó el juego, false = se cortó (gameplayStart / gameplayStop)
  //       locale:   function () { return 'es-AR'; } // idioma que informa el portal, o null si todavía no lo sabe
  //       // opcionales, para el espejo de la partida (FI_Local*): dataSet devuelve 1 si guardó, dataGet el texto o null
  //       dataSet: function (key, value) { ... }, dataGet: function (key) { ... }, dataRemove: function (key) { ... }
  //     };
  FI_PortalGameplay: function (playing) {
    var portal = window.FarmIdlePortal;
    if (portal && typeof portal.gameplay === 'function') {
      try {
        portal.gameplay(playing ? true : false);
      } catch (e) {
        console.warn('[FarmIdle] La señal de gameplay al portal falló: ' + e);
      }
    }
  },

  // Devuelve un puntero a un texto UTF-8 nuevo, o 0 si el portal no informó idioma (se lee una sola vez al arrancar: la memoria no importa).
  FI_PortalLocale: function () {
    try {
      var portal = window.FarmIdlePortal;
      var locale = portal && typeof portal.locale === 'function' ? portal.locale() : null;
      if (!locale || typeof locale !== 'string') return 0;
      var size = lengthBytesUTF8(locale) + 1;
      var buffer = _malloc(size);
      stringToUTF8(locale, buffer, size);
      return buffer;
    } catch (e) {
      return 0;
    }
  },

  // ---------- Anuncios con recompensa (Fase 8) ----------
  // El lado C# es Managers/WebAdService.cs (JsWebAdBridge). Hoy no hay portal elegido: en un build de desarrollo se muestra un anuncio
  // SIMULADO (un overlay HTML) y en uno final no hay anuncio (ver FI_ConfigureSimulatedAds). Para enchufar un portal real NO hace falta
  // tocar C#: los adaptadores listos para CrazyGames y Poki están en WebPortals/ (sin su SDK, que carga el index.html del portal). La
  // página que hospeda el juego define, antes de que arranque Unity,
  //     window.FarmIdleAdAdapter = {
  //       isReady:      function (placement) { return true; },                     // opcional; sin definir se asume listo
  //       showRewarded: function (placement, done) { ...SDK del portal...; done('rewarded'); }
  //     };
  // y llama a done() UNA vez con 'rewarded' (completó el anuncio), 'closed' (lo cerró antes), 'failed' o 'nofill'. El juego
  // pausa todo mientras tanto (estado AdShowing). Cualquier otra cosa que se pase a done() cuenta como 'failed'.
  // Parámetros del simulador: window.FarmIdleSimAdSeconds (duración, 5 por defecto).

  // El anuncio SIMULADO regala la recompensa: solo se permite en builds de desarrollo (lo avisa C# con allowed = 1) o si la página que
  // aloja el juego lo pide a propósito con window.FarmIdleAllowSimAds = true (una demo). Un build final sin adaptador de portal no
  // muestra ningún anuncio ('nofill'), así no se puede conseguir una recompensa sin ver un video de verdad.
  FI_ConfigureSimulatedAds: function (allowed) {
    window.__farmIdleSimAds = allowed ? 1 : 0;
  },

  // 1 si hay un anuncio listo para ese placement.
  FI_AdIsReady: function (placementPtr) {
    var adapter = window.FarmIdleAdAdapter;
    if (adapter && typeof adapter.isReady === 'function') {
      try {
        return adapter.isReady(UTF8ToString(placementPtr)) ? 1 : 0;
      } catch (e) {
        return 0;
      }
    }
    return (window.__farmIdleSimAds || window.FarmIdleAllowSimAds === true) ? 1 : 0;   // el simulador, solo si está permitido
  },

  // Muestra el anuncio y devuelve el resultado por SendMessage(gameObject, método, "<requestId>|<resultado>").
  FI_ShowRewardedAd: function (placementPtr, requestId, gameObjectNamePtr, methodNamePtr) {
    var placement = UTF8ToString(placementPtr);
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);
    var finished = false;

    var done = function (result) {
      if (finished) return;   // el resultado cuenta una sola vez
      finished = true;
      var text = (result === 'rewarded' || result === 'closed' || result === 'nofill') ? result : 'failed';
      try {
        SendMessage(gameObjectName, methodName, requestId + '|' + text);
      } catch (e) {
        console.warn('[FarmIdle] No se pudo devolver el resultado del anuncio: ' + e);
      }
    };

    var adapter = window.FarmIdleAdAdapter;
    if (adapter && typeof adapter.showRewarded === 'function') {
      try {
        adapter.showRewarded(placement, done);
      } catch (e) {
        console.warn('[FarmIdle] El adaptador de anuncios falló: ' + e);
        done('failed');
      }
      return;
    }

    // Sin adaptador y sin permiso para simular: no hay anuncio (nunca se regala nada).
    if (!(window.__farmIdleSimAds || window.FarmIdleAllowSimAds === true)) {
      done('nofill');
      return;
    }

    // ---- Anuncio simulado: overlay propio con cuenta regresiva, "Cerrar" al final y "Saltar" ----
    var seconds = Number(window.FarmIdleSimAdSeconds);
    if (!(seconds >= 0)) seconds = 5;

    var overlay = document.createElement('div');
    overlay.id = 'farmidle-sim-ad';
    overlay.setAttribute('style', 'position:fixed;left:0;top:0;right:0;bottom:0;z-index:99999;background:rgba(8,14,10,0.94);' +
      'color:#fff;font-family:sans-serif;display:flex;flex-direction:column;align-items:center;justify-content:center;text-align:center;');

    var tag = document.createElement('div');
    tag.setAttribute('style', 'font-size:14px;letter-spacing:2px;opacity:0.65;margin-bottom:14px;');
    tag.textContent = 'ANUNCIO SIMULADO';
    overlay.appendChild(tag);

    var counter = document.createElement('div');
    counter.id = 'farmidle-sim-ad-counter';
    counter.setAttribute('style', 'font-size:30px;font-weight:bold;margin-bottom:26px;');
    overlay.appendChild(counter);

    var buttonStyle = 'font-size:22px;padding:12px 30px;margin:8px;border:0;border-radius:10px;cursor:pointer;color:#fff;';
    var close = document.createElement('button');
    close.id = 'farmidle-sim-ad-close';
    close.textContent = 'Cerrar';
    close.setAttribute('style', buttonStyle + 'background:#3fa34d;');
    close.disabled = true;
    overlay.appendChild(close);

    var skip = document.createElement('button');
    skip.id = 'farmidle-sim-ad-skip';
    skip.textContent = 'Saltar (sin recompensa)';
    skip.setAttribute('style', buttonStyle + 'background:#6b6b6b;font-size:16px;');
    overlay.appendChild(skip);

    var timer = null;
    var remaining = seconds;
    var finish = function (result) {
      if (timer) clearInterval(timer);
      if (overlay.parentNode) overlay.parentNode.removeChild(overlay);
      done(result);
    };

    var update = function () {
      if (remaining > 0) {
        counter.textContent = 'La recompensa llega en ' + Math.ceil(remaining) + ' s';
        close.disabled = true;
        close.style.opacity = '0.4';
      } else {
        counter.textContent = 'Recompensa lista!';
        close.disabled = false;
        close.style.opacity = '1';
      }
    };

    close.addEventListener('click', function () { if (remaining <= 0) finish('rewarded'); });
    skip.addEventListener('click', function () { finish('closed'); });

    update();
    timer = setInterval(function () {
      if (remaining > 0) remaining -= 0.25;
      update();
    }, 250);

    document.body.appendChild(overlay);
  }
});
