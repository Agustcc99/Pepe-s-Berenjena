# Checklist para elegir el pack de arte (ROADMAP A-01)

Sirve para decidir en A-02 entre 2 o 3 candidatos de la Asset Store. Fuente de los 9 criterios: `ROADMAP.md` sección 6. Acá van con el corte exacto y lo que mido yo vs. lo que mirás vos.

## Restricciones fijas del proyecto (no se negocian sin discutirlo, ROADMAP A-05)

- Unity 6000.6.6f1, URP. Los materiales del pack tienen que ser URP sin conversión manual.
- Hoy todo lo que se repite (plantas, productos, monedas, pads) usa UN material de paleta; lo exige `PerformanceBudgetTests`. El pack obliga a cambiar esa regla a un presupuesto (A-05), así que cada material extra cuenta.
- Sin `Instantiate` en juego (pools) y sin asignaciones por cuadro: los modelos del pack se instancian al arrancar, no durante el juego.
- Metas de peso: web ≤ 20 MB iniciales (**verificar el límite del portal**), APK ≤ 60 MB. Hoy: web 8,5 MB, APK 26,9 MB.
- Dispositivo de referencia: Galaxy A70 (Android 11), gama media.
- Todo asset nuevo se registra en `FarmIdle/THIRD_PARTY.md` con su licencia antes de usarse (L-01).

## Tabla de evaluación (una por candidato)

| # | Criterio | Corte (si falla, se descarta) | Cómo se mide | Quién |
|---|---|---|---|---|
| 1 | Licencia | Uso comercial en juegos publicados en web y Android, sin prohibir WebGL; no se redistribuyen los archivos fuente | Leer la licencia de la ficha (L-02). **Verificar el texto vigente, no lo doy por cierto** | Vos |
| 2 | Compatibilidad | Materiales URP incluidos; declara Unity 6 o una versión compatible; reseñas recientes sin quejas de "rosa/magenta" | Ficha del asset + importar en una rama (A-04) | Ambos |
| 3 | Estilo | Low poly coherente entre sí; o se reemplazan TODOS los modelos Kenney (no mezclar estilos) | Capturas de la ficha + render propio | Vos (ojo) |
| 4 | Contenido | Jugador, cultivos con etapas de crecimiento (o escalables), 8 cultivos, edificios, props de mapa | Lista de prefabs vs. lo que usa el juego | Yo |
| 5 | Peso y materiales | Pocos materiales; mejor atlas o paleta compartida; texturas chicas | Cantidad de materiales/texturas, resolución, tris por modelo; tamaño del build antes/después | Yo |
| 6 | Animaciones | Idle y correr como mínimo, rig simple o humanoide | Abrir los clips y el controlador | Yo |
| 7 | Mantenimiento | Autor activo, última actualización razonablemente reciente | Ficha del asset | Vos |
| 8 | Rendimiento | Sin shaders caros ni transparencias pesadas | Perfil en el A70 con el jugador y un cultivo (A-04) | Ambos |
| 9 | Precio | ≤ USD 20 (esperar ofertas) | Ficha | Vos |

Criterios eliminatorios: 1, 2 y 9. El resto puntúa (0 a 2) y desempata.

## Qué me pasás para A-02

- 2 o 3 enlaces de la Asset Store.
- Si ya viste capturas que te gustan o te disgustan, decímelo (el estilo lo juzgás vos).
- Yo devuelvo un cuadro comparativo con esta tabla llena y una recomendación. Si ninguno cumple o se pasa del presupuesto, se activa A-09 (Kenney/Quaternius CC0 con la paleta horneada).

## Orden de trabajo después

A-02 (comparar) → L-02 y A-03 (leer licencia y comprar: tuyas) → A-04 (rama de prueba con medición de tamaño, draw calls y memoria) → decisión de seguir o cortar.
