# Ficha y envío a CrazyGames (D-05)

Borrador de Claude, 2026-11-15. Los textos van en **inglés** (el portal lo exige); el español queda de referencia. Campos del formulario según la FAQ de CrazyGames leída el 2026-11-15: descripción, controles/instrucciones, portadas y video. Si el formulario pide categoría o etiquetas, usar las de abajo (**a verificar en el formulario**: la doc no las lista).

## 1. Textos para pegar

**Título:** Pepe's Berenjena

**Descripción (EN):**

> Pepe's Berenjena is a relaxing arcade idle farming game. You start with a single field and a backpack: walk over ripe crops to harvest them, watch them stack on your back, carry them to the stand and sell them. Every coin goes back into the farm: new zones, eight crops from wheat to sunflowers, and upgrades for speed, backpack size, growth and prices.
>
> Level up to pick perks, fulfil orders from the village, hire helpers that harvest and sell on their own, decorate your farm and unlock new looks for your farmer. When you have bought everything, start a New Season: you restart with a permanent price bonus and golden seeds to spend on upgrades that last forever. Your farm also keeps producing while you are away, so there is always something to collect when you come back.

**Controles / instrucciones (EN):**

> Desktop: move with WASD or the arrow keys, or click and drag anywhere to steer. Press Space to boost.
> Mobile: drag your finger anywhere on the screen to move.
> Everything else happens by walking: step on a ripe crop to harvest and replant it, stand on the sell pad to sell your backpack, and stand on an upgrade or zone pad to buy it.

**Categoría sugerida:** Casual (alternativas: Simulation, Clicker/Idle).
**Etiquetas sugeridas:** farm, idle, farming, harvest, simulation, management, relaxing, upgrade, 3D, mobile.

**Referencia en español** (no se carga): Juego de granja arcade idle y relajante. Caminás sobre los cultivos maduros para cosecharlos, se apilan en la mochila y los vendés en el puesto. Con las monedas abrís zonas, sumás cultivos y comprás mejoras. Al comprar todo empezás una Nueva temporada con un bono permanente. Controles: WASD o flechas, o clic y arrastrar; en el celular, arrastrar el dedo.

## 2. Qué se sube

| Qué | Dónde está | Nota |
|---|---|---|
| Build | `FarmIdle/Builds/Release/Web-CrazyGames/` (la carpeta entera: `index.html`, `crazygames-adapter.js` y `Build/` con 4 archivos) | Regenerado el 2026-11-16 con todo hasta B-11, F-20 y U-12 (parcelas que se guardan, decoraciones señaladas, contraste de carteles), además de U-10, U-11, B-10 y F-19: 6 archivos, 8,4 MB, Brotli. Arrastrar los archivos sueltos, sin `.zip` salvo que el formulario lo pida |
| Portadas | `FarmIdle/Marketing/covers/` (`cover_16x9_1920x1080.png`, `cover_2x3_800x1200.png`, `cover_1x1_800x800.png`) | Son borrador: miralas antes |
| Video | No hay (K-02 quedó para más adelante) | **A verificar en el formulario** si deja enviar sin video; la doc lo lista entre lo que "vas a necesitar" |

## 3. Paso a paso

1. Entrar a <https://developer.crazygames.com/> con tu cuenta (D-02) y completar **Billing** si todavía no está (la doc dice que hace falta para enviar; se puede elegir retener los pagos).
2. **Submit a game** → elegir Unity/HTML5 → arrastrar el contenido de `Web-CrazyGames/`.
3. Pegar título, descripción y controles de la sección 1. Orientación móvil: **vertical (portrait)**.
4. Subir las 3 portadas.
5. Marcar **Progress Save** (el juego guarda con el módulo Data del SDK).
6. Abrir el **preview / QA tool** del portal y jugar un minuto: tiene que aparecer `gameplayStart` en "Detected SDK functionalities" recién después de tocar "Tap to Play". Mirar la consola (F12): sin errores en rojo.
7. Checklist de QA del portal:

| Pregunta | Respuesta |
|---|---|
| Cumple los requisitos de gameplay | Sí |
| Corre en todos los dominios de CrazyGames | Sí (no hay sitelock) |
| Navegadores | Chrome y Edge probados; Safari/Firefox: lo que hayas probado |
| Dispositivos | Escritorio y móvil (probar con "Show QR" en el celular) |
| Anuncios externos | No |
| Login externo | No |
| Mención de Términos / Privacidad | **N/A**: la versión web no junta datos personales (analítica apagada en release) |

8. Enviar. La respuesta llega por mail.

## 4. Qué se comprobó en este build (2026-11-15, en `localhost`)

| Requisito de la doc | Resultado |
|---|---|
| Máx. 250 MB y 1500 archivos; carga inicial ≤ 50 MB (≤ 20 MB para la home móvil) | 6 archivos, 8,4 MB |
| Solo rutas relativas | `index.html` carga `crazygames-adapter.js` y `Build/…` relativos; lo único externo es el SDK oficial |
| `user-select: none` en el `body` | Está en el `index.html` |
| Sin otras redes de anuncios, sin login externo, sin enlaces a tiendas | Ninguno en la página; el botón de privacidad está oculto (no hay URL cargada) |
| SDK | `crazygames-sdk-v3.js` (la doc sigue apuntando a v3); se inicializa como 3.8.0, entorno `local` |
| `Gameplay start` | Sale al tocar "Tap to Play" (`Requesting gameplay start (local)`) |
| Progress Save | El módulo Data guarda `farmidle.save.main` (incluye ayudantes y pedidos) |
| Inglés por defecto | Arranca en inglés con `locale` en-US |
| Vertical con barras a los lados en escritorio | Se ve así en una ventana de 800×450 |
| Errores de consola | 0 al cargar y al empezar a jugar |

No se comprobó acá: el preview del portal, FPS, Safari/Firefox, el celular ni una partida larga (el Q-10 que diste por listo es anterior a este build).

## 5. Antes de apretar "enviar"

- **Un rechazo solo se puede reenviar con mejoras claras** (FAQ, 2026-11-15): conviene enviar una vez y bien.
- **Fecha a tener en cuenta (a verificar):** la guía de Basic Launch dice que desde el **23 de noviembre de 2026** el puntaje suma una cuarta métrica, el CTR (clics sobre la portada), además de tiempo de juego, retención D1 y conversión. Rige para los juegos que arrancan el Basic Launch desde esa fecha. Con CTR en juego, las portadas pesan más.
- Basic Launch: sin anuncios y sin ingresos; mínimo 7 días **y** 500 jugadas (o 21 días). Referencias de la doc: 10+ min por sesión, D1 10 a 15 %, conversión 80 %+, carga < 10 s.
- Lo que sigue sin hacerse de la lista de `CRAZYGAMES_REQUISITOS.md` §8: que otra persona lo juegue de punta a punta y lea el inglés, y probar Safari/Firefox.
