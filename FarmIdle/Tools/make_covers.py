"""K-01: compone las 3 portadas de CrazyGames (solo el titulo sobre el render de Unity).

Uso (despues de correr CoverRenderer.Render en Unity, ver Docs/MARKETING.md):
    python -I make_covers.py <carpeta_de_renders> <carpeta_de_salida>
Entradas: cover_16x9.png, cover_2x3.png, cover_1x1.png. Salidas: 1920x1080, 800x1200, 800x800 (PNG sin transparencia).
Fuente: Liberation Sans (OFL, ya en el proyecto), con trazo grueso para que se lea a tamano chico.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

TITLE = "Pepe's Berenjena"
FONT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "TextMesh Pro", "Fonts", "LiberationSans.ttf")

# nombre -> (ancho, alto, lineas del titulo, tamano de fuente, centro vertical del bloque como fraccion del alto)
COVERS = {
    "cover_16x9": (1920, 1080, [TITLE], 210, 0.80),
    "cover_2x3": (800, 1200, ["Pepe's", "Berenjena"], 150, 0.80),
    "cover_1x1": (800, 800, ["Pepe's", "Berenjena"], 118, 0.80),
}

FILL = (255, 255, 255)
STROKE = (74, 36, 16)
SHADOW = (40, 20, 8, 110)


def compose(src, dst, w, h, lines, size, cy):
    img = Image.open(src).convert("RGB")
    if img.size != (w, h):
        img = img.resize((w, h), Image.LANCZOS)
    font = ImageFont.truetype(FONT, size)
    stroke = max(6, size // 12)
    line_h = int(size * 1.08)
    top = int(h * cy - line_h * len(lines) / 2)

    shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    draw = ImageDraw.Draw(img)
    for i, text in enumerate(lines):
        width = draw.textlength(text, font=font)
        x = (w - width) / 2
        y = top + i * line_h
        sd.text((x + stroke * 0.5, y + stroke * 0.8), text, font=font, fill=SHADOW, stroke_width=stroke, stroke_fill=SHADOW)
    img = Image.alpha_composite(img.convert("RGBA"), shadow).convert("RGB")
    draw = ImageDraw.Draw(img)
    for i, text in enumerate(lines):
        width = draw.textlength(text, font=font)
        draw.text(((w - width) / 2, top + i * line_h), text, font=font, fill=FILL, stroke_width=stroke, stroke_fill=STROKE)
    img.save(dst, "PNG", optimize=True)
    return os.path.getsize(dst)


def main():
    src_dir, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    for name, (w, h, lines, size, cy) in COVERS.items():
        dst = os.path.join(out_dir, name + "_" + str(w) + "x" + str(h) + ".png")
        kb = compose(os.path.join(src_dir, name + ".png"), dst, w, h, lines, size, cy) // 1024
        print(os.path.basename(dst), str(kb) + " KB")


if __name__ == "__main__":
    main()
