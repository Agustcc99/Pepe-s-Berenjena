"""V-04: dibuja el ícono de la app (una berenjena sobre el campo) y lo escribe en Assets/Sprites/AppIcon.

Uso:  python -I make_icon.py [carpeta_de_salida]
Sin argumentos escribe en Assets/Sprites/AppIcon (icon_full 512, icon_background 432, icon_foreground 432, splash_logo 512).
Solo Pillow, sin Unity. Si se corre ReleaseArt.Apply en el editor, pisa estos PNG con el dibujo viejo (la moneda).
"""
import math
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

S = 2048  # lienzo de trabajo, se achica con LANCZOS
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "Sprites", "AppIcon")


def background():
    grad = Image.linear_gradient("L").resize((S, S))
    top, bottom = (126, 206, 84), (58, 140, 62)
    chans = [grad.point(lambda v, a=a, b=b: int(a + (b - a) * v / 255)) for a, b in zip(top, bottom)]
    base = Image.merge("RGB", chans).convert("RGBA")
    # rayos de sol detras de la berenjena
    rays = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(rays)
    cx, cy, n = S // 2, int(S * 0.52), 14
    for i in range(n):
        a0 = math.tau * i / n
        a1 = a0 + math.tau / n / 2
        d.polygon([(cx, cy), (cx + 3000 * math.cos(a0), cy + 3000 * math.sin(a0)), (cx + 3000 * math.cos(a1), cy + 3000 * math.sin(a1))],
                  fill=(255, 250, 190, 52))
    base = Image.alpha_composite(base, rays)
    # brillo central
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([cx - 700, cy - 700, cx + 700, cy + 700], fill=(255, 255, 210, 90))
    base = Image.alpha_composite(base, glow.filter(ImageFilter.GaussianBlur(160)))
    # tierra con surcos
    soil = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(soil)
    y0 = int(S * 0.74)
    d.rectangle([0, y0, S, S], fill=(120, 78, 46, 255))
    for k in range(7):
        y = y0 + 40 + k * 92
        d.rectangle([0, y, S, y + 38], fill=(88, 56, 32, 255))
    d.rectangle([0, y0 - 14, S, y0 + 10], fill=(150, 104, 62, 255))
    return Image.alpha_composite(base, soil)


def eggplant():
    """Berenjena con tallo y caliz, sobre transparente, inclinada."""
    cx, y0, h, w = S // 2, 600, 1180, 430

    def hw(t):  # semiancho segun la altura (0 cuello, 1 base)
        return w * (math.sin(math.pi * t) ** 0.62) * (0.50 + 0.50 * t)

    ys = [i / 240 for i in range(241)]
    pts = [(cx - hw(t), y0 + h * t) for t in ys] + [(cx + hw(t), y0 + h * t) for t in reversed(ys)]
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).polygon(pts, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(3))

    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # contorno oscuro
    outline = mask.filter(ImageFilter.MaxFilter(41)).filter(ImageFilter.GaussianBlur(4))
    layer.paste(Image.new("RGBA", (S, S), (44, 16, 64, 255)), (0, 0), outline)
    # cuerpo
    layer.paste(Image.new("RGBA", (S, S), (112, 48, 150, 255)), (0, 0), mask)
    # sombra interior (abajo a la derecha)
    shifted = ImageChops.offset(mask, -150, -110)
    shade = ImageChops.subtract(mask, shifted).filter(ImageFilter.GaussianBlur(70))
    shade = ImageChops.multiply(shade, mask)
    layer.paste(Image.new("RGBA", (S, S), (52, 18, 84, 255)), (0, 0), shade.point(lambda v: int(v * 0.85)))
    # luz arriba a la izquierda
    lit = Image.new("L", (S, S), 0)
    ImageDraw.Draw(lit).ellipse([cx - 330, y0 + 330, cx - 40, y0 + 720], fill=150)
    lit = ImageChops.multiply(lit.filter(ImageFilter.GaussianBlur(90)), mask)
    layer.paste(Image.new("RGBA", (S, S), (168, 96, 206, 255)), (0, 0), lit)
    # brillo duro
    spec = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(spec)
    d.rounded_rectangle([cx - 290, y0 + 420, cx - 215, y0 + 760], radius=38, fill=235)
    d.ellipse([cx - 290, y0 + 360, cx - 215, y0 + 420], fill=235)
    d.ellipse([cx - 285, y0 + 800, cx - 235, y0 + 850], fill=235)
    spec = ImageChops.multiply(spec.filter(ImageFilter.GaussianBlur(5)), mask)
    layer.paste(Image.new("RGBA", (S, S), (255, 255, 255, 255)), (0, 0), spec.point(lambda v: int(v * 0.8)))

    # caliz verde: casquete + 5 sepalos que caen sobre el cuerpo
    cap_y = y0 + 40
    d = ImageDraw.Draw(layer)
    green_dark, green, green_light = (30, 96, 40, 255), (66, 156, 58, 255), (118, 204, 84, 255)
    sepals = [(-250, 120, 230), (-130, 60, 270), (0, 0, 290), (130, 60, 270), (250, 120, 230)]
    for dx, dy, ln in sorted(sepals, key=lambda s: -abs(s[0])):
        tip = (cx + dx * 1.25, cap_y + 120 + ln)
        d.polygon([(cx + dx - 85, cap_y + dy), (cx + dx + 85, cap_y + dy), tip], fill=green_dark)
        d.polygon([(cx + dx - 62, cap_y + dy + 6), (cx + dx + 62, cap_y + dy + 6), (tip[0], tip[1] - 38)], fill=green)
    d.ellipse([cx - 320, cap_y - 85, cx + 320, cap_y + 175], fill=green_dark)
    d.ellipse([cx - 296, cap_y - 70, cx + 296, cap_y + 150], fill=green)
    d.ellipse([cx - 230, cap_y - 56, cx + 40, cap_y + 20], fill=green_light)
    # tallo curvo
    for grow, color in ((10, green_dark), (-8, green)):
        for i in range(41):
            t = i / 40
            x = cx + 30 * t + 70 * t * t
            y = cap_y - 60 - 270 * t
            r = 64 - 14 * t + grow
            d.ellipse([x - r, y - r, x + r, y + r], fill=color)

    layer = layer.rotate(14, resample=Image.BICUBIC, center=(cx, y0 + h * 0.5))
    # sombra en el suelo
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse([cx - 470, y0 + h - 70, cx + 470, y0 + h + 110], fill=(30, 18, 8, 130))
    shadow = shadow.filter(ImageFilter.GaussianBlur(40))
    return Image.alpha_composite(shadow, layer)


def save(img, path, size):
    img.resize((size, size), Image.LANCZOS).save(path, "PNG", optimize=True)
    print(os.path.basename(path), size, str(os.path.getsize(path) // 1024) + " KB")


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else OUT
    os.makedirs(out, exist_ok=True)
    bg, fg = background(), eggplant()
    # el primer plano adaptativo debe caber en el 66 % central: se achica y se centra
    fg_adaptive = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    small = fg.resize((int(S * 0.80), int(S * 0.80)), Image.LANCZOS)
    fg_adaptive.paste(small, ((S - small.width) // 2, (S - small.height) // 2 + 20), small)
    save(Image.alpha_composite(bg, fg), os.path.join(out, "icon_full.png"), 512)
    save(bg, os.path.join(out, "icon_background.png"), 432)
    save(fg_adaptive, os.path.join(out, "icon_foreground.png"), 432)
    save(fg, os.path.join(out, "splash_logo.png"), 512)


if __name__ == "__main__":
    main()
