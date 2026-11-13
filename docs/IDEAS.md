# Lluvia de ideas — Pepe's Berenjena (Farm Idle)

Fecha: 2026-11-13 · Solo lectura del proyecto: no se tocó código ni se corrieron pruebas.
Fuentes: `Assets/_Scripts` (Data, Managers, Environment, Player, UI, builders), `ROADMAP.md`, `Docs/ECONOMIA_V1.md`, `Docs/ARQUITECTURA.md`, `Docs/DECISIONES.md`, `docs/AUDITORIA.md`.
IDs: `IM-xx` = mejora a algo que existe · `IN-xx` = funcionalidad nueva. Esfuerzo con la escala del roadmap (S = media sesión, M = una sesión con pruebas, L = varias: partir).
Los números de balance de este documento son **propuestas a calibrar**, no mediciones.

---

## 1. Resumen

### El juego en 5 líneas
1. Arcade idle vertical: un granjero se mueve con joystick flotante (o teclado) y **todo se hace pisando**: pisar una parcela vacía siembra, pisar una lista cosecha, pisar la zona de venta vende, quedarse parado en un pad compra (las monedas "gotean").
2. Lo cosechado se apila en la espalda (mochila de 10 a 30 lugares) y hay que llevarlo a vender; el ritmo real del juego es **caminar entre campos y la plaza**.
3. Con monedas se abren 5 zonas (una por cultivo nuevo, 8 cultivos del trigo de 18 s al girasol de 680 s) y se suben 5 mejoras; vender da XP, y cada nivel (hasta 20) da un punto de perk.
4. Al comprar todo (~5 h de jugador real, según el bot) se reinicia con "Nueva temporada": +25 % de precio permanente por temporada y semillas doradas para una tienda de 6 mejoras permanentes.
5. Alrededor hay una capa de retención y monetización solo con video opcional: offline, misiones, logros, racha diaria, pedidos del pueblo, Turbo y fertilizante.

### Funcionalidades actuales
| Bloque | Qué hay hoy (dónde) |
|---|---|
| Núcleo | Sembrar/cosechar pisando (`PlotLogic`, `PlotView`, `PlotField`), mochila apilada (`BackpackManager`, `BackpackStackView`), venta con camión de adorno (`SellZone`, `SellTruck`, `SalePacer`) |
| Contenido | 8 cultivos, granja inicial + 5 zonas (`ProgressionData`, `ZoneController`) |
| Progresión | Mejoras: Mochila ×3, Velocidad ×3, Crecimiento ×6, Precio ×6, Almacén ×2 (`UpgradeData`); nivel 1 a 20 con 2 perks, Fuerza y Velocidad, 10 elecciones cada uno (`PerkData`, `LevelCurve`) |
| Prestigio | Nueva temporada (+25 % por temporada) y tienda de semillas doradas, 6 mejoras × 3 niveles, 32 semillas en total (`PrestigeManager`) |
| Azar | Cosecha rara: 3 % de chance, paga ×3 al instante (`GameConfig.rareHarvest*`) |
| Pedidos | Cartel con 3 pedidos (uno grande que da semilla dorada, máx. 2 por temporada), entrega a mano en 3 pads (`OrderBoard`, `OrdersManager`, `OrderPad`) |
| Video opcional | Turbo (pad fijo + símbolo flotante fuera de la web), fertilizante (video o monedas con precio creciente), duplicar lo ganado offline (`AdsManager`, `BuffManager`, `FertilizerManager`, `OfferManager`) |
| Retención | Offline (25 % de eficiencia, tope 2 a 4 h y 25.000 monedas por hora), 3 misiones diarias (8 plantillas, 4 tipos), 18 logros (11 tipos), racha diaria de 7 días |
| Sumideros | 8 decoraciones (500 k), 3 ayudantes (850 k, ingreso pasivo +10/20/35 %), 6 skins de color (700 k) |
| Guía y UX | Tutorial de flechas, flecha "conviene ir", ajustes, ES/EN, texto escalable, pads distinguibles por forma |
| Jugo | `+N` y estallidos al cosechar/vender/comprar, squash/stretch, granjero animado por matemática, cámara con jugo, combo de tono al vender, vibración |
| Infraestructura | Guardado versionado con respaldo, pools, analítica por interfaz (`IAnalytics`), anuncios por interfaz (`IAdService`), bot de balance |

### Diagnóstico de diseño (lo que condiciona todas las ideas)
- **D1 · Un solo verbo y cero decisiones.** Cada zona tiene un cultivo fijo y la mejor jugada siempre es "dar la vuelta y vender". Los pedidos son lo único que hace elegir qué cosechar. Las ideas de variedad que valen son las que agregan una decisión sin agregar un sistema.
- **D2 · El prestigio repite las mismas 5 horas.** Las temporadas solo cambian un multiplicador. La tienda de semillas se completa en la temporada 9 (~45 h): es mucho pedir que alguien haga 9 vueltas idénticas.
- **D3 · Tres progresos que se mueren.** La XP no sirve después del nivel 20 (minuto ~182, y el nivel no se reinicia con la temporada); las semillas no sirven con la tienda completa; las monedas no sirven después de 2,05 M de sumideros.
- **D4 · El ingreso del final lo limita caminar** (el bot camina el 80 % del tiempo desde la hora 3). Las mejoras dejan de sentirse; nada del juego ataca ese límite salvo el Turbo.
- **D5 · Los ayudantes son un número.** Se pagan 850.000 monedas por algo que no se ve en el mundo (no hay ninguna vista de ayudante en `Environment` ni en `Player`, solo el pad). Es la compra más cara del juego y la menos satisfactoria.
- **D6 · El offline queda chico al final.** 25.000 por hora contra ~360.000 por hora jugando: volver al día siguiente da el 7 % de una hora de juego. El motivo para volver se diluye justo cuando el jugador ya está enganchado.
- **D7 · El momento manda.** El código está listo para publicar y el cuello de botella es medir (auditoría H-2). Casi todo lo de este documento debería elegirse **con datos del portal**; antes de publicar solo se justifican las victorias rápidas que no mueven el balance.

---

## 2. Mejoras a lo que ya existe

| ID | Sobre | Mejora | Por qué | Esf. | Reutiliza | Riesgos |
|---|---|---|---|---|---|---|
| IM-01 | Cosecha | **Combo de cosecha**: el tono sube con cada parcela cosechada seguida (como ya pasa al vender) y a las 10 seguidas el `+N` crece y hay un golpecito de cámara. Solo sensación, sin premio. | Cosechar es el 50 % del tiempo de juego y hoy suena siempre igual; la venta ya tiene su "ding ding ding" y se nota la diferencia. | S | `ComboCounter` (`SoundThrottle.cs`), `AudioManager.Play(cue, pitch)`, `HarvestFeedback`, `CameraJuice` | Si suma premio rompe el bot: dejarlo cosmético. Cuidar el límite de voces de audio (10). |
| IM-02 | Cosecha rara | **Anticipación y garantía**: (a) contador invisible que asegura una rara cada N cosechas sin suerte (N ≈ 60), (b) sonido propio (hoy no hay `SoundCue` para la rara) y destello de pantalla dorado. | Un 3 % sin garantía produce rachas largas de nada; el premio existe pero no se "festeja" distinto. | S | `GameConfig.rareHarvest*`, azar inyectable de J-04, `ScreenFlashView`, `AudioBank` (cue nuevo) | La garantía cambia el valor esperado (+9 % hoy): recalcular `BalanceBot.CreditRareHarvest`. La planta dorada visible ya se descartó (DECISIONES J-04): no reabrir. |
| IM-03 | Mochila llena | **Que llenarla sea un logro, no un freno**: al llegar al tope, rebote grande de la pila, sonido propio y un cartel breve "¡Llena!"; hoy solo hay un indicador y el sonido `Denied` al intentar cosechar de más. | El momento más repetido del juego hoy se comunica como error. | S | `BackpackFullIndicator`, `SquashStretch`, `BackpackStackView` | Ninguno de balance. Texto nuevo en `Localization.csv`. |
| IM-04 | Venta | **Venta grande**: si se vende la mochila entera de una (≥ 20 unidades sin cortar), un `+N` total grande al final y bocina del camión al irse. | La venta ya está bien; le falta el remate. El camión se va en silencio. | S | `SellFeedback`, `SellTruck` (fases ya existentes), `SalePacer` | Ninguno. |
| IM-05 | Pads de compra | **"Te falta X" y cuánto falta en tiempo**: el cartel del pad más barato que no alcanza muestra una barrita de cuánto del precio ya se tiene. | Da un objetivo visible entre compras; los huecos del final son de 20 a 25 min sin ninguna referencia. | S-M | `PurchasePad`, `PadBuilder` (barra de pago ya existe), `IReadOnlyWallet.Changed` | Actualizar textos por evento, no por cuadro (presupuesto de GC). Un material de paleta. |
| IM-06 | Perks de nivel | **Más tipos de perk** (ver IN-04). Hoy hay 19 puntos para 20 elecciones posibles: no se elige nada, se compra casi todo. | La pantalla de "elegí un perk" es la única decisión de build del juego y hoy es falsa. | M | Ver IN-04 | Ver IN-04 |
| IM-07 | Prestigio | **Resumen de temporada**: antes de reiniciar, una tarjeta con monedas ganadas, cosechas, pedidos entregados y semillas que se llevan. Después del reinicio, las monedas iniciales caen como lluvia. | El reinicio es el clímax del juego y hoy es un botón. Además deja claro qué se conserva (miedo clásico a perder todo). | S-M | `SeasonPanelView`, contadores de `AchievementManager`, `PopInOnEnable`, `SellFeedback` (monedas) | Si los contadores son acumulados de toda la partida hay que guardar el valor al empezar la temporada (campo nuevo en un `ISaveable`). |
| IM-08 | Tienda de semillas | **Una mejora sin tope** al final de la lista (ver IN-09). | Las semillas mueren en la temporada 9 (D3). | S | Ver IN-09 | Ver IN-09 |
| IM-09 | Pedidos | **Pedido con reloj** opcional: uno de los 3 lugares normales sale a veces "urgente" (5 min, +50 % en vez de +25 %). Y un tic sonoro al completar cada entrega parcial. | Es el único sistema que hace decidir; un reloj le da tensión sin castigo (si vence, se cambia por otro). | M | `OrderBoard` (lógica pura), `OrdersManager`, `OrderPad`, `OfferTimer` como modelo de reloj | Reloj en tiempo de juego, no real (si no, la pausa y los anuncios lo comen). Recalibrar `OrdersDeliveredShare` del bot. |
| IM-10 | Fertilizante | **Mostrar lo que vale**: al usarlo, un `+N` con la cantidad de parcelas que terminó, y en el pad de compra el número de parcelas creciendo ahora ("rinde 14"). | Hoy el jugador no sabe si conviene usarlo ya o esperar; es la compra más cara por uso (hasta 28.350). | S | `FertilizerManager` (ya cuenta parcelas para la analítica), `FertilizerUsePad`, `HarvestFeedback` | Texto en mundo que cambia seguido: actualizar por evento de `PlotCensus`, con tope de frecuencia. |
| IM-11 | Turbo | **Aviso de que se termina**: últimos 5 s con parpadeo de la estela y sonido de cierre; hoy arranca con sonido (`Turbo`) pero se apaga sin aviso. | Un buff que termina en silencio se siente como que "se rompió". | S | `BuffTracker` (segundos restantes), `TurboTrail`, `TurboCameraEffect`, `RewardHudView` | Ninguno. |
| IM-12 | Offline | **Tope de monedas que acompaña al progreso**: `offlineMaxCoinsPerHour` multiplicado por el prestigio (o por zonas abiertas), en vez de fijo. | D6. El tope se pidió por un caso real (B-07, volver con 150.000), así que no se saca: se escala. | S | `OfflineMath`, `GameConfig`, `PrestigeManager` | Es decisión de balance ya tomada por el usuario: confirmar antes. Actualizar `ECONOMIA_V1.md` y las pruebas de la cuenta. |
| IM-13 | Misiones | **Más variedad de plantillas**: hay 8 plantillas y 4 tipos (cosechar, vender, fertilizar, comprar). Agregar: entregar pedidos, cosechas raras, ganar X monedas, cosechar un cultivo puntual, usar el Turbo. | Con 3 por día de un pozo de 8, al tercer día ya se vieron todas. `Buy` además puede ser imposible con el juego avanzado (a verificar cómo lo filtra `MissionRules`). | S-M | `MissionKind`, `MissionRules`, `MissionManager` (ya escucha eventos), `OrdersManager`, `PlotView.RareHarvested` | Ids nuevos en el guardado (el manager ya tolera ids desconocidos). El tipo "cultivo puntual" tiene que salir solo de zonas abiertas, como `OrderCropRule`. |
| IM-14 | Logros | **Logros para lo nuevo**: no hay ninguno de pedidos, cosecha rara, decoraciones, skins, ayudantes ni racha diaria. | Los sistemas agregados después de F-06 no tienen meta larga; es contenido casi gratis. | S | `AchievementKind`, `AchievementManager`, builder de datos | Sube la cuenta de logros que revisan las pruebas de datos y el panel (¿entra en la tarjeta? revisar el scroll de `AchievementPanelView`). |
| IM-15 | Racha diaria | **Semana 2 en adelante**: al cerrar la racha de 7, la siguiente vuelta da +1 semilla dorada el día 7 (tope 1 por semana). | Hoy el día 8 es igual al día 1: no hay motivo para sostener la racha. | S | `DailyRewardRules`, `DailyRewardManager`, `PrestigeManager` (semillas) | Abre una tercera fuente de semillas: revisar contra el tope de 32 de la tienda. Reloj local manipulable (ya asumido en F-05/F-07). |
| IM-16 | Decoraciones | **Que se noten**: al comprar una, la cámara va a mostrarla (ya existe el alejamiento al abrir zona) y el set completo da un logro. | Se gastan 500.000 en cosas que quizá quedan fuera de pantalla. | S | `CameraJuice`, `DecorationManager`, IM-14 | La cámara que se va sola puede molestar: que sea corto y solo la primera vez. |
| IM-17 | Ayudantes | **Hacerlos visibles** (ver IN-01). | D5. | M | Ver IN-01 | Ver IN-01 |
| IM-18 | Analítica | **Eventos para lo nuevo**: no hay eventos de pedidos, sumideros (decoración/ayudante/skin), tienda de semillas ni cosecha rara en `AnalyticsEvents`. | Sin esto, los datos del portal no van a decir cuáles de estos sistemas se usan, y todo este documento se prioriza a ciegas. | S | `AnalyticsEvents`, `AnalyticsManager` (ya se suscribe a managers) | Ninguno. **Hacer antes de publicar.** |

---

## 3 y 4. Funcionalidades nuevas

### IN-01 · Ayudantes que se ven
- **Qué es:** por cada nivel de ayudante comprado, un peón recorre un circuito fijo entre los campos y el camión, con una pila chica en la espalda. Es adorno (como `SellTruck`): el ingreso sigue saliendo de la fórmula actual de `HelperManager`.
- **Por qué mejora:** convierte la compra más cara (850 k) en algo que se ve trabajar. Es el "momento wow" típico del género y hoy falta.
- **Esfuerzo:** M (versión adorno). L si se quiere que cosechen de verdad.
- **Reutiliza:** `HelperManager` (evento de nivel), patrón de `SellTruck` (objetos hijos de la escena, sin `Instantiate`), `PlayerAnimator` y el modelo del granjero con `PlayerSkinView` para darle otro color, `PaletteMeshBuilder`.
- **Riesgos:** draw calls y presupuesto de materiales (`PerformanceBudgetTests`); 3 peones animados por matemática deberían entrar. La versión "real" (un `IActor` que pisa parcelas, que `PlotView` ya contempla en su comentario) **no la recomiendo**: compite con el jugador por las parcelas, rompe el supuesto del bot y necesita navegación. La mentira piadosa del adorno da el 90 % del efecto.

### IN-02 · Cultivo del día
- **Qué es:** cada día un cultivo (de las zonas abiertas) paga +50 %. Se anuncia en un cartel de la plaza y con un ícono sobre ese campo.
- **Por qué mejora:** agrega una decisión diaria ("hoy conviene la calabaza") y un motivo de retorno que no cuesta contenido. Ataca D1.
- **Esfuerzo:** S-M.
- **Reutiliza:** `PriceCalculator` (un multiplicador más por cultivo), semilla por fecha como `MissionRules`, `OrderCropRule` (qué cultivos están abiertos y con qué peso), `PlazaSignLabel`.
- **Riesgos:** el bot no lo modela: acreditar el valor esperado como se hizo con la cosecha rara. Con el cultivo más caro del día el ingreso sube bastante: ponderar para que no salga siempre el mejor. Reloj local manipulable.

### IN-03 · Segundo punto de venta (carro de reparto)
- **Qué es:** un pad caro en el norte que estaciona un carro cerca de las tres zonas lejanas; vende igual que la plaza. Comprable una vez por temporada.
- **Por qué mejora:** es la única mejora que ataca el límite real del final (D4, el 80 % del tiempo caminando). Se va a sentir más que Precio 6.
- **Esfuerzo:** M.
- **Reutiliza:** `SellZone`, `SellFeedback`, `SellTruck`, `PurchasePad` + `PadBuilder`, `ProgressionManager` (se reinicia con la temporada como una mejora más), `GuidePoints` (la flecha "conviene ir" tendría que elegir el más cercano).
- **Riesgos:** **mueve fuerte el balance**: el bot tiene que modelar la ruta y hay que recalibrar todo el tramo final de `ProgressionData`. Las pruebas de plaza y de forma de pads (`PadShapeTests`) piden geometría propia. Hacerlo solo junto con una recalibración (B-05).

### IN-04 · Perks nuevos: Regateo, Mano verde y Suerte
- **Qué es:** tres perks más para los puntos de nivel: +1 % de precio, +1 % de crecimiento, +0,2 puntos de chance de cosecha rara por elección (10 elecciones cada uno).
- **Por qué mejora:** con 19 puntos y 50 elecciones posibles hay que elegir de verdad; aparecen "builds" (rápido, rico, suertudo). Es IM-06.
- **Esfuerzo:** M.
- **Reutiliza:** `PerkKind`, `PerkData`, `ProgressionModifiers`, `PerkPad`, `PriceCalculator`, `GameConfig.rareHarvestChance`.
- **Riesgos:** cada perk es **un pad en la plaza** (`PerkSpec` tiene posición X): tres pads más no entran sin rediseñar el sector, y la plaza ya se ensanchó una vez. Alternativa: un solo pad que abre un panel de elección (cambia la regla de "las compras viven en los pads"). Precio y crecimiento tienen tope ×3 vigilado por las pruebas de datos: los perks son un multiplicador aparte, revisar que el total siga bajo los topes de `PrestigeShopTests`.

### IN-05 · Maestría por cultivo (almanaque)
- **Qué es:** cada cultivo cuenta sus cosechas de por vida; en 100 / 1.000 / 10.000 sube una estrella que da +5 % de precio permanente a ese cultivo. Un panel "Almanaque" muestra las 8 fichas.
- **Por qué mejora:** da 24 metas largas que sobreviven al prestigio y hace que volver a los cultivos baratos tenga sentido. Ataca D2 y D3.
- **Esfuerzo:** M (lógica + guardado) + S (panel).
- **Reutiliza:** `PlotView.Harvested` (ya lleva el `CropData`), patrón de `AchievementManager` (contadores + `ISaveable`), `AchievementPanelView` como molde del panel, `PriceCalculator`.
- **Riesgos:** sección nueva de guardado (cambia la cuenta que vigila `RewardsIntegrationTests`). +15 % por cultivo se multiplica con prestigio y tienda: comprobar el techo total. El girasol (680 s) tarda muchísimo en llegar a 10.000: escalar los umbrales por cultivo.

### IN-06 · Temporadas con regla
- **Qué es:** al empezar una temporada se elige entre 2 cartas sorteadas: "Sequía" (crece −20 %, vende +40 %), "Feria" (pedidos cada 30 s), "Año de suerte" (rara al 6 %), "Mochila pesada" (+10 lugares, −10 % de velocidad).
- **Por qué mejora:** es la respuesta directa a D2: cada vuelta se juega distinto sin fabricar contenido.
- **Esfuerzo:** M-L (partir: lógica y datos / UI de elección).
- **Reutiliza:** `PrestigeManager`, `SeasonPanelView`, `ProgressionModifiers`, `PriceCalculator`, parámetros de `GameConfig` que ya existen para cada efecto.
- **Riesgos:** no hay bot de prestigio (los números de F-11 ya son "cuenta analítica"): cada carta puede tener una combinación rota. Los topes de velocidad (11 u/s) y de mochila (30, por el tamaño de la pila visible) están vigilados por pruebas: las cartas no pueden pasarlos. Hacerla **solo si los datos muestran que la gente llega al prestigio**.

### IN-07 · Récord de temporada
- **Qué es:** se mide el tiempo de juego de cada temporada; cerrarla más rápido que la anterior (o bajo ciertos umbrales) da +1 semilla.
- **Por qué mejora:** le da al prestigio un objetivo propio y acelera la tienda de semillas (hoy 9 temporadas).
- **Esfuerzo:** S-M.
- **Reutiliza:** `PrestigeManager`, `GameManager` (estado `Playing` para contar solo tiempo jugado), IM-07 (el resumen muestra el récord).
- **Riesgos:** hoy **no hay contador de tiempo jugado** en los managers: campo nuevo guardado. El offline y los ayudantes acortan la temporada "gratis": aceptarlo o medir solo tiempo activo. Presiona a correr, que no es el tono del juego: dejarlo como premio, no como exigencia.

### IN-08 · Pad de "Precio ×2" por video
- **Qué es:** un tercer pad de video junto a Turbo y fertilizante: precio de venta ×2 durante 60 a 90 s.
- **Por qué mejora:** es otra ubicación de rewarded (el objetivo del proyecto es ingreso por anuncios) y combina con la mochila llena: "miro el video justo antes de vender".
- **Esfuerzo:** S.
- **Reutiliza:** **ya está medio hecho**: `RewardKind.SellPriceBuff` existe y `AdsManager` y `BuffManager` lo manejan, pero no hay asset (`ScriptableObjects/Rewards` solo tiene Fertilizante, Offline y Turbo) ni pad. Falta el asset, un `RewardPad` con forma propia y el builder.
- **Riesgos:** con el piso de 180 s entre anuncios compite con el Turbo: puede no sumar anuncios vistos, solo repartirlos. Cumplir las reglas de CrazyGames (pad fijo, pedido por gesto; `ArchitectureTests` ya lo vigila). Usado con la mochila llena de girasol rinde mucho: acotar duración.

### IN-09 · Semillas sin tope: "Tierra fértil"
- **Qué es:** séptima fila de la tienda de prestigio, sin nivel máximo: +2 % de precio por nivel, costo creciente (1, 2, 3...).
- **Por qué mejora:** las semillas siguen sirviendo después de la temporada 9 (D3). Es el sumidero infinito clásico.
- **Esfuerzo:** S.
- **Reutiliza:** `PrestigeShopItem`, `PrestigeManager`, `PrestigeShopView`.
- **Riesgos:** `PrestigeShopItem` hoy tiene `MaxLevel` y tabla de costos: hay que admitir costo por fórmula. `PrestigeShopTests` exige venta × crecimiento < ×2: redefinir ese tope dejando esta fila afuera, con su propio límite.

### IN-10 · Nivel de granjero después del 20
- **Qué es:** pasado el nivel 20 la barra de XP sigue; cada vuelta completa entrega un "cofre" (monedas equivalentes a N minutos de producción, a veces fertilizante).
- **Por qué mejora:** la XP deja de ser un contador muerto a la hora 3 (D3) y la barra del HUD vuelve a dar premios.
- **Esfuerzo:** S-M.
- **Reutiliza:** `LevelCurve`, `PlayerProgress`, `LevelBarView`, la escala de premios de las misiones (`missionRewardScale`, minutos de producción).
- **Riesgos:** no subir `maxLevel`: los perks, la mochila de 30 y el tope de velocidad dependen de 19 puntos. Tiene que ser un contador aparte. Suma ingreso: darle valor esperado al bot.

### IN-11 · Meta semanal de misiones
- **Qué es:** completar 12 de las 21 misiones de la semana abre un cofre grande (1 semilla dorada o 30 min de producción).
- **Por qué mejora:** convierte las misiones diarias en un compromiso de varios días; perder un día no arruina la semana.
- **Esfuerzo:** S-M.
- **Reutiliza:** `MissionManager` (ya cuenta misiones reclamadas para el logro), `MissionPanelView`, `DailyRewardRules` (cuenta de días).
- **Riesgos:** se pisa con IM-15 (las dos dan semillas por constancia): elegir una. Reloj local.

### IN-12 · Visitante de la plaza
- **Qué es:** cada 8 a 12 min de juego llega un comprador a la plaza por 45 s y paga ×2 por **un** cultivo, hasta cierta cantidad. Sin video.
- **Por qué mejora:** rompe la monotonía del circuito con una oportunidad corta y gratis; hoy los únicos eventos espontáneos piden un anuncio.
- **Esfuerzo:** M.
- **Reutiliza:** `OfferTimer` y `OfferSpawner` (aparición con vida limitada), `OrderPad` (entrega desde la mochila), `OrderBoard` (qué cultivo pedir).
- **Riesgos:** se parece mucho a los pedidos: puede ser más simple hacerlo como un cuarto tipo de pedido (IM-09) que como sistema nuevo. El roadmap deja "eventos temporales" para la 1.2, con analítica.

### IN-13 · Molino: un producto elaborado
- **Qué es:** un edificio en la plaza: se deja trigo (10 unidades), tarda 60 s y devuelve una bolsa de harina que vale ×3. Una sola receta para empezar.
- **Por qué mejora:** es la segunda capa clásica del género (producir → transformar → vender) y le da uso a los cultivos baratos al final.
- **Esfuerzo:** L (partir en 3 tandas).
- **Reutiliza:** `DwellPad` (depositar parado), `Inventory` (ya mezcla productos por `CropData`), `BackpackStackView`, patrón de `PlotLogic` para el temporizador, `IProductionSource` para el offline.
- **Riesgos:** la harina tendría que ser un `CropData` sin planta: hay código que asume que todo cultivo tiene `plantPrefab` (prewarm de `PlotField`, pedidos, misiones). Rompe el bot de balance entero y el supuesto de "XP = precio base de lo vendido". **No encaja antes de tener datos**: es contenido de una 1.2, y solo si la retención lo justifica.

### Ideas que descarto (y por qué)
| Idea | Motivo |
|---|---|
| Animales, día/noche, clima | Ya descartados o diferidos en el roadmap (sección 5, DEC-7). Gasto de render y de arte fuera del presupuesto de un material. |
| Tablas de posiciones, regalos entre amigos | Necesitan backend; fuera de alcance explícito. |
| Compras con dinero real, pase de temporada | Fuera de alcance y de los límites del proyecto (pagos). |
| Elegir qué sembrar en cada campo | Es la decisión más rica posible, pero `PlotView` tiene el cultivo fijo, el prewarm del pool cuenta plantas por prefab para el nivel entero y el bot asume una zona = un cultivo. Costo L y riesgo alto de romper el presupuesto de rendimiento. IN-02 e IM-09 dan parte de esa decisión por una fracción del costo. |
| Ayudantes que cosechan de verdad | Ver IN-01. |
| Minijuegos (pesca, etc.) | Otro control y otro arte; no es hipercasual de un dedo. |
| Decoraciones con ventaja | Se decidió que son cosméticas (F-17); mezclarlo las vuelve obligatorias. |

---

## 5. Priorización

### Matriz impacto / esfuerzo
| | Esfuerzo S | Esfuerzo M | Esfuerzo L |
|---|---|---|---|
| **Impacto alto** | IM-18 analítica · IN-08 Precio ×2 · IM-12 tope offline · IM-01 combo de cosecha | IN-01 ayudantes visibles · IN-02 cultivo del día · IN-05 maestría · IN-03 segundo punto de venta | IN-06 temporadas con regla |
| **Impacto medio** | IM-14 logros · IM-13 misiones · IM-03 mochila llena · IM-07 resumen de temporada · IN-09 semillas sin tope · IM-02 garantía de rara | IN-04 perks · IN-10 XP después del 20 · IM-09 pedido urgente · IN-11 meta semanal | IN-13 molino |
| **Impacto bajo** | IM-04, IM-10, IM-11, IM-16, IM-15 | IM-05 · IN-07 récord · IN-12 visitante | — |

### Victorias rápidas (un día cada una, casi sin tocar el balance)
1. **IM-18** eventos de analítica de lo nuevo.
2. **IM-01** combo de cosecha.
3. **IM-03 + IM-04 + IM-11** tanda de jugo: mochila llena, venta grande, fin del Turbo.
4. **IM-14** logros de pedidos, raras, decoraciones, skins, ayudantes.
5. **IM-13** plantillas de misión nuevas.
6. **IN-08** pad de Precio ×2 (el código ya existe).
7. **IM-12** tope offline escalado (requiere tu OK: es una decisión tuya de B-07).
8. **IM-07** resumen de temporada.

### Apuestas de mediano plazo (una o dos sesiones, piden recalibrar)
- **IN-01** ayudantes visibles (versión adorno).
- **IN-02** cultivo del día.
- **IN-05** maestría por cultivo.
- **IN-10** XP después del nivel 20 e **IN-09** semillas sin tope (cierran D3 junto con IN-05).
- **IN-04** perks nuevos (primero resolver dónde van los pads).
- **IM-09** pedido urgente.

### Para más adelante (con datos del portal en la mano)
- **IN-03** segundo punto de venta: junto con la recalibración B-05.
- **IN-06** temporadas con regla: solo si una parte real de los jugadores llega al prestigio.
- **IN-07**, **IN-11**, **IN-12**: según qué sistema de retención muestre uso.
- **IN-13** molino: versión 1.2.

### Las 3 primeras que recomiendo
1. **IM-18 · Analítica de los sistemas nuevos (S).** No es vistosa, pero sin ella no se puede priorizar nada de lo demás: hoy pedidos, sumideros, tienda de semillas y cosecha rara no emiten eventos. Va antes del envío al portal.
2. **IM-01 + IM-03 + IM-04 · Tanda de jugo del circuito cosechar-llenar-vender (S).** Mejora los primeros 3 minutos, que es lo que decide la retención del primer día en un portal web; no toca balance, guardado ni el bot.
3. **IN-01 · Ayudantes visibles, versión adorno (M).** Arregla la peor relación precio/satisfacción del juego (D5) y deja una imagen fuerte para capturas y video (K-02), sin mover un número.

Si después de eso queda una sesión antes de publicar: **IN-08** (Precio ×2), porque el código ya está y es la única idea de la lista que apunta directo al ingreso.

**Lo que no haría ahora:** nada de la columna L, ni IN-03, ni IN-04. Todas mueven el balance o la plaza, y la economía actual todavía no se contrastó con jugadores reales (el factor ×2,5 del bot sigue "a verificar").
