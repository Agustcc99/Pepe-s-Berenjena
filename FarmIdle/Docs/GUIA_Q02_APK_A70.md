# Guía Q-02 — Probar el APK en el Samsung A70

Objetivo: jugar 30 min y devolverme un informe de 10 líneas con problemas y modelo/Android.

**Archivo a instalar** (26,9 MB, Android 8+ / arm64; el A70 cumple):
`C:\Users\Agus Tacconi\Downloads\aa\FarmIdle\Builds\Release\Android\FarmIdle-0.9.0-1.apk`

Está firmado con la clave de debug (normal en esta etapa). Android puede avisar "app de origen desconocido" o Play Protect puede mostrar un aviso: es esperable, no es un virus.

## 1. Pasar el APK al celular (elegí UNA)

**Opción A — Cable USB (la más simple)**
1. Conectá el A70 a la PC con el cable. En el celular, deslizá la barra de notificaciones, tocá "Cargando este dispositivo vía USB" y elegí **Transferencia de archivos**.
2. En la PC abrí el Explorador, entrá al A70 → Almacenamiento interno → carpeta `Download`.
3. Copiá ahí el archivo `FarmIdle-0.9.0-1.apk`.

**Opción B — Google Drive (sin cable)**
1. En la PC subí el APK a tu Google Drive (drive.google.com → Nuevo → Subir archivo).
2. En el celular abrí la app de Drive, tocá los tres puntos del archivo → **Descargar**. Queda en `Download`.

(Evitá WhatsApp: a veces renombra o bloquea los .apk.)

## 2. Instalarlo
1. En el celular abrí la app **Mis archivos** (Samsung) → Descargas → tocá `FarmIdle-0.9.0-1.apk`.
2. Si dice "Por tu seguridad, el teléfono no puede instalar apps desconocidas de esta fuente": tocá **Ajustes** y activá **Permitir desde esta fuente** (para Mis archivos, o Drive/Chrome si lo bajaste desde ahí). Volvé atrás.
3. Tocá **Instalar**. Si aparece Play Protect ("Analizar app" / "Instalar de todos modos"), elegí **Instalar de todos modos**.
4. Al terminar tocá **Abrir**. El ícono queda en el cajón de apps con el nombre del juego (paquete `com.farmidle.prototype`, versión 0.9.0).

Si dice "No se instaló la app" o "paquete no válido": anotá el mensaje exacto y el modelo/versión de Android (Ajustes → Acerca del teléfono → Información de software) y me lo mandás.

## 3. Qué anotar durante los 30 min
Poné un cronómetro. Antes de empezar anotá: **% de batería** y si el celular está frío/tibio.

1. **Arranque**: ¿cuánto tarda en abrir? ¿Hay pantalla negra o cuelgue?
2. **Controles táctiles**: ¿el joystick/dedo responde bien? ¿Se siente impreciso o lento? ¿Los botones son fáciles de tocar?
3. **FPS percibido**: ¿fluido, con tirones, o se traba en algún momento (por ejemplo al cosechar mucho o al vender)? (La build de release no muestra contador de FPS: es solo sensación.)
4. **Sonido y música**: ¿se escuchan bien? ¿Se corta? ¿Los interruptores de Ajustes (música/efectos/silenciar) funcionan?
5. **Vibración** (U-06, aún sin probar en el celular): ¿se siente al cosechar, vender, comprar, subir de nivel? ¿Molesta o es muy fuerte? ¿Se apaga con el interruptor de Ajustes?
6. **Calor y batería**: al minuto 15 y 30 anotá el % de batería y si el celular está caliente (tibio / caliente / incómodo).
7. **Guardado** (importante): a los ~10 min jugando, anotá cuántas monedas tenés y qué compraste. Cerrá el juego **deslizándolo fuera de la lista de apps recientes** (botón de recientes → deslizar hacia arriba) y reabrilo. ¿Está todo igual? Después hacelo una vez más cerrándolo desde Ajustes → Apps → Farm → **Forzar detención**.
8. **Interrupciones**: bloqueá la pantalla 1 min y volvé; mandate un mensaje para que llegue una notificación; salí al inicio y volvé. ¿Sigue bien o se reinicia/rompe?
9. **Anuncios**: los IDs son de prueba (`useTestAds` está activo). Si ves un anuncio, tiene que decir "Test Ad". Podés ver uno para chequear que da la recompensa, pero **no lo toques/clickees**. Si alguna vez un anuncio NO dice que es de prueba, dejá de tocar y avisame.
10. **Tutorial/primeros minutos**: ¿entendiste qué hacer sin ayuda? ¿En qué momento dudaste?
11. **Pantalla**: ¿se ve completo (notch, bordes, textos cortados)? ¿Probaste girar el celular?

## 4. Qué traerme (informe de ~10 líneas)
- Modelo exacto (Samsung A70 / SM-A705…) y versión de Android.
- Cantidad de crashes/cierres inesperados (y qué hacías).
- Batería: % inicial → % a los 30 min; calor.
- FPS percibido: fluido / con tirones (cuándo).
- Resultado del guardado (escenarios 1 y 2 del punto 7: OK / se perdió algo).
- Sonido y vibración: OK / problemas.
- Los 3 problemas más molestos, una línea cada uno.

## Opcional (si te animás): ver los errores técnicos con el cable
Si hay crashes, con el celular conectado y "Depuración USB" activa (Ajustes → Opciones de desarrollador; para verla tocá 7 veces "Número de compilación" en Información de software), yo puedo leer el log con `adb logcat` si me decís que lo conectaste. No hace falta para el informe básico.
