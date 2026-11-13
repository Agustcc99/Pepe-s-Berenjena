# Auditoría del proyecto — Pepe's Berenjena (Farm Idle)

Fecha: 2026-11-13 · Alcance: `ROADMAP.md`, `FarmIdle/Docs`, `Assets/_Scripts`, `Assets/_Tests`, `ProjectSettings`, `Packages`, carpetas raíz.
Solo lectura: no se tocó código ni se corrieron las suites (las cifras de pruebas salen del registro del roadmap del 2026-11-13).

## 1. Resumen

El código está sano y bien protegido por pruebas. **El riesgo más grande no es técnico: el proyecto no tiene control de versiones** y el único respaldo es una copia manual de hace 15 días. Lo segundo es que la salida a la web está frenada por tareas tuyas (Q-10, D-05, mail de la política), no por código.

| Área | Estado | Nota |
|---|---|---|
| Respaldo / versiones | 🔴 Crítico | Sin commits; repo git mal ubicado |
| Camino a publicar web | 🟠 Frenado | 3 ítems "Yo" + política sin mail |
| Arquitectura de capas | 🟢 Bien | Grafo de asmdef limpio, vigilado por pruebas |
| Rendimiento (reglas) | 🟢 Bien | Sin `Instantiate`/`Find*` fuera del pool |
| Guardado | 🟢 Bien | Escritura atómica, versionado, migraciones |
| Pruebas | 🟢 Bien | 507 EditMode + 261 PlayMode (no re-corridas hoy) |
| Config. Android | 🟠 Pendiente | Id de paquete "prototype", empresa "FarmIdle" |
| Documentación | 🟡 Desfasada | Traspaso e índice atrasados 2 semanas |
| Limpieza | 🟡 Menor | Restos de plantilla y de corridas de prueba |

## 2. Números

- Código de juego: 170 archivos, ~19,6 mil líneas (sin Editor). Editor: 35 archivos, 7,3 mil. Pruebas: 94 archivos, 20,2 mil (más pruebas que juego).
- Por capa: Managers 77 archivos / 9,7 mil líneas · Environment 31 / 3,8 mil · UI 23 / 3,1 mil · Data 17 / 1,4 mil · Player 10 / 1,3 mil · Core 12 / 0,3 mil.
- Textos: 191 claves en `Localization.csv`.
- Builds guardados: web 8,5 MB (Brotli), APK de release `FarmIdle-0.9.0-1.apk`. Carpeta `Builds/` 597 MB, `Library/` 5,7 GB.

## 3. Hallazgos

### 🔴 H-1. No hay control de versiones
- `git` encuentra un repositorio en `C:\Users\Agus Tacconi` (tu carpeta de usuario entera), **sin ningún commit**. El proyecto no tiene repo propio.
- Único respaldo: `Downloads\aa_backup_2026-10-29`. Desde ese día se hicieron F-11, F-17, F-18, D-15, J-01 a J-07, V-01, V-02, U-09: todo eso existe en una sola copia, en `Downloads`.
- Además, ese repo en la carpeta de usuario es peligroso: un `git add .` ahí incluiría `.ssh`, `cookies.txt`, `.claude.json`, etc.
- `FarmIdle/.gitignore` ya existe y está bien armado (ignora `Library`, `Builds`, `Logs`, `UserSettings`).

### 🟠 H-2. La web está lista en código pero frenada en tareas tuyas
- Q-10 (medir los 4 criterios de salida), K-02 (video), D-05 (ficha y envío): los tres en Pendiente y a tu cargo.
- M-04 En curso: `Legal/index.html` todavía tiene `[CORREO_DE_CONTACTO]` y `[CONTACT_EMAIL]` sin reemplazar, y no está publicada en una URL.
- El último registro del roadmap dice "sin build web nuevo": el build en `Builds/Release/Web-CrazyGames` es anterior a J-05/J-06/J-07/U-09/V-02. Lo que subas hoy no tiene esos cambios.
- J-06 (cámara) y U-09 (flecha) están marcados "sin ver en un dispositivo" / "probar si molesta".

### 🟠 H-3. Identidad de Android sin definir (no urge, pero es irreversible)
- `applicationIdentifier` de Android: `com.farmidle.prototype`. En Play **el id de paquete no se puede cambiar después de publicar**. Hay que definirlo antes del primer AAB que subas (incluso a prueba cerrada).
- `companyName: FarmIdle` (el estudio decidido es "Pepe's Dev", DEC-2b). En la web afecta el nombre de la base IndexedDB del guardado: cambiarlo después de publicar puede dejar a los jugadores sin partida (**a verificar** antes de tocarlo; si se cambia, que sea antes de D-05).
- `bundleVersion 0.9.0`, `AndroidBundleVersionCode 1`: bien por ahora; D-11 (plan de versiones) sigue pendiente.

### 🟠 H-4. Plugin de AdMob: instalado, pero los documentos dicen que espera tu OK
- `Assets/GoogleMobileAds/`, `Plugins/Android/googlemobileads-unity.aar` y el define `FARMIDLE_ADMOB` (solo Android) están en el proyecto.
- `ROADMAP` M-02 dice "el plugin de AdMob queda a tu OK" y `DECISIONES.md` no registra la decisión de instalarlo.
- No hay IDs reales cargados: solo los de prueba de Google y uno ficticio en `ConsentFlowTests`. Correcto.
- Falta: anotar en `DECISIONES.md` cuándo se aprobó y cuánto pesa (el APK de la Fase 9 medía 26,9 MB; **medir el actual**).

### 🟡 H-5. Documentación atrasada respecto del código
- `Docs/README.md` y `CLAUDE.md` apuntan a `HANDOFF_HITO_2.md` (2026-10-29) como último traspaso; falta el del Hito 3.
- `ROADMAP.md`: el título dice "nombre provisorio"; la sección 8 "Próximos 5 pasos" lista tareas ya hechas (Q-01, Q-02, D-01…); el Hito T todavía cita "71 archivos, `Phase1` a `Phase12`".
- `ROADMAP.md` línea 72: las filas U-04 y U-05 quedaron pegadas en una sola línea (la tabla se rompe).
- `Docs/FICHA_PLAY.md` existe y no está en el índice.
- AR-05 figura Hecho, pero siguen 11 archivos `PhaseNSceneBuilder.cs` (4 de ellos de 420 a 500 líneas) junto a los builders nuevos por responsabilidad. No es un error, pero `ARQUITECTURA.md` debería decir qué queda en cada uno.
- La memoria de Claude del proyecto está desactualizada (dice Fase 9 y "plugin esperando OK").

### 🟡 H-6. Restos para limpiar
- 8 escenas `Assets/InitTestScene*.unity` (quedan cuando una corrida de PlayMode se corta). Inofensivas, pero ensucian.
- Plantilla de Unity sin usar: `Assets/Scenes/SampleScene.unity`, `Assets/TutorialInfo/`, `Assets/Readme.asset` (no entran al build: solo Bootstrap y MainLevel están habilitadas).
- Carpetas vacías: `Assets/_Scripts/Services/`, `Assets/ThirdParty/`, `FarmIdle/build/`.
- Paquetes sin uso aparente: `com.unity.collab-proxy`, `com.unity.ide.rider` (solo editor, no pesan en el build).
- Raíz: `PROMPT_FASE_4..9.md` y `PROMPT_ROADMAP.md` son históricos; `Downloads\aa_spike` ya cumplió (D-03 Hecho).
- `Builds/` tiene 3 generaciones de builds (597 MB).

### 🟡 H-7. Deuda técnica menor (ninguna bloquea)
- 28 managers con `static Instance`. Funciona y está probado; el costo es que las pruebas dependen del orden de arranque. No refactorizar ahora.
- `DebugPanel.cs` 556 líneas (excluido de builds finales por `#if`, bien). `BackpackStackView.cs` 465 y `GameConfig.cs` 412 se acercan al tope de 500 que vigila `ArchitectureTests`.
- 72 llamadas a `Debug.Log*` en 32 archivos de juego. La mayoría son avisos de configuración; **a verificar** que ninguna quede en un camino por cuadro en el build final.
- `StabilityTests.TheMonkey_...` es inestable por diseño (azar). Con semilla fija por caso dejaría de dar falsos rojos.
- `webGLNameFilesAsHashes: 0`: al actualizar el juego en el portal, un navegador podría servir archivos viejos en caché (**a verificar** cómo versiona CrazyGames las subidas; si usa una carpeta por versión, no aplica).

### 🟢 Lo que está bien (no tocar)
- Capas: Core y Data sin dependencias; Managers → Core, Data; Environment, Player y UI → Managers y no se referencian entre sí. Coincide con `CLAUDE.md`.
- Juego sin `Instantiate` fuera de `PoolManager`, sin `FindObjectOfType`/`GameObject.Find`, sin `PlayerPrefs`.
- Guardado: temporal + reemplazo atómico, respaldo, migrador versionado, errores atrapados y registrados.
- Android: IL2CPP, ARM64, min SDK 26, stripping alto. Web: Brotli, stripping alto, memoria inicial 32 MB.
- Sin secretos en el proyecto (keystore sin configurar, IDs solo de prueba).
- Sin `[Ignore]` ni `[Explicit]` en las pruebas.

## 4. Plan de acción

Ordenado por urgencia. "Quién" sigue la convención del roadmap.

### Ahora (antes de cualquier otra tarea)
| # | Acción | Quién | Esfuerzo | Hallazgo |
|---|---|---|---|---|
| 1 | Copiar `Downloads\aa` a otro disco o a la nube (sin `Library` ni `Builds`: quedan ~30 MB). | Yo | 5 min | H-1 |
| 2 | Crear un repo git propio en `Downloads\aa` (`git init` ahí, primer commit) y subirlo a un repo **privado** de GitHub. Requiere tu pedido explícito para el commit y el push. | Ambos | S | H-1 |
| 3 | Decidir qué hacer con el repo vacío de `C:\Users\Agus Tacconi` (recomendado: borrar esa carpeta `.git`; no tiene commits). | Yo | 5 min | H-1 |

### Esta semana (destraba la publicación web)
| # | Acción | Quién | Esfuerzo | Hallazgo |
|---|---|---|---|---|
| 4 | Build web nuevo con todo lo del 2026-11-13 y medir carga (criterio de D-15: < 10 s). | Claude | S | H-2 |
| 5 | Probar ese build: Q-10 (4 criterios) y de paso J-06 y U-09 en el A70. | Yo | S | H-2 |
| 6 | Poner el mail de contacto en `Legal/index.html` y publicarla (M-04). | Ambos | S | H-2 |
| 7 | Decidir `companyName` antes de enviar (verificar el efecto sobre el guardado web). | Ambos | S | H-3 |
| 8 | Ficha y envío a CrazyGames (D-05); el video (K-02) puede ir después. | Yo | S | H-2 |

### Mientras esperás la revisión del portal
| # | Acción | Quién | Esfuerzo | Hallazgo |
|---|---|---|---|---|
| 9 | `HANDOFF_HITO_3.md` + actualizar índice, `CLAUDE.md`, memoria; arreglar la fila U-04/U-05, título y sección 8 del roadmap. | Claude | S | H-5 |
| 10 | Anotar en `DECISIONES.md` el OK del plugin de AdMob y el peso actual del APK. | Ambos | S | H-4 |
| 11 | Limpieza: `InitTestScene*`, plantilla de Unity, carpetas vacías, paquetes sin uso, builds viejos. Con las suites en verde después. | Claude | S | H-6 |
| 12 | Semilla fija para `TheMonkey` y revisión de `Debug.Log` en caminos por cuadro. | Claude | S | H-7 |

### Antes del primer AAB a Play (Hito 5)
| # | Acción | Quién | Esfuerzo | Hallazgo |
|---|---|---|---|---|
| 13 | Definir el id de paquete definitivo (reemplaza `com.farmidle.prototype`). Es para siempre. | Yo decide, Claude aplica | S | H-3 |
| 14 | Plan de versiones (D-11): cómo suben `bundleVersion` y `versionCode`. | Ambos | S | H-3 |

### No hacer ahora
- Refactor de los singletons, partir más los builders `Phase*`, o cualquier cambio de arquitectura: el código está estable y probado; el cuello de botella es publicar y medir.
