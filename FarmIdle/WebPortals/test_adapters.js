// Prueba los adaptadores de portal sin navegador ni SDK real: carga cada adaptador en un "window" falso con un SDK simulado y comprueba que
// done() se llame UNA vez con el resultado correcto. Uso (desde cualquier carpeta): node test_adapters.js
const fs = require('fs');
const path = require('path');
const vm = require('vm');
const assert = require('assert');

function load(file, windowProps) {
  const window = Object.assign({}, windowProps);
  window.window = window;
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, file), 'utf8'), window);
  return window;
}

const tick = () => new Promise((r) => setTimeout(r, 5));

function once() {
  const calls = [];
  return { done: (r) => calls.push(r), calls };
}

async function crazyGames() {
  // Sin SDK: nunca hay anuncio.
  let w = load('crazygames-adapter.js', {});
  assert.strictEqual(w.FarmIdleAdAdapter.isReady('x'), false);
  let c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  assert.deepStrictEqual(c.calls, ['nofill']);

  // Con SDK: ok, error antes de empezar, error a mitad, y un pedido a la vez.
  const make = (behavior) => ({
    CrazyGames: { SDK: { init: () => Promise.resolve(), ad: { requestAd: (type, cb) => { assert.strictEqual(type, 'rewarded'); behavior(cb); } } } }
  });

  w = load('crazygames-adapter.js', make((cb) => { cb.adStarted(); cb.adFinished(); }));
  await tick();
  assert.strictEqual(w.FarmIdleAdAdapter.isReady('x'), true);
  c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  assert.deepStrictEqual(c.calls, ['rewarded']);

  w = load('crazygames-adapter.js', make((cb) => { cb.adError('no hay'); }));
  await tick();
  c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  assert.deepStrictEqual(c.calls, ['nofill'], 'Un error antes de empezar es nofill');

  w = load('crazygames-adapter.js', make((cb) => { cb.adStarted(); cb.adError('se cortó'); cb.adFinished(); }));
  await tick();
  c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  assert.deepStrictEqual(c.calls, ['failed'], 'Un error a mitad es failed y un finished tardío no cuenta');

  let release;
  w = load('crazygames-adapter.js', make((cb) => { release = cb; }));
  await tick();
  c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  assert.strictEqual(w.FarmIdleAdAdapter.isReady('x'), false, 'Con un anuncio en curso no hay otro');
  const second = once();
  w.FarmIdleAdAdapter.showRewarded('x', second.done);
  assert.deepStrictEqual(second.calls, ['nofill']);
  release.adFinished();
  assert.deepStrictEqual(c.calls, ['rewarded']);

  w = load('crazygames-adapter.js', { CrazyGames: { SDK: { init: () => Promise.resolve(), ad: { requestAd: () => { throw new Error('boom'); } } } } });
  await tick();
  c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  assert.deepStrictEqual(c.calls, ['failed'], 'Una excepción del SDK es failed');

  // Entorno 'disabled' (un dominio que no es de CrazyGames): el SDK tira error al usarlo, así que no hay anuncio ni señales ni idioma.
  const events = [];
  const gameApi = { gameplayStart: () => events.push('start'), gameplayStop: () => events.push('stop') };
  w = load('crazygames-adapter.js', { CrazyGames: { SDK: { environment: 'disabled', init: () => Promise.resolve(), game: gameApi, user: { systemInfo: { locale: 'es-AR' } }, ad: { requestAd: () => { throw new Error('disabled'); } } } } });
  await tick();
  assert.strictEqual(w.FarmIdleAdAdapter.isReady('x'), false);
  c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  assert.deepStrictEqual(c.calls, ['nofill'], 'En disabled no se llama al SDK');
  w.FarmIdlePortal.gameplay(true);
  assert.deepStrictEqual(events, [], 'En disabled no se manda gameplayStart');
  assert.strictEqual(w.FarmIdlePortal.locale(), null);

  // Sin SDK el portal existe pero no hace nada.
  w = load('crazygames-adapter.js', {});
  w.FarmIdlePortal.gameplay(true);
  assert.strictEqual(w.FarmIdlePortal.locale(), null);

  // Con el SDK cargado en el <head> (el documento sigue en 'loading') el init espera al DOM: el SDK real agrega cosas al <body>.
  let inits = 0;
  let onReady = null;
  w = load('crazygames-adapter.js', {
    document: { readyState: 'loading', addEventListener: (name, fn) => { assert.strictEqual(name, 'DOMContentLoaded'); onReady = fn; } },
    CrazyGames: { SDK: { init: () => { inits++; return Promise.resolve(); }, ad: { requestAd: () => {} } } }
  });
  await tick();
  assert.strictEqual(inits, 0, 'No se inicia mientras el documento carga');
  assert.strictEqual(w.FarmIdleAdAdapter.isReady('x'), false);
  onReady();
  await tick();
  assert.strictEqual(inits, 1, 'Se inicia una sola vez, con el DOM listo');
  assert.strictEqual(w.FarmIdleAdAdapter.isReady('x'), true);
}

async function crazyGamesPortal() {
  const events = [];
  const sdk = {
    environment: 'local',
    init: () => Promise.resolve(),
    game: { gameplayStart: () => events.push('start'), gameplayStop: () => events.push('stop') },
    user: { systemInfo: { locale: 'es-AR' } },
    ad: { requestAd: () => {} }
  };
  let w = load('crazygames-adapter.js', { CrazyGames: { SDK: sdk } });

  // Una señal que llega ANTES de que el SDK termine de iniciar se guarda y sale al iniciar (una sola vez, la última).
  w.FarmIdlePortal.gameplay(true);
  w.FarmIdlePortal.gameplay(false);
  w.FarmIdlePortal.gameplay(true);
  assert.deepStrictEqual(events, []);
  assert.strictEqual(w.FarmIdlePortal.locale(), null, 'Antes de iniciar no hay idioma');
  await tick();
  assert.deepStrictEqual(events, ['start']);
  assert.strictEqual(w.FarmIdlePortal.locale(), 'es-AR');

  // Ya iniciado: cada cambio sale una vez y lo repetido no sale.
  w.FarmIdlePortal.gameplay(true);
  w.FarmIdlePortal.gameplay(false);
  w.FarmIdlePortal.gameplay(false);
  w.FarmIdlePortal.gameplay(true);
  assert.deepStrictEqual(events, ['start', 'stop', 'start']);

  // Una excepción del SDK no rompe al juego y la señal se reintenta con la siguiente.
  let fail = true;
  const flaky = [];
  w = load('crazygames-adapter.js', { CrazyGames: { SDK: {
    init: () => Promise.resolve(),
    game: { gameplayStart: () => { if (fail) throw new Error('boom'); flaky.push('start'); }, gameplayStop: () => flaky.push('stop') }
  } } });
  await tick();
  w.FarmIdlePortal.gameplay(true);
  assert.deepStrictEqual(flaky, []);
  fail = false;
  w.FarmIdlePortal.gameplay(true);
  assert.deepStrictEqual(flaky, ['start']);
}

// Guardado en el módulo Data del SDK (D-03): lo que el juego espeja con FI_Local* de la jslib.
async function crazyGamesData() {
  const store = {};
  const data = {
    setItem: (k, v) => { store[k] = v; },
    getItem: (k) => (k in store ? store[k] : null),
    removeItem: (k) => { delete store[k]; }
  };
  const sdk = { environment: 'local', init: () => Promise.resolve(), data };

  // Antes de que el SDK inicie no hay módulo Data: el llamador usa localStorage.
  let w = load('crazygames-adapter.js', { CrazyGames: { SDK: sdk } });
  assert.strictEqual(w.FarmIdlePortal.dataSet('k', 'v'), 0, 'Sin iniciar no se guarda en el SDK');
  assert.strictEqual(w.FarmIdlePortal.dataGet('k'), null);
  await w.FarmIdlePortal.whenReady();

  // Iniciado: guarda, lee y borra en el SDK.
  assert.strictEqual(w.FarmIdlePortal.dataSet('k', '{"a":1}'), 1);
  assert.strictEqual(store.k, '{"a":1}');
  assert.strictEqual(w.FarmIdlePortal.dataGet('k'), '{"a":1}');
  assert.strictEqual(w.FarmIdlePortal.dataGet('nada'), null, 'Una clave que no existe es null');
  w.FarmIdlePortal.dataRemove('k');
  assert.strictEqual(w.FarmIdlePortal.dataGet('k'), null);

  // Entorno 'disabled': el SDK tira error, así que ni se toca.
  let touched = 0;
  const trap = () => { touched++; throw new Error('disabled'); };
  w = load('crazygames-adapter.js', { CrazyGames: { SDK: { environment: 'disabled', init: () => Promise.resolve(), data: { setItem: trap, getItem: trap, removeItem: trap } } } });
  await w.FarmIdlePortal.whenReady();
  assert.strictEqual(w.FarmIdlePortal.dataSet('k', 'v'), 0);
  assert.strictEqual(w.FarmIdlePortal.dataGet('k'), null);
  w.FarmIdlePortal.dataRemove('k');
  assert.strictEqual(touched, 0, 'En disabled no se llama al módulo Data');

  // Un error del módulo (por ejemplo "Progress Save" sin marcar, o pasarse del límite) no rompe nada: avisa que no guardó.
  const broken = () => { throw new Error('dataModuleDisabled'); };
  w = load('crazygames-adapter.js', { CrazyGames: { SDK: { environment: 'crazygames', init: () => Promise.resolve(), data: { setItem: broken, getItem: broken, removeItem: broken } } } });
  await w.FarmIdlePortal.whenReady();
  assert.strictEqual(w.FarmIdlePortal.dataSet('k', 'v'), 0);
  assert.strictEqual(w.FarmIdlePortal.dataGet('k'), null);
  w.FarmIdlePortal.dataRemove('k');

  // SDK sin módulo Data, o un valor que no es texto: null / 0.
  w = load('crazygames-adapter.js', { CrazyGames: { SDK: { init: () => Promise.resolve() } } });
  await w.FarmIdlePortal.whenReady();
  assert.strictEqual(w.FarmIdlePortal.dataSet('k', 'v'), 0);
  assert.strictEqual(w.FarmIdlePortal.dataGet('k'), null);
  w = load('crazygames-adapter.js', { CrazyGames: { SDK: { init: () => Promise.resolve(), data: { setItem: () => {}, getItem: () => 5 } } } });
  await w.FarmIdlePortal.whenReady();
  assert.strictEqual(w.FarmIdlePortal.dataGet('k'), null, 'Solo se acepta texto');

  // whenReady nunca se rechaza: se resuelve con el init caído y también sin SDK (con el DOM ya cargado).
  w = load('crazygames-adapter.js', { CrazyGames: { SDK: { init: () => Promise.reject(new Error('sin red')) } } });
  await w.FarmIdlePortal.whenReady();
  assert.strictEqual(w.FarmIdlePortal.dataSet('k', 'v'), 0);
  w = load('crazygames-adapter.js', {});
  await w.FarmIdlePortal.whenReady();
}

async function poki() {
  let w = load('poki-adapter.js', {});
  let c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  assert.deepStrictEqual(c.calls, ['nofill']);

  const make = (reward) => ({ PokiSDK: { init: () => Promise.resolve(), rewardedBreak: reward } });

  w = load('poki-adapter.js', make(() => Promise.resolve(true)));
  await tick();
  c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  await tick();
  assert.deepStrictEqual(c.calls, ['rewarded']);

  w = load('poki-adapter.js', make(() => Promise.resolve(false)));
  await tick();
  c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  await tick();
  assert.deepStrictEqual(c.calls, ['closed'], 'Sin recompensa es closed');

  w = load('poki-adapter.js', make(() => Promise.reject(new Error('x'))));
  await tick();
  c = once();
  w.FarmIdleAdAdapter.showRewarded('x', c.done);
  await tick();
  assert.deepStrictEqual(c.calls, ['failed']);
}

(async () => {
  await crazyGames();
  await crazyGamesPortal();
  await crazyGamesData();
  await poki();
  console.log('Adaptadores de portal: todo bien');
})().catch((e) => { console.error('FALLÓ: ' + e.message); process.exit(1); });
