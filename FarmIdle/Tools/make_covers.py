"""K-01: compone las 3 portadas de CrazyGames (solo el titulo sobre el render de Unity).

Uso (despues de correr CoverRenderer.Render en Unity, ver Docs/MARKETING.md):
    python -I make_covers.py <carpeta_de_renders> <carpeta_de_salida>
Entradas: cover_16x9.png, cover_2x3.png, cover_1x1.png. Salidas: 1920x1080, 800x1200, 800x800 (PNG sin transparencia).
Fuente: Liberation Sans (OFL, ya en el proyecto), con trazo grueso para que se lea a tamano chico.
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

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

    # V-04: oscurece apenas el borde inferior y los costados para que el titulo despegue y la mirada vaya al centro
    ramp = Image.linear_gradient("L").resize((w, h))
    dark = ramp.point(lambda v: int(max(0, v - 110) * 0.75))
    img = Image.composite(Image.new("RGB", img.size, (22, 40, 18)), img, dark)

    probe = ImageDraw.Draw(img)
    fill_mask = Image.new("L", img.size, 0)
    full_mask = Image.new("L", img.size, 0)
    fd, ud = ImageDraw.Draw(fill_mask), ImageDraw.Draw(full_mask)
    for i, text in enumerate(lines):
        pos = ((w - probe.textlength(text, font=font)) / 2, top + i * line_h)
        fd.text(pos, text, font=font, fill=255)
        ud.text(pos, text, font=font, fill=255, stroke_width=stroke)

    # sombra suave y sombra "de relieve" (trazo desplazado hacia abajo)
    base = img.convert("RGBA")
    drop = ImageChops.offset(full_mask, int(stroke * 0.3), int(stroke * 0.9))
    base.paste(Image.new("RGBA", img.size, (30, 14, 6, 190)), (0, 0), drop.filter(ImageFilter.GaussianBlur(stroke * 0.35)))
    base.paste(Image.new("RGBA", img.size, STROKE + (255,)), (0, 0), full_mask)
    # letra con degradado crema -> dorado (arriba claro, abajo calido)
    grad = Image.linear_gradient("L").resize((w, h))
    grad = grad.point(lambda v: int(v))
    top_c, bot_c = (255, 250, 214), (255, 190, 58)
    chans = [grad.point(lambda v, a=a, b=b: int(a + (b - a) * v / 255)) for a, b in zip(top_c, bot_c)]
    gold = Image.merge("RGB", chans)
    # el degradado ocupa solo el alto del bloque de texto, no toda la imagen
    block_h = int(line_h * len(lines))
    band = Image.linear_gradient("L").resize((w, block_h))
    gold_layer = Image.new("RGB", img.size, bot_c)
    gold_layer.paste(Image.merge("RGB", [band.point(lambda v, a=a, b=b: int(a + (b - a) * v / 255)) for a, b in zip(top_c, bot_c)]), (0, top))
    base.paste(gold_layer.convert("RGBA"), (0, 0), fill_mask)
    # brillo fino arriba de cada letra
    shine = ImageChops.multiply(fill_mask, ImageChops.offset(fill_mask, 0, int(size * 0.045)).point(lambda v: 255 - v))
    base.paste(Image.new("RGBA", img.size, (255, 255, 255, 150)), (0, 0), shine.filter(ImageFilter.GaussianBlur(1.2)))
    base.convert("RGB").save(dst, "PNG", optimize=True)
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
