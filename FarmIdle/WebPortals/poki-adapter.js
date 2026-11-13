/*
 * Adaptador de FarmIdle para Poki (Poki SDK, rewardedBreak). Define window.FarmIdleAdAdapter. NO trae el SDK: lo carga el index.html
 * que sube a Poki (<script src="https://game-cdn.poki.com/scripts/v2/poki-sdk.js"></script>, ANTES del loader de Unity). Sin el SDK dice
 * 'nofill' y el juego muestra "Sin anuncio": nunca se regala nada.
 *
 * Poki pide aclarar al jugador, antes, que va a ver un video a cambio de algo (los pads de FarmIdle ya dicen "Ver video" y qué da) y que
 * una recompensa no exija varios videos seguidos (acá es uno por recompensa). Ver WebPortals/README.md.
 */
(function () {
  var ready = false;
  var busy = false;

  function sdk() {
    return window.PokiSDK || null;
  }

  var instance = sdk();
  if (instance && typeof instance.init === 'function') {
    instance.init().then(function () { ready = true; }).catch(function () { ready = false; });
  }

  window.FarmIdleAdAdapter = {
    isReady: function (placement) {
      return ready && !busy && !!sdk();
    },

    showRewarded: function (placement, done) {
      var s = sdk();
      if (!s || !ready || busy) {
        done('nofill');
        return;
      }

      busy = true;
      var finish = function (result) {
        if (!busy) return;
        busy = false;
        done(result);
      };

      try {
        // rewardedBreak devuelve una promesa con 'true' si el jugador vio el video y se gana la recompensa.
        s.rewardedBreak().then(function (withReward) {
          finish(withReward ? 'rewarded' : 'closed');
        }).catch(function () {
          finish('failed');
        });
      } catch (e) {
        finish('failed');
      }
    }
  };
})();
