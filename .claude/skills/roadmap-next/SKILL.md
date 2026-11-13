---
name: roadmap-next
description: Toma la próxima tarea del ROADMAP.md (o los IDs que se le pasen), la ejecuta si es de Claude, o avisa y espera confirmación si es del usuario, y actualiza el estado en el roadmap.
disable-model-invocation: true
argument-hint: "[ID ...]  (vacío = elegir la próxima automáticamente)"
---

# /roadmap-next

Ejecutás UNA tarea del roadmap por invocación y cerrás. Español rioplatense, directo, sin relleno. Los tokens del usuario son el recurso escaso: leé poco, devolvé resúmenes, no repitas contexto.

Archivo de trabajo: `C:\Users\Agus Tacconi\Downloads\aa\ROADMAP.md`. Proyecto Unity: `C:\Users\Agus Tacconi\Downloads\aa\FarmIdle`.

## 1. Elegir la tarea

1. **No leas el roadmap entero.** Con Grep ubicá los encabezados `### Hito` y las filas con Estado `Pendiente` o `En curso` (`-n`); después leé solo el rango del hito actual. El hito actual es el primero que tiene filas sin `Hecho` ni `Descartado`.
2. Si hay argumentos (`$ARGUMENTS`), esos IDs son la tarea; saltá la selección automática (si algún ID no existe, decilo y frená).
3. Si no hay argumentos, el candidato es la primera fila del hito actual cuyo Estado sea `Pendiente`/`En curso` y cuyas dependencias ("Depende de") estén todas en `Hecho` o `Descartado`.
   - Las dependencias tipo `DEC-n` son decisiones de la sección 2: tratalas como resueltas solo si el "Registro de cambios" lo dice. Si no, preguntale al usuario si ya decidió.
   - Si hay varias filas candidatas y relacionadas (mismo tema, esfuerzo S), podés agruparlas como una sola tarea. Nunca más de 3 ítems juntos.
   - Si el hito actual no tiene candidatos desbloqueados, decí qué los bloquea y proponé pasar al hito siguiente solo si el usuario lo aprueba.
4. Antes de actuar, mostrá en 2 líneas qué vas a hacer: ID, ítem, Quién, Esfuerzo.

## 2. Según el campo "Quién"

### Quién = Yo (tarea del usuario) → NO la hagas
Frená y mostrá una tarjeta así:

```
TAREA TUYA — <ID> · <Ítem>
Qué hacer: <pasos concretos, 3 a 6 líneas, en orden>
Terminado cuando: <criterio de terminado de la fila>
Qué traerme: <el dato mínimo que necesito para seguir: números, lista corta, sí/no>
Cuando termines, escribime "listo" + lo que te pedí. Si no podés hoy, decime "después" y la dejo pendiente.
```

- Si hay otras tareas "Yo" del mismo hito que no dependen entre sí, listalas todas en la misma tarjeta, para que las haga en paralelo.
- Si además existen tareas de Claude desbloqueadas que no dependen de lo que el usuario va a hacer, ofrecé UNA pregunta con AskUserQuestion: "Mientras tanto, ¿hago <ID>?" (Sí / No, espero). No arranques sin respuesta.
- **Esperá la respuesta.** No sigas, no asumas que está hecho.
- Con "listo": pedí lo que falte del dato, verificá que cumple el criterio de terminado, marcá la fila como `Hecho` y anotá el resultado en una línea en "Registro de cambios". Si el resultado revela trabajo nuevo (por ejemplo bugs de un playtest), proponé filas nuevas con ID y dejá que el usuario las apruebe antes de agregarlas.
- Con "después": dejá la fila como está y sugerí la siguiente tarea de Claude desbloqueada.

### Quién = Claude
1. **S**: hacelo directo. **M o L**: mostrá un plan de 5 a 8 líneas y esperá aprobación (L: proponé partirlo en tandas y hacé solo la primera; dejá el estado en `En curso`).
2. Cargá los datos de contexto necesarios con Grep o lecturas acotadas; no reexplores el proyecto. Leé `FarmIdle/Docs/HANDOFF_*.md` más reciente solo si la tarea toca algo que no conocés.
3. Implementá respetando la arquitectura: una asmdef por capa (Core, Data, Managers, Environment, Player, UI, Editor), UI y gameplay no se conocen y se comunican por eventos, datos en ScriptableObjects de solo lectura, sin asignaciones por cuadro ni `Instantiate` en juego, pocos draw calls. Escenas generadas por el builder de editor.
4. Probá: corré las pruebas relevantes en modo batch (consultá la memoria "Unity headless workflow"; nunca uses `cd`). **Reportá solo el resumen** (cantidad en verde y fallos con una línea cada uno), no pegues logs.
5. Verificá el "Criterio de terminado" de la fila, literalmente. Si no se cumple, no marques `Hecho`: dejá `En curso` y explicá qué falta.

### Quién = Ambos
Hacé primero la parte de Claude (como arriba), después mostrá la tarjeta "TAREA TUYA" con la parte del usuario y esperá su "listo". La fila queda `En curso` hasta que ambas partes estén hechas.

## 3. Actualizar el roadmap (barato)

- Editá **solo las filas afectadas** con Edit (cambiar el Estado) y agregá una línea al final de la sección "Registro de cambios": fecha, IDs y resultado en una frase. **No reescribas el documento.**
- Si cambió algo del alcance o apareció una dependencia nueva, proponelo; no lo apliques sin aprobación.

## 4. Cierre de la tarea

Terminá con este bloque, corto:

```
HECHO: <IDs>  ·  Pruebas: <resumen>  ·  Roadmap actualizado
TUS PENDIENTES (Yo): <IDs desbloqueados, o "ninguno">
SIGUIENTE: <ID propuesto y por qué>
```

Después, **entre tareas**, hacé como máximo 2 preguntas cortas con AskUserQuestion, solo si aportan algo real: una decisión abierta (DEC-n) que afecte a la siguiente tarea, una preferencia de diseño o balance, o el resultado de algo que el usuario probó. Si no hay nada útil que preguntar, no preguntes. Última pregunta siempre: "¿Seguimos con <siguiente> o cortamos acá?" (Seguir / Cortar). Con "Cortar" no hagas nada más.

Si el contexto de la conversación ya está largo, sugerí abrir una sesión nueva antes de seguir.

## Reglas que no se negocian

- **Nunca** manejes secretos: keystore, contraseñas, IDs reales de AdMob, claves de API, ni pagos. Esas filas son del usuario; solo preparás el proyecto para recibirlas.
- No aceptes términos, no publiques ni subas nada a ningún portal o tienda.
- No hagas commit ni push salvo que el usuario lo pida en la conversación.
- Los datos de mercado (requisitos de Play, CrazyGames, ingresos) no los des por ciertos: recordá verificarlos con la fuente indicada en la sección 4 del roadmap.
- Una sola tarea por invocación. Si el usuario dice "seguí", repetí el ciclo desde el paso 1.
