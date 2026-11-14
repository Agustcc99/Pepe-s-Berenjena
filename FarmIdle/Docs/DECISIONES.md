# Registro de decisiones

Roadmap AR-06. Una entrada corta por decisión que no se deduce leyendo el código: **qué**, **por qué** y **cuándo** (con el ID del roadmap). Lo más nuevo, arriba. Una decisión que se revierte no se borra: se agrega una entrada nueva que la reemplaza.

### Nombre del estudio: Pepe's Dev (DEC-2b) · 2026-11-04
- **Qué:** el estudio se llama "Pepe's Dev"; es el titular de la política de privacidad (M-04) y de las cuentas de Play (D-07/D-09).
- **Por qué:** lo eligió el usuario; va con el juego "Pepe's Berenjena". Chequeo de INPI y de homónimos: pendiente (no verificado por Claude).

### Tienda de prestigio: semillas doradas y 6 mejoras de 3 niveles (F-11) · 2026-11-04
- **Qué:** cada «Nueva temporada» da `GameConfig.SeedsForSeason(n)` = 1 + n/2 semillas (1, 2, 2, 3, 3...). Se gastan en 6 mejoras permanentes (`GameConfig.prestigeShop`): monedas iniciales (+300/+1.000/+3.000), crecimiento (×1,1/1,2/1,35), tope offline (+1/+2/+4 h), venta (×1,1/1,2/1,3, se multiplica con el prestigio), fertilizante de regalo (1/2/3) y XP (×1,15/1,3/1,5). Costo total 32 semillas ≈ 9 temporadas. Guardado `prestige` v2 (semillas + niveles por id; v1 migra a 0).
- **Por qué:** es el motivo para reiniciar y le da qué comprar al final. Los números son provisorios (sin datos reales): se ajustan en el asset, no en código. Un test limita venta × crecimiento al máximo a ×2 y exige que comprar todo tome de 4 a 12 temporadas.
- **Cómo:** los demás sistemas leen `PrestigeManager.GrowthFactor/XpFactor/OfflineCapBonus` (estáticos neutros sin manager); UI en `PrestigeShopView` (botón «Semillas» bajo «Nueva temporada»). No hay bot de prestigio en las pruebas de balance: la cuenta es analítica.

### Fertilizante: el precio sube ×1,8 por cada zona abierta (B-08) · 2026-11-03
- **Qué:** `FertilizerManager.Price` pasa de 1.500 fijos a `GameConfig.FertilizerPriceFor(zonas abiertas)`: 1.500 / 2.700 / 4.850 / 8.750 / 15.750 / 28.350 con 0 a 5 zonas (factor `fertilizerPriceStepPerZone` = 1,8, redondeo a 50). El cartel del pad se repinta al abrir una zona. Solo afecta la compra con monedas: el video sigue gratis. Una temporada nueva cierra las zonas y el precio vuelve a bajar.
- **Por qué:** pedido del usuario. El fertilizante termina de crecer todos los campos, así que con las 6 granjas rendía mucho más de lo que costaba. Se eligió ×1,8 entre ×1,5 (último precio ≈ 1,7 min de ingreso del final), ×1,8 (≈ 4,2 min) y ×2,2 (≈ 11,5 min).
- **Número provisorio:** el ingreso de ~6,75 mil por minuto sale de `ECONOMIA_V1.md`, y el bot de balance no modela la compra con monedas. Se ajusta con datos reales (B-05) cambiando solo el factor en `GameConfig`.

### Web como prueba ya; Android recién completo (DEC-1 y DEC-4) · 2026-11-03
- **Qué:** la web se envía a CrazyGames como Basic Launch (grupo chico, sin anuncios ni ingresos, actualizable al instante) en cuanto estén portadas, ficha y política. Android **no sale a producción** hasta tener las compras nuevas (F-11 y las que se aprueben) y el arte final (Hito 6). Lo previo a producción de Android (cuenta de Play, firma, AdMob de prueba, prueba cerrada con testers) puede avanzar antes.
- **Por qué:** el usuario temía publicar sin estar completo. En la web el Basic Launch es una prueba por diseño del portal (fuente: docs.crazygames.com, leída el 2026-10-28; el umbral para Full Launch no está publicado) y mide retención del día 1 y tiempo de sesión, que dependen de los primeros minutos, no del final. En las tiendas la primera impresión y las reseñas no se rehacen, así que ahí se espera. Reemplaza lo de DEC-4 ("Android después de 2 a 4 semanas de datos") y mantiene DEC-1 solo para la web.
- **Riesgo:** el plazo de revisión de la primera entrega en CrazyGames no está publicado; si el Basic Launch sale con mala retención, hay que revisar los primeros minutos antes de gastar tokens en contenido del final.

### Tope en monedas del progreso offline: 25.000 por hora fuera (B-07) · 2026-10-30
- **Qué:** lo que se acredita al volver (antes de Duplicar) no pasa de `GameConfig.offlineMaxCoinsPerHour` = **25.000 por cada hora acreditada**: con el tope de tiempo de 2 h son 50.000, y con el Almacén (3 h y 4 h) suben a 75.000 y 100.000. Duplicar multiplica lo ya limitado. 0 = sin límite.
- **Por qué:** el tope era solo de tiempo (2 h al 25 %) y las parcelas del final rinden mucho a "cosecha sin parar": el usuario volvió con ~150.000 monedas, casi lo que cuesta la compra más cara del juego (146.000). Un tope fijo en monedas (no por hora) habría dejado inútil al Almacén (140.000 y 146.000 monedas): por hora, el Almacén sigue sumando.
- **Número provisorio:** 25.000 sale de la cuenta (50.000 por 2 h ≈ 7 min del ingreso activo del final, ~7 mil por minuto según `ECONOMIA_V1.md`), no de una medición; se ajusta con juego real (B-05). El bot de balance no modela lo offline.

### AR-05 tanda 1: la Fase 6 se parte en cuatro piezas · 2026-10-30
- **Qué:** `Phase6SceneBuilder` (789 líneas) pasa a `ProgressionData`, `PlazaBuilder`, `ZoneBuilder` y `PadBuilder`. Los `.asset` salen byte a byte iguales (665 archivos, hash antes y después); las escenas se regeneran. Sin cambios de lógica ni de números.
- **Por qué:** el archivo mezclaba datos de balance, la plaza, las zonas y lo común de todos los pads; cada cambio de arte o de balance obligaba a leer los 789 renglones. Y pasaba el tope de 500 líneas de `ArchitectureTests`.
- **Cómo se verifica una tanda:** `BuildAll` antes y después, hash de todo lo de `Assets` menos escenas y scripts (con sus `.meta`), y las dos suites.

### Motor en el formulario de CrazyGames: HTML5, no Unity 6 (D-03) · 2026-10-29
- **Qué:** el build `Web-CrazyGames` se sube eligiendo **HTML5**. Con "Unity 6" el portal arma su propia página (`.../unity/unity2020/...`, con `Web.loader.js` y `Web.framework.js.br` por `<script>`) y **no carga nuestro `index.html` ni `crazygames-adapter.js`**: el QA no detectaba el SDK y el Log solo mostraba `gameLoaded` y `gameFinishedLoading`. Con HTML5 aparece la lista de funciones del SDK y el QA mide la carga (10 s, 8,4 MB).
- **Riesgo abierto:** para HTML5 el QA pide que el juego ande bien en **iOS y Android** (con Unity decía solo Android; la doc dice que Unity en iOS está apagado por defecto por fallas de memoria). No hay iPhone para probarlo. Si falla en iOS, opciones: marcar solo escritorio y Android, o pasar al motor Unity con el paquete oficial de CrazyGames (SDK nuevo: pide OK por el peso).
- **Sin verificar todavía:** que el QA marque como detectadas `Gameplay Start` y `Data` después de jugar.

### Plaza en cinco sectores con avenida, vallas con colisión y carteles · 2026-10-29
- **Qué:** la plaza (antes un cuadrado con filas) ahora tiene una avenida central de tierra desde la entrada y cinco sectores con valla, puerta y cartel: **Videos** (turbo y fertilizante por video) y **Atributos** (Crecimiento, Precio, Velocidad, Mochila) al oeste; **Fertilizante** (usar y comprar), **Puntos** (los dos perks) y **Almacén** al este. Cada sector deja un pasillo libre al norte y los pads al fondo, así entrar no obliga a pisar uno. Las vallas **tienen colisión** (`PlazaWalls`): solo se entra por las puertas. Todo sale de las constantes de `PlazaBuilder` (antes en `Phase6SceneBuilder`; `WestFarX`, `VideoRowZ`...); el cartel es `PlazaSignLabel` con claves `plaza.*`. El recinto se achicó de z -28,8 a -27.
- **Por qué:** pedido del usuario (secciones separadas con sus rutas, no un cuadrado con todo junto). Costo de balance: los caminos más largos subieron el hueco máximo del bot de 24,9 a 25,1 min (el tope es 25), así que la Quinta del Nordeste bajó de 85.000 a 83.000. `BalanceBot` camina en línea recta, sin pasar por puertas, así que el recorrido real es algo más largo que el simulado. Las posiciones están duplicadas en `BalanceBot` y en `OfflineIntegrationTests`.

### Web: inglés por defecto, sin anuncios en el Basic Launch y sin cubos de referencia (D-03, D-04) · 2026-10-29
- **Qué:** `Loc.AutoIsEnglishOnly` (lo activa el `Bootstrapper` en WebGL): "Automático" es inglés sin mirar el idioma del sistema ni el del portal; botón de idioma en el menú (`LanguageSwitchButton`, rótulo con el otro idioma) además del de Ajustes. `AdPolicy`: en la web los pads de video (`AdGatedObject`) y el "Duplicar" del regreso se apagan salvo build con `FARMIDLE_WEB_ADS=on` (Full Launch). Se sacaron los cubos rojos de referencia del mundo (`Phase1SceneBuilder`).
- **Por qué:** CrazyGames exige inglés por defecto (la doc además recomienda usar el `locale` del SDK: elegimos inglés siempre a pedido del usuario, costo: un jugador en español ve inglés hasta tocar el botón). En Basic Launch no hay anuncios y se rechazan los botones de video que no hacen nada. Los cubos eran ayudas de las primeras fases.

### Build web sin respaldo de descompresión (D-03) · 2026-10-29
- **Qué:** `ReleaseBuilder.ApplyWebSettings` deja `decompressionFallback` apagado por defecto (archivos `*.br`); `FARMIDLE_WEB_FALLBACK=on` lo reactiva.
- **Por qué:** el formulario de subida de CrazyGames rechazó el build con `*.unityweb` ("Found .unityweb files... Decompression fallback"). Costo: un servidor estático simple ya no sirve el build solo; para probar en local hay que mandar `Content-Encoding: br` o construir con la variable.

### Título visible: "Pepe's Berenjena" (singular) (DEC-2) · 2026-10-29
- **Qué:** `menu.title` y los créditos del CSV, y `PlayerSettings.productName` solo en el build web (`ReleaseBuilder.ApplyWebSettings`, da el `<title>` de la pestaña). Android conserva "FarmIdle" hasta probar que el apóstrofo no rompe la etiqueta de la app. El título del menú se parte en dos líneas con autoajuste (60 a 150) porque no entra en una.
- **Por qué:** el formulario de CrazyGames exige que el nombre sea igual al título dentro del juego. El usuario escribió el nombre en singular (antes "Berenjenas"); reemplaza a la entrada de abajo. Sin verificar a ojo cómo queda el título en el menú.
- **Cierre de DEC-2 (2026-11-03):** nombre final **"Pepe's Berenjena"** (singular, el que ya está en el juego). Según el usuario (Claude no lo verificó): no hay otro juego con ese título en Play ni en CrazyGames, no hay registro en el INPI y el dominio está libre pero no le interesa por ahora. **Nombre del estudio: pendiente** (el usuario dijo "después"); hace falta antes de M-04 (titular de la política) y de D-07/D-09 (cuenta y ficha de Play).

### Pad fijo de Turbo, en todas las plataformas (D-14) · 2026-10-29
- **Qué:** `TurboPad_Video` en la fila del fertilizante de la plaza, que pasó a 4 columnas (valla de 7,4 a 8,6). El símbolo flotante sigue en Android y comparte el enfriamiento de 90 s con el pad.
- **Por qué:** en la web el símbolo está apagado (entrada siguiente) y sin pad no había forma de pedir el Turbo por video. Una sola escena para todas las plataformas.

### Símbolo flotante de Turbo apagado en la web (D-04) · 2026-10-28
- **Qué:** `OfferManager.SymbolsAllowedOn` da `false` en WebGL.
- **Por qué:** la regla de rewarded de CrazyGames no admite el botón de pedir el video en una pantalla de juego activo. Qué cuenta como "juego activo" no está definido: se confirma con la revisión del portal.

### Tecla Esc deshabilitada (D-04) · 2026-10-28
- **Qué:** Esc ya no abre ni cierra Ajustes (se sacó el `Update` de `SettingsPanelView`).
- **Por qué:** la guía de CrazyGames recomienda no usar Esc. La pista del menú ahora dice solo "Mover: WASD o flechas / Empezar: Espacio".

### Guardado en la web: espejo en localStorage y en el módulo Data del SDK (D-03) · 2026-10-28
- **Qué:** las funciones `FI_Local*` de la jslib usan primero el módulo Data del SDK de CrazyGames y caen a `localStorage`. Sin cambios de C#.
- **Por qué:** el portal guarda la partida de un jugador logueado (la nube de un jugador real no se verificó todavía). El límite del módulo es 1 MB y la partida nueva pesa ~1,3 KB.

### SDK de CrazyGames por `<script>`, sin paquete de Unity (D-03) · 2026-10-28
- **Qué:** SDK JS v3 cargado en el `<head>` por el adaptador `crazygames-adapter.js`; la carpeta que se sube es `Builds/Release/Web-CrazyGames`.
- **Por qué:** no suma peso al build ni pide el OK de un plugin nuevo.

### Nombre del juego: "Pepe's Berenjenas" (DEC-2) · 2026-10-28
- **Qué:** elegido por el usuario; reemplazó a "Jhon Papa's" y "Juan Papa's".
- **Por qué:** esos se parecían a "Papa John's", una cadena conocida, y CrazyGames pide nombres originales. Faltan estudio, disponibilidad (CrazyGames, Play, dominio) y búsqueda en el INPI. Dentro del juego sigue "Farm Idle" hasta que sea definitivo.

### Playtest externo descartado (Q-03) · 2026-10-28
- **Qué:** no hubo prueba con 3 a 5 personas; los hallazgos futuros entran como filas nuevas.
- **Por qué:** pedido del usuario ("no importa, sigamos"); la prueba con gente puede hacerse ya en el portal.

### Fertilizante no farmeable (B-06) · 2026-10-21
- **Qué:** compra con monedas a 1500, tope de 1 en stock y enfriamiento de 180 s; el video llena hasta 3. Todo en `GameConfig`.
- **Por qué:** el playtest Q-02 mostró que comprar por 250 y cosechar 800 al instante en bucle rompía el juego.

### Rewarded siempre opcional, con piso de ~3 minutos entre anuncios (B-03) · 2026-10-26
- **Qué:** ningún anuncio se dispara sin acción del jugador; `adMinIntervalSeconds` = 180 s. Detalle en `REWARDED_REGLAS.md`.
- **Por qué:** es el mismo piso de ~3 min que usa el SDK de CrazyGames; evita sorprender al jugador y problemas con el portal.

### Localización con tabla propia (U-03) · 2026-10-19
- **Qué:** `Resources/Localization.csv` + `Loc`, no el paquete Unity Localization.
- **Por qué:** más liviano para el peso del build (web y APK).

### Escenas generadas por builders (desde la Fase 2)
- **Qué:** Bootstrap y MainLevel salen de `Phase2SceneBuilder.BuildAll`; no se editan a mano.
- **Por qué:** regla del proyecto (`CLAUDE.md`); el motivo de fondo no quedó anotado en su momento. AR-05 reordena los builders por responsabilidad.

### Decoraciones con UN pad en orden (F-17) · 2026-11-04
- **Qué:** un solo pad "Decorar la granja" vende siempre la siguiente de 8 decoraciones (se guarda solo cuántas hay). Va en el sector Almacén, que pasó a tener dos columnas (almacén en x 3,1 y decoraciones en 6,7). Se abre con todas las zonas compradas y sobrevive al prestigio.
- **Por qué:** 8 pads con forma propia (regla `PadShapeTests`) no entraban en la plaza y recargaban el arte; con uno solo el guardado es un entero y se suma una sola forma. El plan aprobado hablaba de pads nuevos: se simplificó.

### Ayudantes fuera del bot y plaza más ancha (F-17 tanda 2) · 2026-11-04
- **Qué:** los ayudantes son un sistema aparte (`HelperManager`), no una mejora de `ProgressionData`, y la plaza se ensanchó 3,3 a cada lado (`PlazaHalfWidth` 8,6 a 11,9) para una tercera columna en el sector Almacén (almacén x 3,1, decoración 6,7, ayudantes 10,3). El oeste queda con espacio libre.
- **Por qué:** como mejora entraría al plan de compras del bot y movería toda la curva de 4 h 20 min y sus pruebas; aparte es una compra de final de juego sin riesgo para el balance. No cabía otro pad en la plaza sin ensancharla.

### Skins con color por bloque de propiedades y un pad que compra o cambia (F-17 tanda 3) · 2026-11-04
- **Qué:** las skins pintan al jugador con un `MaterialPropertyBlock` (`PlayerSkinView`), sin materiales nuevos. Un solo pad en una tercera columna del sector Atributos (oeste, x -10,3) compra la siguiente skin si alcanzan las monedas o, si no, rota entre las que tiene.
- **Por qué:** seis materiales rompían la regla de pocos materiales (`PerformanceBudgetTests`, tope 6 en el mundo); el bloque de propiedades afecta a un solo renderer. Un pad que sirve para las dos cosas evita sumar otro a la plaza; el costo es que parado sin monedas cambia el color (inofensivo).

## J-04 · Cosecha rara sin planta dorada (2026-11-05)
**Que**: el aviso es un texto dorado y un estallido doble; la planta no cambia de color. **Por que**: un color por planta rompe el material unico de paleta (A-05). **Premio**: 3x (no 5x) para no subir tanto el ingreso.

## F-18 · Pedidos del pueblo: se entregan a mano, no con la venta (2026-11-05)
**Qué**: el jugador se para en el cartel y entrega los cultivos de la mochila. **Por qué**: así hay que elegir entre vender y entregar, y se diferencia de las misiones diarias (que cuentan lo que ya se hace). **Tope de semillas**: 2 por temporada desde pedidos grandes, para no duplicar la tienda de prestigio.

## V-02 · Entorno: bosque y prado generados, sin assets nuevos (2026-11-13)
**Qué**: `SceneryBuilder` arma con `PaletteMeshBuilder` un bosque alrededor del área jugable (4 mallas, una por lado), un prado de pasto, flores y piedritas (1 malla) y 4 paredes invisibles en x ±22,5 / z -30,5 a 26,5. **Por qué**: mejora visual sin pack nuevo ni licencia nueva, con el material de paleta único: 5 draw calls como máximo (más la sombra del bosque), sin scripts ni costo por cuadro. Suma 6 colores a la paleta. **Paredes**: antes el jugador podía caminar hasta el borde del suelo; ahora el bosque es el límite.

## F-18 · Tanda 2: tres pads de entrega en vez de un panel (2026-11-13)
**Qué**: el cartel de pedidos es un cartel de madera con tres pads (`OrderPad`, uno por lugar; el tercero es el pedido grande) al oeste de la avenida, entre el campo y la plaza (no adentro: la plaza está llena). El HUD muestra una tarjeta con los pedidos activos (`OrdersHudView`), sin botones. **Por qué**: mismo gesto que el resto del juego (pararse en un pad), sin pantalla nueva que tape el juego. La espera del lugar vacío se muestra de a 5 s para no reescribir el cartel cada segundo.

## J-05 a J-07 y U-09 · Jugador animado, cámara, rebote del HUD y flecha de "conviene ir" (2026-11-13)
**Qué**: (1) `PlayerAnimator` anima al granjero por matemática (rebote, inclinación, ladeo, pasos y manos; sin Animator) y el builder le suma sombrero de paja, manos y pies con primitivas (1 material nuevo, `M_PlayerHat`; manos y pies usan el de la nariz y siguen a la skin). (2) `CameraJuice` mueve la **distancia** del Position Composer (zona nueva: se aleja 1,4 s; nivel: golpecito; corriendo: +1,2), y el campo de visión queda solo para el Turbo, así no se pisan. (3) Todos los botones llevan `ButtonPressFeedback` y las tarjetas modales `PopInOnEnable` (`EditorUi.CreateButton` / `CreateCard`). (4) La flecha del borde (`TutorialEdgeArrow`) sigue apuntando, terminada la guía, al pad de venta con la mochila llena o al pad de compra que ya se puede pagar entero (`IGuidePoint` + `GuidePoints`, en Core), con 3 s de espera y solo si el lugar está fuera de pantalla. **Por qué**: pedido del usuario (más vida visual sin pack de arte ni romper el presupuesto) y que el jugador no quede perdido en el juego medio. `GuidePoints` es una lista estática y no un servicio porque no tiene estado que guardar ni orden de arranque; las decoraciones, skins y ayudantes no se anotan (son sumideros opcionales, la flecha sería ruido). Quedó afuera el polvo en los pies (necesita partículas en la capa Player).

## J-08 · Jugo del circuito cosechar-llenar-vender y sonidos de cosecha propios (2026-11-13)
**Qué**: (1) Combo de cosecha: `AudioManager` sube el tono por una escala pentatónica (`HarvestScale`, 8 notas, ventana de 0,9 s) y `HarvestFeedback` saca el `+1` grande desde la décima seguida. (2) Mochila llena: sonido propio (`SoundCue.BackpackFull`), rebote de la pila entera (`BackpackStackView`, escala del ancla) y el cartel dice "¡Llena!" 1,2 s antes de volver al MAX (`BackpackFullIndicator`). (3) Venta grande (≥ 20 unidades sin cortar, `JuiceRules`): el `+N` total remata en grande (`SellFeedback`) y suena la bocina (`SoundCue.TruckHorn`). (4) Los sonidos de cosecha pasan de los pluck/drop de Kenney a una marimba suave generada (`Tools/sfx_gen.py`), a pedido del usuario ("más agradables"). **Por qué**: cosechar es la mitad del tiempo de juego y sonaba siempre igual; llenar la mochila se comunicaba como error. **Decisiones**: la escala es pentatónica y las tres variantes tienen la misma nota porque subir de a pasos iguales de frecuencia (como la venta) con variantes de tono distinto desafina. La bocina suena cuando TERMINA la venta grande, no cuando el camión se va: el audio vive en Managers y no puede ver `SellTruck` (Environment); lo cuenta él mismo con `StreakTracker` sobre `ProductSold`. Los `SoundCue` nuevos van al final del enum (el banco guarda el índice) y el banco se rehace con `Phase9SceneBuilder.RebuildAudio` (`BuildAll` no pisa un banco existente). Sin premio, sin tocar balance ni guardado; quedó afuera el golpecito de cámara de IM-01.
