/*
 * Adaptador de FarmIdle para CrazyGames (SDK JS v3, leído en docs.crazygames.com el 2026-10-28). Define dos cosas que usa el juego
 * (Assets/Plugins/WebGL/FarmIdleWeb.jslib):
 *   - window.FarmIdleAdAdapter: el video con recompensa.
 *   - window.FarmIdlePortal:    gameplay(playing) -> game.gameplayStart() / gameplayStop(), locale() -> user.systemInfo.locale,
 *                               dataGet/dataSet/dataRemove -> módulo Data (guardado) y whenReady() -> promesa de "el SDK ya inició".
 * NO trae el SDK: lo carga el index.html (<script src="https://sdk.crazygames.com/crazygames-sdk-v3.js"></script> en el <head>, ANTES de este
 * archivo). WebPortals/ + FarmIdle > Release > Pack CrazyGames arma esa página sola. Sin el SDK, o en un dominio donde el SDK está
 * 'disabled' (cualquiera que no sea crazygames.com, localhost o ?useLocalSdk=true), este adaptador dice 'nofill' y el juego muestra
 * "Sin anuncio": nunca se regala nada.
 *
 * Contrato del anuncio (ver el comentario de FarmIdleWeb.jslib): showRewarded llama a done() UNA vez con 'rewarded' (vio el video completo),
 * 'closed', 'failed' o 'nofill'. Mientras tanto el juego se pausa solo (estado AdShowing) y calla el audio. Los errores del SDK
 * (adsDisabled en Basic Launch, unfilled, adblock, adCooldown, other) antes de empezar son 'nofill'; uno a mitad del anuncio es 'failed'.
 */
(function () {
  var ready = false;
  var busy = false;
  var wantPlaying = false;   // lo último que pidió el juego; se aplica cuando el SDK termina de iniciar
  var sentPlaying = false;   // lo último que se le avisó al SDK
  var locale = null;

  // Se resuelve cuando el SDK terminó de iniciar o falló, o si no hay SDK (nunca se rechaza): la plantilla espera esto, con tope, antes de
  // arrancar el juego, para que la primera lectura de la partida ya encuentre el módulo Data (la nube de un jugador logueado).
  var settle = null;
  var readyPromise = new Promise(function (resolve) { settle = resolve; });

  function sdk() {
    return window.CrazyGames && window.CrazyGames.SDK ? window.CrazyGames.SDK : null;
  }

  // En 'disabled' las llamadas al SDK tiran error: ahí no se usa.
  function usable(instance) {
    return !!instance && instance.environment !== 'disabled';
  }

  function syncGameplay() {
    var s = sdk();
    if (!ready || !usable(s) || !s.game || wantPlaying === sentPlaying) return;
    try {
      if (wantPlaying) s.game.gameplayStart();
      else s.game.gameplayStop();
      sentPlaying = wantPlaying;
    } catch (e) {
      // Una señal perdida no rompe el juego; se reintenta con la próxima.
    }
  }

  // El SDK de CrazyGames v3 se inicializa a mano. Al iniciar agrega elementos al <body>, que desde el <head> todavía no existe
  // (probado con el SDK real: "Cannot read properties of null (reading 'appendChild')"), así que se espera al DOM.
  function startInit() {
    var instance = sdk();
    if (!instance || typeof instance.init !== 'function') {
      settle();
      return;
    }

    instance.init().then(function () {
      if (!usable(instance)) {
        settle();
        return;
      }
      ready = true;
      try {
        var info = instance.user && instance.user.systemInfo;
        locale = info && typeof info.locale === 'string' && info.locale ? info.locale : null;
      } catch (e) {
        locale = null;
      }
      syncGameplay();
      settle();
    }).catch(function () { ready = false; settle(); });
  }

  var doc = window.document;
  if (doc && doc.readyState === 'loading' && typeof doc.addEventListener === 'function') {
    doc.addEventListener('DOMContentLoaded', startInit);
  } else {
    startInit();
  }

  // Guardado en el módulo Data del SDK (misma API que localStorage; según la doc los invitados quedan en localStorage y los logueados en la
  // nube, 1 MB por usuario). El juego espeja ahí la partida principal (MirroredSaveStorage, vía FI_Local* de la jslib). Si el módulo no se
  // puede usar (SDK sin iniciar, entorno 'disabled', "Progress Save" sin marcar en el envío, error del SDK) devuelve false/null y la jslib
  // sigue con localStorage, así que nunca se pierde un guardado por esto.
  function dataModule() {
    var s = sdk();
    if (!ready || !usable(s) || !s.data) return null;
    return typeof s.data.getItem === 'function' && typeof s.data.setItem === 'function' ? s.data : null;
  }

  window.FarmIdlePortal = {
    gameplay: function (playing) {
      wantPlaying = !!playing;
      syncGameplay();
    },

    locale: function () {
      return locale;
    },

    whenReady: function () {
      return readyPromise;
    },

    // 1 si el módulo Data guardó el valor, 0 si no se pudo (el llamador usa localStorage).
    dataSet: function (key, value) {
      var data = dataModule();
      if (!data) return 0;
      try {
        data.setItem(key, value);
        return 1;
      } catch (e) {
        return 0;
      }
    },

    // El texto guardado, o null si no hay o el módulo no se puede usar.
    dataGet: function (key) {
      var data = dataModule();
      if (!data) return null;
      try {
        var value = data.getItem(key);
        return typeof value === 'string' ? value : null;
      } catch (e) {
        return null;
      }
    },

    dataRemove: function (key) {
      var data = dataModule();
      if (!data || typeof data.removeItem !== 'function') return;
      try {
        data.removeItem(key);
      } catch (e) {
        // sin módulo Data no hay nada que borrar ahí
      }
    }
  };

  window.FarmIdleAdAdapter = {
    isReady: function (placement) {
      return ready && !busy && usable(sdk());
    },

    showRewarded: function (placement, done) {
      var s = sdk();
      if (!ready || !usable(s) || busy) {
        done('nofill');
        return;
      }

      busy = true;
      var started = false;
      var finish = function (result) {
        if (!busy) return;
        busy = false;
        done(result);
      };

      try {
        s.ad.requestAd('rewarded', {
          adStarted: function () { started = true; },
          adFinished: function () { finish('rewarded'); },
          // Un error antes de empezar es "no hay anuncio" (el jugador no pierde nada); uno a la mitad es un fallo.
          adError: function (error) { finish(started ? 'failed' : 'nofill'); }
        });
      } catch (e) {
        finish('failed');
      }
    }
  };
})();
