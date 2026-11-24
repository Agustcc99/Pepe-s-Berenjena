# Portadas del portal (K-01)

Medidas de CrazyGames (leídas el 2026-10-28 en docs.crazygames.com/requirements/game-covers, **a verificar** antes de enviar): 1920×1080 (16:9), 800×1200 (2:3) y 800×800 (1:1); solo el título como texto, sin bordes ni logos de tiendas.

Entregadas en `../Marketing/covers/` (borrador de Claude; el diseño final lo aprobás vos):
`cover_16x9_1920x1080.png`, `cover_2x3_800x1200.png`, `cover_1x1_800x800.png`.

## Regenerarlas (si cambia el arte o el nombre)
1. Render sin texto desde MainLevel (planta un cultivo maduro por parcela solo en memoria, no guarda la escena). Con Unity cerrado:
   `FI_OUT=<carpeta>` y `FI_SHOTS="nombre,ancho,alto,px,py,pz,rx,ry,rz,fov;..."`, luego
   `Unity -batchmode -projectPath FarmIdle -executeMethod FarmIdle.EditorTools.CoverRenderer.Render -quit`.
   Tomas usadas: `cover_16x9,1920,1080,0,4.5,-4,29,0,0,36` (V-04; usar también `FI_HIDE_LOCKS=1` para ocultar los terrenos cerrados) · `cover_2x3,800,1200,0,7,-3.5,41,0,0,50` · `cover_1x1,800,800,0,6.6,-3.6,41,0,0,48` (con `FI_HIDE_MARKERS=1` para ocultar la flecha del tutorial; la primera versión quedaba dentro de la flecha).
2. Título encima: `python -I FarmIdle/Tools/make_covers.py <renders> <salida>` (Liberation Sans, OFL; el texto está en `TITLE`).

## Pendiente de K-01 (tuyo)
- Mirar las 3 y decir si van o qué cambiar (cámara, cultivos, color/posición del título).
- Miniatura legible a tamaño chico: probarla en el portal.
- Ícono: CrazyGames pide solo los 3 covers; el ícono de 512 de Play (D-09) se hace después.
- El juego dentro sigue diciendo "Farm Idle" hasta que se renombre (no tocado acá).
