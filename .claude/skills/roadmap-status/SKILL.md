---
name: roadmap-status
description: Resume en pocas líneas el estado del ROADMAP.md: hito actual, avance, tus pendientes, lo próximo de Claude, bloqueos y decisiones abiertas. Solo lectura.
disable-model-invocation: true
allowed-tools: Read Grep
---

# /roadmap-status

Solo lectura: no edites nada y no ejecutes nada. Respuesta de **máximo 25 líneas**, español rioplatense, directo.

Archivo: `C:\Users\Agus Tacconi\Downloads\aa\ROADMAP.md`.

## Cómo leer sin gastar de más

1. Con Grep (`-n`) listá los encabezados `### Hito` y las filas de tablas con Estado `Pendiente`, `En curso`, `Hecho` y `Descartado` (patrón sobre ` Pendiente | `, etc.). No leas el archivo entero.
2. Leé solo el rango del hito actual (el primero con filas sin `Hecho` ni `Descartado`) si necesitás las dependencias, y la sección "Registro de cambios" para saber qué decisiones DEC ya se tomaron.

## Formato de salida

```
HITO ACTUAL: <nombre> (semanas <rango>)
AVANCE: <hechas>/<total> en este hito · Total general: <hechas>/<total> (descartadas aparte)
EN CURSO: <ID · ítem> ...  (o "nada")

TUS PENDIENTES (Yo, desbloqueados): <ID · ítem> ... (o "ninguno")
PRÓXIMO PARA CLAUDE: <ID · ítem · esfuerzo> (el que tomaría /roadmap-next)
BLOQUEADOS: <ID> por <dependencia>   (máx. 5)
DECISIONES ABIERTAS RELEVANTES: <DEC-n: una frase>   (solo las que afectan a este hito)
RIESGO A VIGILAR: <1 línea, solo si algún riesgo del hito se activó>
```

- Si falta el archivo, avisalo en una línea.
- Un ítem está "desbloqueado" si todas sus dependencias están `Hecho` o `Descartado`. Las `DEC-n` cuentan como resueltas solo si el Registro de cambios lo dice.
- No des recomendaciones largas: una línea final con "Sugiero: <acción>" alcanza.
