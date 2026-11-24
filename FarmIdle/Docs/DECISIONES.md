# Registro de decisiones

Roadmap AR-06. Una entrada corta por decisión que no se deduce leyendo el código: **qué**, **por qué** y **cuándo** (con el ID del roadmap). Lo más nuevo, arriba. Una decisión que se revierte no se borra: se agrega una entrada nueva que la reemplaza.

### Tema visual de la UI: `UiTheme` (V-03) · 2026-11-24
- **Qué:** los colores de paneles y botones salen de `Editor/UiTheme.cs` (tarjeta verde bosque con borde dorado, botones dorado/verde hoja/madera/gris verdoso/tomate, letra crema). El azul genérico de Unity (0,30/0,50/0,78) desapareció de `HudBuilder`. Botones con sombra inferior (relieve) y tarjetas con borde + sombra, todo con `Outline`/`Shadow` de uGUI (vértices, sin draw calls ni materiales nuevos).
- **Por qué:** el azul desentonaba con la paleta de granja y los colores estaban repetidos a mano en cada builder; con un tema único el estilo se cambia en un solo archivo.
- **Segunda tanda:** misiones, logros, premio diario, pedidos, nivel y contador de monedas también usan el tema (`Pill`, `Row`, `BarBack`, `BarFill`, `Gold`, `Reward`, `BodyText`). Quedan fuera a propósito: botones de debug, colores de buffs del HUD, el velo del anuncio y los colores de skins (`SkinLooks`), que son diseño de juego y no de UI.
- **Tercera tanda (2026-11-24):** esquinas redondeadas. `UiTheme.RoundedSprite()` genera `Assets/Sprites/UI/rounded.png` (96 px, radio 36, 9-slice de 40, blanco con alfa) y `EditorUi.CreatePanel` lo usa para todo menos los velos de pantalla completa (`*Backdrop`, `AdOverlay`, `Menu`), que siguen con el sprite recto de Unity porque con esquinas redondas dejarían huecos en los bordes. Perilla de los deslizadores dorada.
- **Herramienta:** `UiPreviewRenderer.Render` (batch, `FI_OUT`) pasa los Canvas a "Screen Space - Camera" en memoria y saca PNG del mundo, del HUD, de "Tocá para jugar" y de cada panel `*Backdrop`. Ojo: en edición los textos que se llenan en juego salen vacíos (misiones, tienda, premios).
- **Pendiente:** verlo en un dispositivo.

### Pasto con variación (V-06) · 2026-11-24
- **Qué:** `Phase1SceneBuilder.CreateCheckerTexture` genera `T_GroundChecker.png` de 32×32 (antes 2×2): cada texel sigue la paridad del damero pero 20 % sale más claro y 14 % más oscuro, con un generador congruencial de semilla fija (la textura es idéntica en cada `BuildAll`). El tiling del material pasa a `50 / 32` para conservar los cuadros de 2 unidades y su alineación con parcelas y pads.
- **Por qué:** el damero de dos tonos se leía como tablero; la variación da pasto sin sumar materiales, texturas extra ni draw calls. Se repite cada 64 unidades, tapado por la niebla.

### Camino de arena y plaza adoquinada (V-07) · 2026-11-24
- **Qué:** en `PlazaBuilder.BuildPlazaDecor` la avenida y las rutas de sector usan arena (`SceneryBuilder.RoadSand`) en vez de `SoilLight` (que con la luz se veía marrón oscuro y pesaba como una "T" gigante); la avenida suma un borde más oscuro y 46 piedritas con semilla fija. El piso de la plaza es una base clara (juntas) con baldosas de 2×2 más oscuras en damero, igual que el pasto.
- **Paleta:** 4 colores nuevos (`RoadSand`, `RoadEdge`, `PaverLight`, `PaverDark`) registrados al final de `SceneryBuilder.Prepare`, así los índices anteriores no se mueven. Sigue siendo una malla y un material (la plaza suma ~80 cajas, sin draw calls nuevos).

### Cultivo del día y camión mejorable (F-25 = IN-02, F-26) · 2026-11-24
- **Cuál sale:** `DailyCropRules.Pick` baraja TODOS los cultivos de `GameConfig.OrderCrops` con una semilla mezclada del `DayKey` y gana el primero cuya zona está abierta. Así cada cultivo abierto sale con la misma probabilidad (no sale siempre el más caro; el peso de `OrderCropRule` no se usa, porque crece con el precio) y no hace falta guardar nada. Abrir una zona durante el día solo lo cambia si el cultivo nuevo cae antes en el orden del día.
- **Cuánto:** +25 % (`DailyCropBonus`). Con +50 % el bot terminaba el juego ~20 % antes y rompía el plan de ritmo; el usuario eligió bajar a +25 % y correr los minutos del `EconomyPlan` desde la compra 18 (143→122, 166→140,5, 190→159, 214→179, 238→199, 262→219, 280→234,5, 303→252). Pendiente recalibrar costos con datos reales (B-05).
- **Dónde se cobra:** `PriceCalculator.FinalPrice(base, cropId)`; lo usan `EconomyManager` (venta y cosecha rara) y `OrdersManager.RewardCoins`. Las pruebas que cuentan monedas apagan el extra (`Pricing.SetDailyCrop("", 0)`), igual que `RareRoll`.
- **Bot de balance:** `BalanceBot.CreditDailyCrop` acredita el valor esperado (bonus / cultivos abiertos) sobre cada venta; sin fechas.
- **Camión:** `TruckManager` guarda cuántas mejoras tiene (`truck`), se compran en orden (`GameConfig.TruckSkins`: 4.000, 30.000, 120.000, PROVISORIOS) y no dan ventaja. `SellTruck` cambia la malla (`SM_SellTruck`, `_1`, `_2`, `_3`; mismo material de paleta). El pad va en la tercera columna de Atributos, fila 1 (libre), junto al de skins.

### Perk de Suerte y una granja nueva por temporada (F-24) · 2026-11-24
- **Cuándo aparece la temporada nueva:** nunca sola. El botón «Reiniciar con bonus» aparece en el HUD cuando está todo comprado (`ProgressionManager.IsEverythingBought`: todas las zonas abiertas y todas las mejoras al máximo; los perks no cuentan) y reiniciar pide dos toques. No hay fecha ni cuenta regresiva: depende solo de lo que compró el jugador.
- **Suerte (perk):** tercer perk, sube gastando los puntos de nivel como Fuerza y Velocidad. Cada elección suma 0,5 puntos a la probabilidad de cosecha dorada (J-04); con 10 elecciones pasa de 3 % a 8 % (la dorada paga 3 veces el precio). `PerkKind.RareChance` → `ProgressionModifiers.RareChanceBonus` → `EconomyManager.SetRareChanceBonus`. Si `RareHarvestChance` está en 0 en el `GameConfig`, la dorada sigue apagada. Pad en la tercera columna del sector Puntos (x 10,3). PROVISORIO (B-05): con el bot no se mide porque no elige perks.
- **Granja de temporada:** `ZoneData.RequiredSeason` (0 = desde el principio). Una granja con temporada mayor que la actual no se ve (ni terreno ni portón), no se puede pagar (`ProgressionManager.IsZoneAvailable`/`TryPay`) y no cuenta para `IsEverythingBought`. Al empezar la temporada N aparece la de N, que hay que comprar para poder cerrar esa temporada: así cada temporada tiene una meta más. Tres granjas: Campo del Alba (temporada 1, 150.000), Quinta del Roble (2, 260.000), Estancia Dorada (3, 400.000), de girasol; costos PROVISORIOS (B-05). Después de la temporada 3 no hay más granjas nuevas.
- **Mapa:** tercera fila en z 31 (x -11, 0, 11); el mundo crece 12 al norte (`SceneryBuilder.NorthZ` 26,5 → 38,5), el bosque del norte se corre. El portón del Campo del Alba queda en z 25, junto al campo del medio de la fila anterior (hay que abrirlo antes); los de los costados, en el Alba. La bandera pasa a (-11, 25), en el pasillo entre la segunda y la tercera fila. Antes de la temporada 1 esa zona del mapa está vacía.
- **Bot de balance:** ignora las granjas de temporada (`BalanceBot.IsPlayed`), así la calibración de la temporada 0 no cambia.

### Las decoraciones van alrededor de las granjas, no de la plaza (F-23) · 2026-11-24
- **Qué:** las 8 decoraciones estaban juntas en el césped a los lados de la plaza (z -12 a -23). Ahora se reparten por el mapa de las granjas, en el terreno que queda libre entre campos y valla: entrada de la granja inicial (farol y espantapájaros, z 1,5, a ±4,6), costado este (banco x 17,6 z 7; molino x 18,3 z 19,5), costado oeste (estanque x -18,4 z 7; fardo x -19,2 z 19,5), pasillo entre filas (flores x -11 z 13) y borde norte (bandera z 24,8).
- **Por qué:** el pedido del usuario fue que se vean por todo el mapa y con sentido. Las dos primeras (las más baratas, que se compran con una sola zona abierta) quedan donde el jugador pasa más; el resto da la vuelta por los costados y el fondo a medida que se abren zonas.
- **Cómo se verificó:** un script temporal de editor midió la huella de cada decoración (aspas incluidas) contra todas las mallas de la escena: la primera pasada mostró el molino rozando la losa bloqueada de la Quinta del Nordeste (aspas, a 3 m de altura) y se corrió 0,5 m. Sin choques en la segunda. Queda por ver en pantalla.
- **Sin cambios de lógica:** solo las posiciones del `DecorationBuilder` y la escena regenerada. La flecha de guía de F-20 ya apunta a la decoración nueva esté donde esté.

### El reinicio de temporada se explica mejor y la tienda de semillas ocupa menos (F-03/F-11) · 2026-11-23
- **Qué:** un tester volvió al juego, vio el botón «¡Nueva temporada!», lo tocó y se reiniciaron las granjas sin entender por qué. El botón pasa a «Reiniciar con bonus»; la tarjeta ahora tiene tres bloques (SE REINICIA / TE QUEDA / GANÁS), aclara que es opcional, y los botones dicen «¡Reiniciar!» y «Cancelar». «Perk» no se muestra más al jugador: la plaza los llama «atributos» (Fuerza y Velocidad), y el aviso del HUD dice «mejorá un atributo».
- **Tienda de semillas:** la tarjeta ocupaba ~1720 de 1920 px; ahora ~1165 (filas de 2 líneas, 6 filas de 120 px) y el fondo oscuro baja de 0,70 a 0,40 de opacidad.
- **Sin cambios de lógica:** el reinicio nunca es automático ni depende de los días sin abrir; sigue pidiendo dos toques (botón + «¡Reiniciar!»). Solo textos (`Localization.csv`) y `HudBuilder`.

### Festejo al comprar una decoración (F-22) · 2026-11-23
- **Qué:** al comprar una decoración: estallido dorado con el doble de trocitos (`BurstFeedback`), cartel dorado grande "+2 % ventas" sobre ella (`HarvestFeedback`), sonido de perk (`AudioManager`) y vibración de perk (`HapticsManager`). `DecorationSet.Placed(posición, encanto %)` es el aviso (estático, como `PurchasePad.PurchaseCompleted`); cargar una partida no lo dispara. Antes comprar una decoración era silencioso.
- **No se hizo "cosecha dorada":** ya existe como cosecha rara (J-04: `RareHarvestChance`, premio dorado con estallido doble). Si se quiere más, se ajusta ese número en `GameConfig` (toca balance: con el bot).
- **Pendiente a propósito:** nada de partículas ni luces nuevas (presupuesto de rendimiento); un destello de pantalla (`ScreenFlashView`) quedó afuera por ser más invasivo.

### Decoraciones con vida y con encanto (F-21) · 2026-11-21
- **Qué:** (1) cada decoración da **encanto** (`DecorationData.Charm`, +2 %): multiplicador permanente del precio de venta (`PriceCalculator.CharmMultiplier`, lo fija `DecorationManager` en cada compra o carga; también cuenta en las monedas offline). Las ocho: **+16 %**. Revierte lo de F-17 ("no da ninguna ventaja"). (2) Las decoraciones se mueven (`DecorationMotion`: gira, se mece, orbita, late, salta): aspas del molino, tela de la bandera, espantapájaros y flores al viento, luciérnagas en el farol, mariposas en las flores, patos en el estanque, pajarito en el banco, gallina en el fardo. (3) El pad muestra "+2 % ventas" junto al nombre.
- **Por qué:** el usuario dijo que las decoraciones eran poca cosa para lo que cuestan (pedido 2026-11-21). Un sumidero de 500.000 monedas que además hace producir más da una razón para seguir farmeando; ver una granja viva da otra.
- **Cómo se ajusta:** `Charm` está en cada asset (0 lo apaga). El +16 % es chico a propósito: no toca los muros de balance; subirlo es cambiar un número.
- **Rendimiento:** los bichos y piezas móviles son mallas sueltas con el MISMO material de paleta, hijas de la decoración (apagadas hasta comprarla); sin partículas, sin `Instantiate`, sin asignaciones por cuadro (solo `Vector3`/`Quaternion`). Con las ocho compradas suman unos 14 renderers más.

### Lo sembrado se guarda: crece al volver y se pudre a las 24 h (B-11) · 2026-11-16
- **Qué:** servicio nuevo `PlotStateManager` (sección de guardado `plots`, volátil). Cada `PlotField` es un `IPlotStateHolder` (Core) y pide su foto en `Start`. Al volver, lo sembrado crece el tiempo real que se estuvo fuera (por la velocidad de crecimiento de hoy); con 24 h o más (`GameConfig.OfflineRotSeconds`) las parcelas vuelven vacías. En el celular, al volver de segundo plano pasa lo mismo sin recargar.
- **Decidido por el usuario:** se pudre **solo lo sembrado**; las monedas del tiempo fuera se siguen cobrando (ya tienen tope de horas). Y las monedas al volver eran demasiadas: eficiencia **25 % → 15 %** y tope **25.000 → 15.000 por hora** (con 2 h: 30.000; con el Almacén al máximo, 60.000). Números provisorios.
- **Por qué así:** el estado de las parcelas vive en Environment (`PlotLogic`), no en un manager; en vez de moverlo se guarda una foto por campo, identificada por `padre/nombre` del objeto (único en el nivel: lo vigila una prueba). Reloj local: si la hora retrocede, lo sembrado vuelve tal cual.
- **Límites:** el aviso ("Lo que sembraste ya creció" / "se pudrió") sale en la tarjeta de bienvenida, que solo aparece si hay monedas para cobrar. Un campo de una zona que hoy no está abierta conserva su foto en la partida.
- **Para qué sirve el Almacén** (pregunta del usuario): sube el tope de horas que cuentan para las monedas al volver, de 2 h a 3 h y 4 h. No guarda cosecha ni tiene que ver con las 24 h.

### Decoración comprada que se nota (F-20) y contraste de carteles (U-12) · 2026-11-16
- **F-20:** `DecorationSet` es un `IGuidePoint` con prioridad nueva `GuidePoints.NewItemPriority` (3, gana a vender y comprar): 12 s después de comprar, la flecha del borde y su cartel señalan la decoración, que aparece con un salto. En la escena van a ×1,6. **No se cambiaron los modelos**: mejorarlos es trabajo del pack de arte (A-06); esto solo hace que se vean y se encuentren.
- **U-12:** todos los carteles del mundo usan el material con borde que trae TextMeshPro (`LiberationSans SDF - Outline`, un solo material compartido: no suma draw calls entre carteles) y un amarillo más claro; el gris `#9A9A82` de las líneas secundarias pasó a `#DCDCC8`.

### Tanda de pedidos del usuario: cartel de la guía, control fijo, espera del pad, premio de los pedidos · 2026-11-15
- **Cartel de la guía (U-10):** la flecha ahora dice qué hacer. Un solo cartel del HUD (`TutorialEdgeArrow`): junto a la flecha del borde o, con el lugar a la vista, encima de él (4,2 unidades de mundo arriba, por encima de la flecha que flota). Solo se ve jugando. El texto sale del paso de la guía o, después, de la prioridad del `IGuidePoint` (vender / comprar): no se agregó nada a la interfaz.
- **Control fijo (U-11):** ajuste nuevo `SettingsManager.FixedControls` (guardado `settings`, sin subir de versión: un guardado viejo queda en flotante). `FloatingJoystick` lo lee en cada cuadro; en fijo queda abajo al centro con cuatro flechas y la dirección se mide desde su centro, se toque donde se toque. Se eligió un joystick fijo y no cuatro botones sueltos: mantiene el movimiento en cualquier dirección.
- **Espera del pad (B-10):** `purchaseStartDelaySeconds` pasó de 0,7 a **1,0 s** (pedido: "pararse un poco más"). Con 1,2 s el `BalanceBot` compraba "Crecimiento 6" en el minuto 202,2 contra un límite de 202,3 (238 − 15 %) y la prueba de balance fallaba. **Dato a mirar:** el bot ya viene ~12 % adelantado respecto del `EconomyPlan` en las últimas compras (Crecimiento 6 en el minuto 210 contra 238 del plan); la tabla de `ECONOMIA_V1.md` quedó vieja y conviene recalibrar (B-05).
- **Pedidos (F-18):** el premio no cambió (venta +25 %, el grande +40 % y 1 semilla). El usuario eligió solo mostrarlo: la tarjeta del HUD lo pone en cada línea y al completar sale un aviso con lo ganado. El premio de la tarjeta se calcula al último cambio del cartel.
- **Skins con plata real:** el usuario las quiere para el lanzamiento, no ahora. Anotado en M-06; hace falta un SDK de pagos (OK previo) y en CrazyGames las compras son solo por invitación (a verificar).

### Los ayudantes pagan por tandas (F-19) · 2026-11-15
- **Qué:** `HelperManager` junta lo que rinden los ayudantes y lo entrega de una cada `GameConfig.HelperPayoutSeconds` (5 s; evento `Paid`). El ingreso total no cambia. Lo juntado y no entregado (hasta 5 s) se pierde al cerrar o al empezar una temporada.
- **Por qué:** pedido del usuario. Acreditar moneda a moneda cambiaba el saldo casi en cada cuadro, y `CoinCounterView` reinicia su conteo y su latido con cada cambio: el contador quedaba "tildado" (y arrancaba corrutinas en cada cuadro).
- **Pendiente:** la tanda no está sincronizada con el peón que descarga en el camión ni muestra un "+N" en el mundo; el aviso es el latido del contador.

### Correo de contacto público (M-04) · 2026-11-15
- **Qué:** el contacto de la política de privacidad (`Legal/index.html`) es `alternativaflay@gmail.com`, dado por el usuario para publicarse.

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

## F-19 · Ayudantes visibles: un peón por nivel, adorno puro (2026-11-13)
**Qué**: `HelperCrew` (Environment) prende un peón por cada nivel de `HelperManager` escuchando `Changed` (compra, carga de partida y temporada nueva usan el mismo camino) y `HelperWalker` lo hace dar vueltas por un circuito fijo: carga una pila al borde del campo inicial y la deja a mitad de camino. Los arma `HelperCrewBuilder` (5 mallas de paleta: 3 cuerpos, pie y pila), apagados en la escena. **Por qué**: era la compra más cara y la única que no se veía (IDEAS D5). **Decisiones**: (1) No cosechan ni venden: el ingreso sigue saliendo de la fórmula, así el bot de balance y la economía no cambian. (2) Sin colliders y por el césped libre: no pisan parcelas ni pads ni frenan al jugador; una prueba recorre cada circuito con `Physics.OverlapSphere` y falla si algo nuevo del mapa queda en el camino. (3) El segundo peón descarga en el **puesto de verduras** y no en el camión (el roadmap decía "camión"): tres peones del lado este se cruzaban, y así hay movimiento a los dos lados del pad de venta. (4) El tercer nivel se llama "Cosechadora" pero se ve como otro peón (overol verde): una máquina era otra malla y otra animación. (5) Tiempo de juego (se frenan con la pausa), a diferencia del camión. Cuando el camión se va, los peones siguen descargando en su lugar: se aceptó por ser adorno. Costo: hasta 4 renderers por peón (12 con los tres), todos `M_Palette`; solo el cuerpo da sombra.

## Q-06 · Calor en iPhone (WebGL): resolución y FPS limitados (2026-11-21)
**Qué**: (1) `index.html` pasa `devicePixelRatio` a Unity con tope 2 (antes usaba el nativo: un iPhone con DPR 3 renderizaba ~1170x2532 px) y, en pantallas táctiles, limita el juego a 30 FPS salteando cuadros de `requestAnimationFrame`; en PC sigue a 60. Se prueba sin recompilar con `?dpr=1.5&fps=45` en la URL. (2) `Mobile_RPAsset`: HDR apagado (no hay postproceso en la cámara; el HDR obligaba a un buffer intermedio de 16 bits). **Por qué**: tres personas con iPhone jugaron mucho rato y el teléfono se calentó; la causa más probable es el relleno de píxeles (GPU) a 60 FPS, no el código (sin asignaciones por cuadro, un material). **Pendiente de medir**: no se midió en un iPhone; si sigue caliente, bajar `fps` o `dpr` por defecto, achicar sombras (`m_ShadowDistance` 50) o `m_RenderScale` 0,8 (compartido con Android, ojo).

## F-27 · Sombreros y diseños en las skins; perks Regateo y Mano verde en una segunda fila (2026-11-24)
**Qué**: (1) Una skin ya no es solo un color: `SkinData` suma sombrero (`SkinHat`), diseño (`SkinPattern`) y dos colores (sombrero y detalle). Las piezas son grupos apagados bajo el cuerpo del jugador (`Hat_*`, `Pat_*`) que `PlayerSkinView` prende y pinta con `MaterialPropertyBlock` al cambiar de skin: sin `Instantiate`, sin materiales nuevos, nada por cuadro. (2) Los perks nuevos NO usan un panel de elección: se eligió una segunda fila de pads en el sector Puntos (el sector crece 3,8 al sur; `PlazaSouthZ` -27 a -30,8, `EastDividerZ` -22 a -25,8, `StoreRowZ` -25 a -28,8; el mundo (`SceneryBuilder.SouthZ`) pasa de -30,5 a -34,5). **Por qué**: mantiene la regla "las compras viven en los pads" y no agrega UI; cuesta un poco de bosque al sur. Regateo y Mano verde son un multiplicador APARTE (+1 % por elección, 10 elecciones, +10 % al máximo): no entra en los topes de la tienda de prestigio (`PrestigeShopTests`) ni en el de las mejoras. Suerte ya existía (F-24, +0,5 puntos): no se tocó. **Pendiente**: valores PROVISORIOS (B-05); no se verificó a ojo el aspecto de los sombreros en pantalla.

## F-27b · Espera de 1,6 s en los pads de perk y pedidos plegables (2026-11-24)
**Qué**: `PerkPad.startDelaySeconds` 0,35 a 1,6 s con el pad creciendo mientras carga (`chargeScale`); la segunda fila de perks queda a 1,2 de la primera y para llegar hay que cruzar los pads de adelante, así que la espera evita gastar puntos de casualidad (no se agregó un pasillo ni otra puerta). `OrdersHudView` plegable por defecto. **Por qué**: pedido del usuario tras probar el preview de CrazyGames. **Pendiente**: si 1,6 s se siente largo o corto en el celular, es un solo valor.

## V-05 · La web arranca sola, sin "Tap to Play" (2026-11-24)
**Qué**: `TapToPlayOverlay.AutoStart` (true en WebGL, false en editor y Android): al llegar a `Ready` el juego pasa solo a `Playing` un cuadro después, el overlay no se muestra en `Ready` y no pide el toque. Como el navegador no deja sonar audio sin un gesto, al primer toque o tecla `AudioManager.RestartMusicAfterGesture()` reinicia la música.
**Por qué**: el portal mide la carga hasta el primer `gameplayStart`, y el toque lo atrasaba lo que tardara el jugador (el usuario midió hasta 40 s sin tocar).
**Riesgo / a verificar en el build web**: si la música entra callada hasta el primer gesto (esperado) o a tiempo; si el portal acepta empezar sin clic. Para volver al toque, `AutoStart` en false.
