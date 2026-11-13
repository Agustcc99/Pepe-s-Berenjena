# Cómo sacar un release de FarmIdle

Versión actual: `0.9.0` / código `1` (`Assets/_Scripts/Editor/ReleaseVersion.cs`). Unity `6000.6.6f1`, módulos Android y WebGL.

Hay dos tipos de build y no se mezclan:

| | Prueba (desarrollo) | Release |
|---|---|---|
| Menú / método | `ProjectConfigurator.BuildAndroid` / `BuildWeb` | `FarmIdle > Release > Build Android (APK + AAB)` / `Build Web` (`ReleaseBuilder`) |
| Salida | `Builds/Android/FarmIdle.apk`, `Builds/Web` | `Builds/Release/Android`, `Builds/Release/Web` (y `Web-Gzip`) |
| Development Build | sí | **no** |
| `DebugPanel`, `FpsCounter`, `MockAdService` | sí (también en el Editor) | **no existen** (se compilan solo con `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`; el release saca el panel de la escena y `ReleaseBuilder` revisa las dlls de FarmIdle y falla si alguno aparece) |
| Anuncios | Mock / AdMob de prueba | AdMob (Android), adaptador del portal (web) o ninguno |

## 1. Android

### 1.1 Crear la clave de firma (la hacés vos; es SECRETA)
Nada de esto va al repositorio. Guardá el archivo y las contraseñas en un lugar fuera del proyecto y hacé una copia de respaldo: **si perdés la clave no podés actualizar la app** (con Play App Signing, perder la *upload key* se puede resolver pidiéndole a Google que la restablezca, pero es un trámite).

Con el JDK que trae Unity (PowerShell):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" -genkeypair -v -keystore D:\secretos\farmidle-upload.keystore -alias farmidle -keyalg RSA -keysize 2048 -validity 10000
```

`keytool` te pregunta las contraseñas y tus datos. Recomendado: en Play Console activá **Play App Signing** y subí con esta clave como *upload key* (Google guarda la clave de firma final).

### 1.2 Conectar la clave al build (variables de entorno)
El build lee estas variables y no guarda nada en el proyecto (al terminar limpia los campos de firma):

```powershell
$env:FARMIDLE_KEYSTORE_PATH = 'D:\secretos\farmidle-upload.keystore'
$env:FARMIDLE_KEYSTORE_PASS = Read-Host 'Contraseña del almacén'
$env:FARMIDLE_KEY_ALIAS     = 'farmidle'
$env:FARMIDLE_KEY_PASS      = Read-Host 'Contraseña de la clave'   # si falta, se usa la del almacén
```

Sin ellas el release sale firmado con la clave de **debug** (sirve para probar en un celular; Google Play NO lo acepta) y el log lo avisa. Para cambiar la versión: `FARMIDLE_VERSION_NAME` y `FARMIDLE_VERSION_CODE` (en cada subida a Play el código tiene que ser mayor que el anterior), o editar `ReleaseVersion.cs`.

### 1.3 IDs de AdMob (también los ponés vos)
Un único lugar: `Assets/Resources/AdMobConfig.asset`.
1. En la consola de AdMob creá la app (Android) y un bloque de anuncios **con recompensa**.
2. Cargá `Real Android App Id` (`ca-app-pub-XXXX~YYYY`) y `Real Rewarded Unit Id` (`ca-app-pub-XXXX/ZZZZ`) y **apagá `Use Test Ads`**.
3. Antes de cada build de Android `AdMobConfigSync` copia el App ID al manifiesto (falla si el formato es inválido). No hay que tocar código.

Mientras `Use Test Ads` esté activo o falte algún ID real se usan los IDs de prueba de Google y el build lo avisa. Nunca pruebes con anuncios reales tocándolos vos mismo: AdMob lo considera tráfico inválido.

### 1.4 Construir
Cerrá el Editor (solo una instancia puede abrir el proyecto) o usá el menú `FarmIdle > Release > Build Android (APK + AAB)`. Por consola:

```powershell
$env:TEMP = 'C:\Users\Agus Tacconi\Downloads\aa\FarmIdle\Library\fi-tmp'; $env:TMP = $env:TEMP
Start-Process -Wait -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe' -ArgumentList '-batchmode','-projectPath','"C:\Users\Agus Tacconi\Downloads\aa\FarmIdle"','-logFile','"C:\Users\Agus Tacconi\Downloads\aa\FarmIdle\Logs\release-android.log"','-buildTarget','Android','-executeMethod','FarmIdle.EditorTools.ReleaseBuilder.BuildAndroid','-quit'
```

(El `TEMP` redirigido es necesario en este equipo: ver la memoria del proyecto, "Android build workarounds".) Salida en `Builds/Release/Android`:
- `FarmIdle-0.9.0-1.apk`: para instalar a mano.
- `FarmIdle-0.9.0-1.aab`: lo que se sube a Google Play.
- `FarmIdle-0.9.0-1-0.9.0-v1-IL2CPP.symbols.zip`: símbolos para entender cierres en Play Console (subirlos con el AAB).
- `…_BackUpThisFolder_ButDontShipItWithYourGame`: no se sube; sirve para depurar.

El log termina con una línea `[FarmIdle] Release Android: … verificado: sin MockAdService, DebugPanel, FpsCounter en las bibliotecas de FarmIdle`. Si no dice "verificado", no subas el build.

Tamaños medidos (2026-10-19, IL2CPP arm64, minSdk 26, targetSdk 36): **APK 26,9 MB**, **AAB 26,7 MB**, símbolos 8,9 MB. Antes de optimizar, el APK de desarrollo pesaba 58,0 MB (37,8 MB el release sin las optimizaciones de tamaño). El minify de R8 NO se activó: no se puede probar AdMob sin un celular y un mal recorte rompe el SDK de anuncios; es lo primero que se puede probar después de la prueba en el celular.

### 1.5 Validar el archivo (SDK que trae Unity)
```powershell
$sdk = 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Data\PlaybackEngines\AndroidPlayer'
& "$sdk\SDK\build-tools\36.0.0\aapt2.exe" dump badging "Builds\Release\Android\FarmIdle-0.9.0-1.apk"
& "$sdk\OpenJDK\bin\java.exe" -jar "$sdk\SDK\build-tools\36.0.0\lib\apksigner.jar" verify --verbose "Builds\Release\Android\FarmIdle-0.9.0-1.apk"
```
Tiene que decir `package: name='com.farmidle.prototype' versionCode='1' versionName='0.9.0'`, `minSdkVersion:'26'`, `targetSdkVersion:'36'`, `native-code: 'arm64-v8a'` y `Verified using v2 scheme (APK Signature Scheme v2): true` (v1 y v3 en `false` es lo normal con la clave de debug). Permisos que trae (todos del SDK de anuncios): `INTERNET`, `ACCESS_NETWORK_STATE`, `AD_ID`, `ACCESS_ADSERVICES_AD_ID/ATTRIBUTION/TOPICS` (Privacy Sandbox), `WAKE_LOCK`, `FOREGROUND_SERVICE`; no se pide cámara, ubicación ni almacenamiento.

## 2. Web

### 2.1 Construir
Menú `FarmIdle > Release > Build Web`, o por consola igual que arriba con `-buildTarget WebGL -executeMethod FarmIdle.EditorTools.ReleaseBuilder.BuildWeb`. Sale en `Builds/Release/Web` (Brotli). Para hostings sin Brotli: poné `$env:FARMIDLE_WEB_COMPRESSION = 'gzip'` antes y sale en `Builds/Release/Web-Gzip`.

### 2.2 Tamaños medidos (2026-10-19)
| Build | Total | wasm | data |
|---|---|---|---|
| Web release, sin comprimir (medición base, antes de optimizar) | 48,5 MB | 38,4 | 12,1 |
| Web release **Brotli** (`Builds/Release/Web`) | **8,4 MB** | 5,46 | 2,74 |
| Web release **Gzip** (`Builds/Release/Web-Gzip`) | **10,5 MB** | 7,16 | 3,24 |

Ambos arrancan en Chrome sin interfaz servidos con `python -m http.server` (sin ningún header de compresión): el respaldo de descompresión en JS (`decompressionFallback`) lo resuelve. Los archivos del Gzip se llaman `Web-Gzip.*` porque Unity usa el nombre de la carpeta.

### 2.3 Qué necesita el hosting
- **Desde 2026-10-29 el build sale SIN respaldo de descompresión** (archivos `*.br`, o `*.gz` en la variante Gzip): CrazyGames rechaza los `*.unityweb` al subir. Esos archivos exigen que el servidor mande `Content-Encoding: br` (el de CrazyGames lo hace). Para un hosting sin headers, o para probar con un servidor estático simple, construir con `FARMIDLE_WEB_FALLBACK=on`; las dos viñetas de abajo describen ese modo.
- **Con el respaldo de descompresión activo (como está) no hace falta configurar nada**: los archivos se llaman `*.unityweb` y el loader los descomprime en JS si el servidor no manda `Content-Encoding`. Es la opción más portable, pero descomprimir en JS tarda más y el wasm no se compila en *streaming*.
- **Para que cargue más rápido**: que el servidor mande `Content-Encoding: br` (o `gzip` en la variante Gzip) en los `*.unityweb`, con el `Content-Type` original de cada archivo (`application/wasm` para el wasm, `application/javascript` para el framework, `application/octet-stream` para los datos). Si el hosting no deja fijarlo por archivo, dejá el respaldo.
- Servir por HTTPS (Service Workers, `localStorage` y los SDK de los portales lo piden).
- `dataCaching` está activado: el navegador guarda los datos en IndexedDB y la segunda carga es mucho más rápida.

### 2.4 Guardado en la web
Se guarda en IndexedDB (1 s después de un cambio, y al ocultar la pestaña) **y** en `localStorage` de forma síncrona (`MirroredSaveStorage`): si se cierra la pestaña de golpe y IndexedDB no terminó, gana la copia más reciente. Verificado en Chrome sin interfaz: con el volumen silenciado, se borró IndexedDB a mano, se recargó y el juego volvió silenciado (restaurado desde `localStorage`).

### 2.5 Subirlo a un portal
Ver `WebPortals/README.md` (CrazyGames recomendado, Poki, y qué agregar al `index.html`). Para CrazyGames, `Build Web` ya deja `Builds/Release/Web-CrazyGames` (o `Web-Gzip-CrazyGames`) con el SDK y el adaptador puestos: esa carpeta es la que se sube. Sin adaptador, el build release no muestra anuncios y los pads de video dicen "Sin anuncio".

## 3. Declaraciones y privacidad (lo hacés vos en las consolas)
Esto es una guía; verificá siempre contra las páginas vigentes de Google, que cambian.

**Qué recoge el SDK de anuncios (Google Mobile Ads / AdMob)**, aunque el juego no pida nada: ID de publicidad (Advertising ID), dirección IP y ubicación aproximada derivada de ella, identificadores del dispositivo/aplicación, datos de interacción con anuncios, y diagnósticos/informes de fallos. El juego en sí guarda la partida solo en el dispositivo (monedas, mochila, mejoras, ajustes) y **no** tiene cuentas, analytics propios ni compras.

**Política de privacidad** (una URL pública, obligatoria en Play y AdMob). Tiene que decir:
- quién sos y cómo contactarte;
- que la app muestra anuncios de Google AdMob con recompensa, que Google y sus socios pueden usar el ID de publicidad e información del dispositivo para mostrarlos y medirlos, con enlace a la política de Google (https://policies.google.com/technologies/partner-sites);
- que en el Espacio Económico Europeo, Reino Unido y otras regiones se pide consentimiento antes de personalizar anuncios y que se puede cambiar después desde Ajustes → "Privacidad";
- que la partida se guarda localmente en el dispositivo, que no se envía a un servidor propio y que se borra al desinstalar;
- para qué público es la app y que no se recoge a sabiendas información de menores de 13 años (o de la edad que corresponda en tu país);
- cómo pedir información o borrado (un correo).

**Google Play Console**
- *Contenido de la app → Seguridad de los datos (Data safety)*: declarar los datos que recoge el SDK de anuncios (ID de dispositivo u otros ID, ubicación aproximada, informes de fallos/diagnóstico, interacción con anuncios según corresponda: usar la tabla de Google "Declaraciones de Play Data safety para AdMob"), con finalidad "Publicidad o marketing" y que se comparten con Google.
- *Contenido de la app → ID de publicidad*: "Sí, la app lo usa" (finalidad: publicidad). El SDK agrega el permiso `com.google.android.gms.permission.AD_ID`.
- *Contenido de la app → Anuncios*: "Contiene anuncios: sí".
- *Público objetivo y contenido*: elegí la edad con honestidad. Si incluís menores de 13 años, la app cae en la política "Familias" y solo puede usar anunciantes certificados para ese público: para un prototipo conviene apuntar a 13+ o a adultos, y dejar `tagForUnderAgeOfConsent` apagado en `AdMobConfig`. Si la app va a menores, avisame antes: cambia el SDK y los ajustes.
- *Clasificación de contenido* (cuestionario IARC) y *política de privacidad* (la URL).
- Subir el `.aab` y el `.symbols.zip`.

**AdMob**
- Crear la app y el bloque con recompensa; vincular la app con la ficha de Play cuando esté publicada.
- *Privacidad y mensajes → Consentimiento* (GDPR): crear y **publicar** el mensaje de consentimiento (UMP) para la app y elegir los socios; sin mensaje publicado el formulario no aparece. Agregar tus dispositivos de prueba para ver el formulario antes de publicar.
- Completar los datos de pago e impuestos para cobrar.
- No tocar los anuncios reales con el dedo propio; probar con los de prueba.

**Consentimiento en el juego** (ya implementado): `AdMobAdService` pide el estado a UMP **antes** de `MobileAds.Initialize`, solo pide anuncios si `CanRequestAds`, reintenta, y Ajustes muestra "Privacidad" cuando UMP lo exige. Cómo probarlo con región simulada: `PRUEBA_CELULAR.md`.

## 4. Antes de subir (lista corta)
- [ ] IDs reales cargados y `Use Test Ads` apagado.
- [ ] Clave de firma propia conectada (el log dice "firmado con la clave de release").
- [ ] Log con "verificado: sin MockAdService, DebugPanel, FpsCounter".
- [ ] `versionCode` mayor que el último subido.
- [ ] Probado en un celular real (`PRUEBA_CELULAR.md`).
- [ ] Mensaje UMP publicado en AdMob y política de privacidad en línea.
