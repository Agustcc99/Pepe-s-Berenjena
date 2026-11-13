# Guía de prueba en celular (la hacés vos)

Nada de esto se probó todavía en un dispositivo real: el APK solo pasó tests, el Editor y Chrome sin interfaz. Esta lista es corta a propósito.

## Antes de empezar
1. Archivo: `Builds/Release/Android/FarmIdle-0.9.0-1.apk` (release, firmado con la clave de debug salvo que hayas conectado la tuya; ver `RELEASE.md`). Para ver `[FarmIdle]` en el log conviene el APK de **desarrollo** (`Builds/Android/FarmIdle.apk`): el release no trae el panel de debug ni el contador de FPS.
2. En el celular: Ajustes → Opciones de desarrollador → Depuración USB activada. Con el cable: `adb devices` tiene que mostrarlo.
3. Instalar: `adb install -r FarmIdle-0.9.0-1.apk` (o copiar el APK y abrirlo; hay que permitir "instalar apps desconocidas").
4. Empezar un log limpio en otra terminal:
   ```bash
   adb logcat -c
   adb logcat -s Unity | grep "\[FarmIdle\]"
   ```

## Qué probar (en este orden)
- [ ] **Instalación y primer arranque**: abre sin cierre, vertical, aparece "Tap to Play", suena la música después del toque (no antes). Anotar el tiempo hasta poder jugar.
- [ ] **Primeros minutos**: las flechas guían sembrar → cosechar → vender → primera mejora y desaparecen solas. Si las seguís sin leer nada, ¿te perdés en algún paso?
- [ ] **Joystick y pads**: ¿el joystick responde bien con el pulgar? ¿Se entiende cuándo un pad "está cargando" (los 0,6 s)? ¿Los carteles se leen en pantalla chica?
- [ ] **Cosecha y venta**: la pila en la espalda, el pasaje a dos columnas, las monedas y el sonido al vender.
- [ ] **Turbo**: aparece el símbolo (~60 s), parado 1 s lo activa; ¿se *siente* más rápido (FOV, estela, destello)?
- [ ] **Fertilizante**: video (+1), comprar ($250), usar parado en su pad.
- [ ] **Video de prueba de AdMob**: el anuncio de prueba se muestra, el juego se congela y calla mientras dura, la recompensa llega UNA vez al terminarlo; cerrarlo antes NO da recompensa. Probar también sin red (modo avión): debe decir "Sin anuncio" y no regalar nada.
- [ ] **Consentimiento (UMP)**: ver la sección de abajo.
- [ ] **Pausa y volver**: Home, otra app, volver; bloquear la pantalla; llamada o notificación. El juego debe quedar pausado y silencioso y volver bien (sin quedar trabado).
- [ ] **Guardado**: matar la app desde recientes y reabrir: monedas, mochila, nivel, mejoras y stock siguen.
- [ ] **Ajustes**: el ícono del parlante (arriba a la derecha) abre Música / Efectos / Silenciar, y el cambio sobrevive a cerrar la app.
- [ ] **FPS y batería**: jugá 10 minutos seguidos. Anotá el FPS típico y el peor, si el celular se calienta y cuánto bajó la batería (% al empezar y al terminar).

## Consentimiento con simulación de región
Sirve para ver el formulario de UMP aunque no estés en Europa.
1. Abrí `Assets/Resources/AdMobConfig` en el Editor y poné `Debug Geography = EEA`. (Solo vale en el Editor y en builds de **desarrollo**: un release lo ignora.)
2. Reconstruí el APK de desarrollo e instalalo.
3. Arrancá una vez y buscá en `adb logcat -s Unity` la línea del SDK `Use ConsentDebugSettings ... addTestDeviceHashedId("XXXXXXXX")`. Copiá ese hash en `Test Device Hashed Ids` del mismo asset y reconstruí.
4. Reinstalá con los datos borrados (`adb shell pm clear com.farmidle.prototype`) para que UMP vuelva a preguntar. Deberías ver el formulario ANTES del primer anuncio.
5. Probá las tres salidas: aceptar, rechazar y cerrar sin elegir. Verificá que con "Privacidad" en Ajustes se puede reabrir.
6. Volvé `Debug Geography` a `Off` cuando termines.

## Qué traerme
- **Modelo del celular**, versión de Android y RAM.
- **FPS** típico y peor, sensación de input (¿lag del joystick?), calor y % de batería gastado en 10 min.
- **Capturas** de: primer arranque, el tutorial, el HUD con Turbo, un video de prueba, el formulario UMP y cualquier cosa rara.
- **Log filtrado**: `adb logcat -d -s Unity | grep "\[FarmIdle\]" > farmidle.log` (y el `adb logcat -d` completo si hubo un cierre inesperado).
- Cualquier cierre inesperado: qué estabas haciendo justo antes.
