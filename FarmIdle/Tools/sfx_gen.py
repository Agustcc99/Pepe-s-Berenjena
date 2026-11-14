import math
import os
import struct
import sys
import wave

# Efectos de sonido propios de FarmIdle, generados desde cero (sin material de terceros), con la misma idea que music_loop.py:
#   harvest_soft_1..3  cosecha: un "plop" de marimba suave en Do5. Las tres variantes tienen la MISMA nota (el combo de cosecha
#                      sube el tono por una escala pentatónica: si las variantes desafinaran entre sí, la escala no se oiría).
#   backpack_full_1    mochila llena: arpegio corto que sube (Do-Mi-Sol-Do), con la misma marimba.
#   truck_horn_1       venta grande: dos bocinazos cortos y redondos (tercera mayor), sin filo.
# Uso: python -I sfx_gen.py <carpeta de salida>   (por defecto, Assets/Audio/Sfx del proyecto)
RATE = 44100
C5 = 523.25


def envelope_attack(i, attack_samples):
    if i >= attack_samples:
        return 1.0
    return 0.5 - 0.5 * math.cos(math.pi * i / attack_samples)


# Una nota de marimba: fundamental + parciales que se apagan más rápido, y un deslizamiento mínimo de tono al atacar (el "plop").
def marimba(freq, seconds, partials, decay, glide=0.03):
    n = int(seconds * RATE)
    out = [0.0] * n
    attack = int(0.004 * RATE)
    phases = [0.0] * len(partials)
    for i in range(n):
        t = i / RATE
        bend = 1.0 + glide * math.exp(-t / 0.012)
        value = 0.0
        for p, (ratio, amp, tau_scale) in enumerate(partials):
            phases[p] += 2.0 * math.pi * freq * ratio * bend / RATE
            value += amp * math.exp(-t / (decay * tau_scale)) * math.sin(phases[p])
        out[i] = value * envelope_attack(i, attack)
    fade = int(0.02 * RATE)
    for i in range(fade):
        out[n - 1 - i] *= i / fade
    return out


def horn(freqs, seconds):
    n = int(seconds * RATE)
    out = [0.0] * n
    attack = int(0.012 * RATE)
    release = int(0.05 * RATE)
    for i in range(n):
        t = i / RATE
        value = 0.0
        for freq in freqs:
            for h in range(1, 9):
                value += math.sin(2.0 * math.pi * freq * h * t) / (h ** 1.6)
        gain = envelope_attack(i, attack)
        if i > n - release:
            gain *= (n - i) / release
        out[i] = value * gain
    return out


def mix(length_seconds, parts):
    buf = [0.0] * int(length_seconds * RATE)
    for start, samples in parts:
        offset = int(start * RATE)
        for i, value in enumerate(samples):
            if offset + i < len(buf):
                buf[offset + i] += value
    return buf


def write(path, samples, peak):
    top = max(abs(v) for v in samples) or 1.0
    scale = peak / top
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, v * scale)) * 32767)) for v in samples))
    print("escrito", path, len(samples), "muestras")


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "Assets", "Audio", "Sfx")

    # (relación con la fundamental, volumen, cuánto dura respecto de la fundamental)
    soft = [(1.0, 1.0, 1.0), (2.0, 0.22, 0.55), (4.0, 0.10, 0.28)]
    round_ = [(1.0, 1.0, 1.0), (2.0, 0.30, 0.50), (3.0, 0.08, 0.30)]
    woody = [(1.0, 1.0, 1.0), (2.0, 0.16, 0.60), (4.0, 0.14, 0.22), (0.5, 0.18, 0.80)]

    write(os.path.join(out, "harvest_soft_1.wav"), marimba(C5, 0.30, soft, 0.085), 0.75)
    write(os.path.join(out, "harvest_soft_2.wav"), marimba(C5, 0.30, round_, 0.075), 0.75)
    write(os.path.join(out, "harvest_soft_3.wav"), marimba(C5, 0.32, woody, 0.090), 0.75)

    semitone = 2.0 ** (1.0 / 12.0)
    arpeggio = [(0.00, 0), (0.075, 4), (0.15, 7)]
    parts = [(start, marimba(C5 * semitone ** steps, 0.30, soft, 0.10)) for start, steps in arpeggio]
    parts.append((0.225, marimba(C5 * 2.0, 0.65, soft, 0.22)))
    write(os.path.join(out, "backpack_full_1.wav"), mix(0.9, parts), 0.80)

    third = [311.13, 392.00]   # Mib4 + Sol4
    write(os.path.join(out, "truck_horn_1.wav"), mix(0.56, [(0.00, horn(third, 0.13)), (0.19, horn(third, 0.34))]), 0.60)


main()
