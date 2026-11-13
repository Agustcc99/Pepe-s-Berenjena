# Licencias de terceros — Farm Idle

Inventario de todo lo que no es código propio y va en el proyecto o en el build. **Todo asset nuevo se registra acá con su licencia antes de usarse** (ROADMAP L-01). Los créditos visibles para el jugador están en Ajustes → Créditos (`settings.credits_body` en `Assets/Resources/Localization.csv`).

## Assets de contenido

| Qué | Dónde | Autor / fuente | Licencia | Notas |
|---|---|---|---|---|
| Nature Kit 2.1 (modelos 3D de plantas) | `Assets/Models_3D/Kenney/NatureKit` | Kenney — https://kenney.nl/assets/nature-kit | CC0 1.0 | Se hornean a la paleta del juego (`KenneyArt`). Licencia original en `License.txt` de la carpeta. |
| Food Kit 2.0 (modelos 3D de productos) | `Assets/Models_3D/Kenney/FoodKit` | Kenney — https://kenney.nl/assets/food-kit | CC0 1.0 | Ídem. |
| Interface Sounds 1.0 (efectos) | `Assets/Audio/Sfx` | Kenney — https://kenney.nl/assets/interface-sounds | CC0 1.0 | Copiados sin modificar y renombrados. Detalle en `Assets/Audio/CREDITS.txt`. |
| Casino Audio (efectos de venta) | `Assets/Audio/Sfx` | Kenney — https://kenney.nl/assets/casino-audio | CC0 1.0 | Ídem. |
| Digital Audio (nivel, fertilizante, oferta, recompensa, Turbo) | `Assets/Audio/Sfx` | Kenney — https://kenney.nl/assets/digital-audio | CC0 1.0 | Ídem. |
| Loop de música `farm_loop.wav` | `Assets/Audio/Music` | Propio, generado con `Docs/music_loop.py` | Propio, sin restricciones | Síntesis desde cero, sin material de terceros. |
| Sprites de UI e íconos (`Sprites/UI`, `Sprites/AppIcon`) | `Assets/Sprites` | Propios, generados por código (builders de editor, `ReleaseArt`) | Propio, sin restricciones | No hay arte externo. |
| Liberation Sans (fuente de TextMesh Pro) | `Assets/TextMesh Pro/Fonts` | Red Hat / Liberation Fonts | SIL Open Font License 1.1 | Texto de la licencia en `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`. Se puede redistribuir con el juego sin cambiar el nombre. |

## SDK y plugins

| Qué | Dónde | Licencia | Notas |
|---|---|---|---|
| Google Mobile Ads Unity Plugin 11.6.0 | `Assets/GoogleMobileAds`, `Assets/Plugins/Android` | Apache 2.0 (`Assets/GoogleMobileAds/LICENSE`) | Solo entra en builds Android (símbolo `FARMIDLE_ADMOB`). El SDK nativo y UMP tienen sus propios términos de Google: se aceptan al crear la cuenta de AdMob (tarea del usuario, ROADMAP M-02). |

## Paquetes de Unity (Packages/manifest.json)

Cinemachine 6.6.0, Input System 1.20.0, Universal RP 17.6.0, uGUI/TextMesh Pro 2.6.0, Test Framework 1.8.0 y los módulos `com.unity.modules.*`. Se usan bajo los términos de Unity (Unity Companion License / licencia de paquetes de Unity y Términos del Editor). Los paquetes de IDE (Rider, Visual Studio) y `collab-proxy` son solo de editor y no van en el build.

## Pendiente de verificar

- Texto exacto de la licencia de cada paquete de Unity y del SDK de Google: leerlo en su fuente antes de publicar (no está transcripto acá).
- Packs de arte futuros (ROADMAP A-02/A-03/L-02): agregar su fila **antes** de importarlos.
- SDK de CrazyGames (D-03) y Firebase (N-03): agregar su fila al integrarlos.
