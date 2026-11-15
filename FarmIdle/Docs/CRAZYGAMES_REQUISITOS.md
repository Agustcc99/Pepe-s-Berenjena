# D-01 — Requisitos vigentes de CrazyGames

Leído el **2026-10-20** por Claude (a pedido del usuario) desde la documentación oficial. Fuente base: <https://docs.crazygames.com/> (las páginas citadas abajo). Todo dato de mercado queda "a verificar" por el usuario antes de decidir; lo marcado **[NO PUBLICADO]** no figura en las docs y hay que leerlo en los Términos al crear la cuenta (D-02).

## 1. Cómo funciona el lanzamiento (cambia el plan)
Fuente: [intro de requisitos](https://docs.crazygames.com/requirements/intro/), [FAQ](https://docs.crazygames.com/faq/), [Basic Launch](https://docs.crazygames.com/resources/basic-launch-metrics/).

| Etapa | Qué es | Anuncios / ingresos |
|---|---|---|
| **Basic Launch** | El juego sale sin adaptarse a CrazyGames. SDK **opcional** (si lo integrás, debe disparar `Gameplay start`). Se muestra a un grupo chico de jugadores para medir retención, tiempo de juego, CTR y conversión. | **Anuncios apagados, sin ingresos**. |
| **Full Launch** | Solo juegos seleccionados con buenos KPIs. SDK **obligatorio**. Segunda revisión de QA antes de salir. | **Anuncios activos**, hay reparto de ingresos. |

- Basic Launch dura mínimo **7 días y 500 jugadas** (ambas); si no llega a 500, termina solo a los **21 días**. KPIs en el panel, se refrescan cada día (retención D1 con un día extra). Las actualizaciones en Basic Launch salen al instante.
- Referencias de "buen" rendimiento que da la doc: **juego ≥ 10 min por sesión, retención D1 10–15 %, conversión (juega ≥ 1 min tras empezar) ≥ 80 %, carga < 10 s, build < 20 MB**. Promedio de la plataforma de retención D1: 6,7 %. El umbral exacto para pasar a Full Launch **[NO PUBLICADO]** ("strong KPIs").
- Plazo de revisión de la primera entrega **[NO PUBLICADO]**: se avisa por mail aceptación o rechazo. Las actualizaciones se procesan normalmente el mismo día hábil.

**Consecuencia para el roadmap:** nuestro Hito 3 asume SDK + anuncios desde el día 1 (D-03, B-03). En realidad el primer tramo es Basic Launch (sin anuncios, sin ingresos). Conviene subir primero con SDK mínimo o sin SDK, y recién después de pasar a Full Launch activar anuncios. Propuesta de cambio de alcance en el cierre (no aplicada).

## 2. Requisitos técnicos
Fuente: [Technical](https://docs.crazygames.com/requirements/technical/).

| Tema | Requisito | Nuestro estado |
|---|---|---|
| Tamaño total | Máx. **250 MB**, máx. **1500 archivos** | Web Brotli 8,4 MB: sobra |
| Descarga inicial | ≤ **50 MB**; ≤ **20 MB** para estar en la home móvil. Con SDK se mide hasta el primer `Gameplay start` | 8,4 MB: cumple |
| Sin SDK | Cuenta el tamaño total: ≤ 50 MB (≤ 20 MB móvil) | Cumple |
| Archivos externos | QA mide el tiempo hasta el gameplay: ≤ **20 s** | n/a |
| Rutas | Solo relativas | A verificar en la build |
| Compresión | Solo corren builds comprimidas (**Brotli o Gzip**); las de desarrollo no | Brotli y Gzip generados |
| Navegadores | Chrome y Edge obligatorios; Safari/Firefox recomendados (si anda mal en Safari lo deshabilitan ahí) | Probar en Q-01 |
| Entrada | Mouse + teclado, y táctil si soporta móvil | Falta teclado (U-05) |
| Orientación | Desktop jugable en horizontal; **vertical permitido** con barras negras/fondo a los lados | Hace falta marco centrado (U-05) |
| Móvil | CSS `user-select: none` en `body`; probar en la app de CrazyGames (fullscreen + safe-area) | A hacer en la plantilla WebGL |
| iOS | **Los juegos Unity están deshabilitados en iOS por defecto** (crashes de memoria); se evalúan si llegan a muchas jugadas | Sin iPhone, irrelevante |
| Chromebook | Se deshabilita si no corre fluido en 4 GB RAM | A probar si se puede |
| Sitelock | Si se usa, permitir todos los dominios de crazygames.com | No usamos |
| Datos personales | Si se recolectan más datos que los eventos del SDK, aviso de T&C/Privacidad (aviso simple, no pop-up bloqueante) | M-04 |
| Resolución | Legible a DPR 1 y en 907×510, 1216×684, 1077×606, 821×462 (ventana); 1366×768, 1920×1080, 1536×864, 1280×720 (pantalla completa); 800×450 móvil; 1080×607 tablet | Probar UI vertical en estas medidas |

## 3. Requisitos de gameplay
Fuente: [Gameplay](https://docs.crazygames.com/requirements/gameplay/), [Quality](https://docs.crazygames.com/requirements/quality/).
- **Inglés obligatorio**; traducciones de buena calidad; usar el `locale` del SDK y caer en inglés. (Ya tenemos ES/EN.)
- Física consistente a 144/165 Hz (cuidar `Time.deltaTime`; el tope 11 u/s depende del tiempo).
- Rendimiento fluido, sin errores ni crashes. Nombre, assets y contenido **originales** (no clones ni asset flips; los modelos Kenney CC0 son lícitos pero la combinación debe ser distinguible).
- **Prohibido** un botón propio de pantalla completa. **Sin promoción cruzada** de otros juegos/plataformas (excepciones: política de privacidad/términos, Discord o web propia en el menú sin enlazar a una versión jugable, juegos de la misma serie). **Nunca** links a tiendas de apps en el juego.
- Apto para **13+, PEGI 12**; juegos para niños rechazados. El dominio kids.crazygames.com no monetiza.
- **Full Launch:** el jugador nuevo debe caer **directo en gameplay** (máx. 1 clic). Nuestro "Tocá para jugar" cuenta como 1 clic.
- Guías (recomendadas): onboarding corto y saltable, controles consistentes, adaptar teclas al layout (AZERTY), no usar Esc ni Ctrl/Cmd+W, audio parejo, estilo visual consistente.

## 4. Reglas de anuncios
Fuente: [Ads](https://docs.crazygames.com/requirements/ads/), [Video ads](https://docs.crazygames.com/sdk/video-ads/), [Rewarded deep dive](https://docs.crazygames.com/resources/rewarded-ads-deep-dive/), [Midcore/Idle](https://docs.crazygames.com/resources/monetizing-midcore-idle/).
- **Solo anuncios pedidos por el SDK de CrazyGames**: ninguna otra red (nada de AdMob en la build web). Hoy el adaptador web no usa el SDK real (D-03).
- Tipos: midgame, rewarded, banners.
- **Midgame:** nunca en pleno juego; en transiciones lógicas; no en botones de navegación (menú/ajustes/tienda). Pausar el juego, bloquear la UI hasta `adFinished`/`adError`. Silenciar el audio **cuando el anuncio realmente empieza**, no al pedirlo. El SDK ignora pedidos con menos de **~3 min** (cuenta también los rewarded): error `adCooldown`.
- **Rewarded:**
  - Es una oferta opcional, nunca una expectativa; ningún nivel debe poder completarse *solo* viendo un anuncio.
  - No se encadenan (una recompensa = un solo anuncio); no ofrecerlos con demasiada frecuencia (usar temporizador o esconder el botón).
  - El botón **no** puede aparecer en pantalla de juego activo; ubicación accesible y consistente.
  - El botón de rechazar/continuar tiene el mismo tamaño, fuente y color; claro de entrada que es opcional; icono de video.
  - **Debe existir una alternativa** (por ejemplo comprar la recompensa con monedas del juego).
  - En `adFinished`: mostrar la recompensa claramente. En `adError`: **no dar la recompensa**.
  - Entre dos niveles, no combinar midgame + rewarded.
  - Botón con texto de la recompensa ("Mirá un anuncio: +50 monedas"), no "Watch Ad".
  - Sin anuncio disponible (`unfilled`): ocultar el botón o avisar "probá más tarde". **Sin botones de rewarded sin efecto.**
- **Banners:** solo en pantallas que se quedan abiertas ≥ 5 s, nunca en gameplay ni tapando UI, máx. 2 por pantalla. (No planeamos banners.)
- **AdBlock:** nunca bloquear ni penalizar a jugadores con adblock; se pueden deshabilitar funciones extra con un aviso (sin pop-up).
- Códigos de error del SDK: `adsDisabledBasicLaunch`, `unfilled`, `adblock`, `adCooldown`, `other`.
- Ideas de la doc para idle (no probadas): rewarded que duplica/triplica la ganancia offline al volver (calza con B-01), rewarded + banners de menú, midgame solo en transiciones grandes. Premio escalado al progreso; tope tipo ~5 por día (ejemplo de la doc); cuidar la inflación de la moneda.

## 5. SDK y guardado
Fuente: [SDK intro](https://docs.crazygames.com/sdk/intro/), [Data](https://docs.crazygames.com/sdk/data/), [Account integration](https://docs.crazygames.com/requirements/account-integration/).
- Unity: se importa `UnityCrazySDK.unitypackage` (borrar antes las carpetas viejas `CrazySDK`/`CrazyOptimizer`). Init: `CrazySDK.Init(() => {...})`; chequear `CrazySDK.IsAvailable`; **no** inicializar en `RuntimeInitializeOnLoadMethod`. Módulos: `Ad`, `Banner`, `Game`, `User`, `Data`. Ads: `CrazySDK.Ad.RequestAd(CrazyAdType.Midgame/Rewarded, started, error, finished)`. El SDK pausa el juego solo en Unity. Entorno `local`/`crazygames`/`disabled`.
- Versión del SDK de Unity **[NO PUBLICADA]** en la doc (hay un `UnityChangelog.md`): verificar la vigente en D-03. Agregar el SDK requiere tu OK (peso de build, regla del proyecto).
- Eventos de gameplay: `Gameplay start/stop` obligatorios en Full; `Load start/stop` opcionales. Whitelist del dominio propio en `CrazySDK/Resources/CrazyGamesSettings`.
- **Guardado:** módulo Data (preferido), API tipo PlayerPrefs (`SetInt/GetString`…); invitados van a `localStorage` y se sincronizan al loguearse. **Límite 1 MB** por usuario (JSON); guardado con debounce de 1 s (hasta 30 s). Hay que marcar "Progress Save" al enviar el juego. Nuestro guardado versionado tiene que migrar a este módulo (D-03).
- Cuentas: los invitados deben poder jugar; el único login permitido es el de CrazyGames; juegos sin cuentas no necesitan el módulo User.
- Compras dentro del juego: **solo por invitación** (requiere cuenta de Xsolla). No están disponibles para nosotros por ahora.
- Herramienta "custom build" de Unity (menú `CrazySDK`): genera build optimizada en `Builds/CrazyGamesRelease`; marcada por ellos como nueva, se puede entregar la build normal.
- Problemas Unity conocidos: bloqueo de cursor al inicio (esperar interacción), URP en Mac con mallas negras (desactivar Depth Priming Mode), picos de mouse en WebGL. Consejos de tamaño: Brotli, LTO "Disk Size", URP suma ~3,6 MB, texturas y audio mono.

## 6. Ingresos, pagos y exclusividad
Fuente: [Payouts](https://docs.crazygames.com/payouts/), [FAQ](https://docs.crazygames.com/faq/).
| Tema | Dato | Estado |
|---|---|---|
| Reparto de ingresos | **[NO PUBLICADO]** en las docs; la FAQ remite a los Términos para desarrolladores. Fuentes de terceros mencionan 60 % del ingreso publicitario al desarrollador y 70 % en compras (terceros: términos de un *game jam*; no confirmado) | **A verificar** en los Términos (se ven al crear la cuenta, D-02) |
| Condiciones para cobrar | Juego en Full Launch, SDK integrado, sin otros anuncios ni marca de otro portal, contenido original (citado de la FAQ) | Doc oficial |
| Procesador | Tipalti; hay que completar Cuenta → Billing (pasos 1–4) para poder enviar un juego (se puede elegir "hold payments") | Doc oficial |
| Métodos | Transferencia bancaria, ACH, eCheck, PayPal (según país) | Doc oficial; disponibilidad para Argentina **a verificar** |
| Mínimo | **€100** para cobrar; menos se acumula al mes siguiente. Umbral de factura: €10 | Doc oficial |
| Frecuencia | Mensual; término NET 60, apuntan a pagar antes del día 10 del mes siguiente | Doc oficial |
| Facturación | CrazyGames emite facturas de autofacturación; Tipalti pide el formulario fiscal según país/entidad | Hablar con contador (L-05) |
| Moneda | PayPal paga en USD; hay comisiones de cambio | Doc oficial |
| Exclusividad | La FAQ dice que publicar en Steam, Google Play, App Store o Facebook **no afecta** el reparto; no se publica ninguna exclusividad. Un bono por exclusividad temporal aparece solo en fuentes de terceros viejas | **A verificar** en Términos antes de D-13 |
| Propiedad | Seguís siendo dueño del juego; hay que tener los derechos de distribución | Doc oficial |
| Portales competidores | No iframean juegos de portales competidores | Doc oficial |
| Soporte | developer.crazygames.com/support; soporte técnico de SDK desde 50 000 jugadas combinadas | Doc oficial |

## 7. Qué tiene que revisar el usuario (para cerrar D-01 del todo)
1. Reparto de ingresos exacto y cláusula de exclusividad en los **Términos para desarrolladores** (aparecen al crear la cuenta, D-02).
2. Si Tipalti admite Argentina y con qué método (D-02, paso Billing).
3. Versión vigente del SDK de Unity y su requisito de versión de Unity (descargar el paquete en D-03).

## 8. Lista "Before you submit" y cómo estamos (2026-10-29)
Texto que pegó el usuario desde CrazyGames (a verificar contra la página vigente antes de enviar). Dice que son los motivos más comunes de rechazo en la revisión manual; el criterio de fondo es que el jugador decide en los primeros minutos.

| Requisito de CrazyGames | Estado | Qué falta |
|---|---|---|
| Carga y juega en Chrome de escritorio en ≤ 20 s, sin errores | Sin medir en el portal. Build de 8,5 MB y 0 errores de consola con el SDK real en `localhost` | Medir carga hasta el gameplay y consola en el preview del portal (D-03, D-04, Q-10) |
| Probado en móvil, o marcado "solo escritorio" | Hecho: A70 con Chrome, "anda perfecto" (Q-01) | Probar también dentro del portal en el celular |
| Idioma por defecto: inglés | El mecanismo está: con el `locale` del SDK manda el portal, y sin él solo el español del sistema da ES, el resto EN (`Loc.ResolveAuto`) | El inglés lo redactó Claude y nadie lo revisó: lo lee el usuario o alguien que hable inglés |
| El primer minuto deja claro qué hacer, casi sin leer | Hay guía con flechas (`TutorialManager`) | Nunca la vio alguien que no conozca el juego |
| Alguien que no seas vos lo jugó de principio a fin | **No**: Q-03 quedó `Descartado` el 2026-10-28 | Playtest corto con 2 o 3 personas sobre la web (ver propuesta en el roadmap) |
| No es una plantilla de assets ni un clon 1:1; derechos sobre código, arte y sonido | Código propio; modelos y audio Kenney CC0 y música propia, inventariados en `THIRD_PARTY.md` | Confirmar que el juego no copia 1:1 a ningún otro; si en 1.1 entra un pack pago, leer su licencia (L-02) |

## 9. Relectura completa de la doc (2026-10-29)
Leídas en docs.crazygames.com: requirements (intro, technical, gameplay, ads, quality, game-covers), sdk (game, data), resources (basic-launch-metrics, getting-to-the-first-frame, unity-custom-build, monetizing-midcore-idle, issues) y FAQ. **No leídas todavía** (por si hacen falta): account-integration, multiplayer, optimization-tips, crazygames-app, sitelock/common-fixes, rewarded-ads-deep-dive, midgame-ads-pacing.

**Lo que no sabíamos o estaba mal**
- **Rechazo por botones de video que no hacen nada** (ads): en Basic Launch los juegos con el SDK se revisan con anuncios apagados, y "rewarded buttons that do nothing" se rechazan; tampoco se deja un botón de video clickeable sin efecto con adblock. Por eso los pads de video ya no se ofrecen en la web (`AdPolicy`).
- **Basic Launch**: las actualizaciones salen al instante, pero un juego **rechazado solo se puede reenviar con "mejoras significativas"** (FAQ). Conviene enviar una sola vez y bien. Referencias de KPI: partida de 10+ min de promedio, D1 de 10 a 15 %, conversión (≥ 1 min jugado) 80 %+, carga < 10 s y build < 20 MB. Sin umbral numérico para pasar a Full Launch.
- **Idioma**: inglés obligatorio, y la doc pide usar el `locale` del SDK con respaldo en inglés. Por pedido del usuario la web arranca siempre en inglés (`Loc.AutoIsEnglishOnly`) y se cambia con el botón del menú o con Ajustes; los jugadores en español ven inglés hasta tocarlo.
- **Covers** (K-01): 3 imágenes: 1920×1080 (16:9), 800×1200 (2:3) y 800×800 (1:1); solo el título como texto (nada de "New", "Play" ni logos de tiendas), sin bordes. **Videos** (K-02): 15 a 20 s, máx. 50 MB, dos versiones 1080p (16:9 y 2:3), **sin sonido**, sin acelerar, el primer cuadro es la portada, sin pantalla negra, sin barras, sin cursor por defecto, sin texto promocional.
- **Gameplay start/stop**: `gameplayStop` en cada corte (menú, pausa, fin de nivel) pero **no** al perder el foco. Nuestro juego no pausa por foco (la pausa solo existe en el panel de debug), así que cumple. El primer `gameplayStart` fija el tamaño de carga medido.
- **Datos**: tope 1 MB (nuestra partida nueva mide 1,3 KB), hay que marcar "Progress Save" (hecho) y la doc pide apoyarse en el módulo Data y migrar lo que había en localStorage (nuestra lectura prefiere al portal y cae a localStorage).
- **muteAudio**: para Unity enviado como Unity la doc dice que el silencio lo maneja el portal solo. (Antes sugerí no tildar la casilla de silenciar por SDK; no es seguro, mirar el texto del formulario.)
- **Consentimiento**: el aviso de Términos o Privacidad solo hace falta si el juego junta datos personales más allá de los eventos del SDK; la web no junta ninguno, así que en el QA corresponde **N/A**.
- **Más reglas**: sin botón propio de pantalla completa (cumple), sin promoción cruzada, sin Esc ni Ctrl+W (cumple), teclas según el teclado (AZERTY: no cumple, es guía), física igual en 144 y 165 Hz, legible a DPR 1 en 907×510, 1216×684, 1077×606, 821×462 (ventana), 1366×768, 1920×1080, 1536×864, 1280×720 (pantalla completa), 800×450 móvil y 1080×607 tablet, Safari (si falla se deshabilita ahí), iOS apagado por defecto para Unity.
- **Para el Full Launch**: el botón de video **no puede estar en una pantalla de juego activo**, tiene que estar en un lugar consistente y fácil de encontrar, y hay que **ofrecer una alternativa sin video** (el Turbo hoy no tiene). Rewards idle recomendados por la doc: cofre diario, reponer energía, duplicar lo ganado offline (la doc dice que no está probado en el género) y rewards que escalan con el nivel.
- **Checklist de QA del portal** (captura del usuario): tamaño inicial (automático), cumple requisitos de gameplay, corre en todos los dominios de CrazyGames, chequeo de navegadores, chequeo de dispositivos (móvil, con "Show QR"), sin anuncios externos, sin login externo, mención de Términos/Privacidad (Sí/No/N/A). La sección "Detected SDK functionalities" salió **vacía**: hay que volver a probarlo **después de jugar** (el `gameplayStart` aparece al tocar "Tocá para jugar").

## 10. Relectura del 2026-11-15 (antes de enviar)
Releídas: requirements (intro, technical, gameplay, ads, quality, game-covers), FAQ y basic-launch-metrics. **A verificar** por el usuario como todo dato de mercado.
- **Sin cambios** en lo que nos toca: 250 MB / 1500 archivos, carga inicial ≤ 50 MB (≤ 20 MB home móvil), rutas relativas, Chrome y Edge, vertical con barras, `user-select: none`, resoluciones, inglés obligatorio, sin botón propio de pantalla completa ni promoción cruzada, 13+ / PEGI 12, reglas de rewarded, aviso de privacidad solo si se juntan datos personales, medidas de portadas y video.
- **Cambio con fecha:** la guía de Basic Launch dice que el puntaje cambia el **23 de noviembre de 2026**: el **CTR** pasa a ser la cuarta métrica (con tiempo de juego, retención D1 y conversión) para los juegos que empiezan o reinician el Basic Launch desde ese día. Los anteriores siguen con el puntaje actual.
- **Reenvío tras un rechazo:** la FAQ ahora dice "clear improvements" (antes anotamos "significant") y avisa que con cambios chicos el resultado va a ser el mismo.
- **No aparece en estas páginas** (lo teníamos anotado de otras fuentes): la exigencia de Brotli/Gzip y el rechazo de `*.unityweb`, el "≤ 20 s en Chrome de escritorio" como requisito general (hoy figura solo para archivos externos) y la lista "Before you submit". No hay requisito de versión de Unity ni del SDK, ni campos de categoría o etiquetas.
- El SDK sigue siendo `crazygames-sdk-v3.js`; el que baja hoy se presenta como 3.8.0.
- Estado del build contra esta lista: `FICHA_CRAZYGAMES.md` §4.

**Faltantes antes de enviar (al 2026-10-29)** (ordenados): (1) confirmar que el QA detecta `gameplayStart`; (2) medir carga ≤ 20 s (ideal < 10 s) y legibilidad en las resoluciones de la lista; (3) probar Safari y Firefox; (4) que otra persona lo juegue de punta a punta y revisar el inglés; (5) covers y videos (K-01, K-02); (6) descripción y controles del formulario (D-05); (7) sin DEC-2 cerrada no hay nombre final.
