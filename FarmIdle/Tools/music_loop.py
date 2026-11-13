import math
import random
import struct
import sys
import wave

# Loop de música de fondo para FarmIdle, generado desde cero (sin material de terceros): pad suave + melodía de "marimba" pentatónica
# + eco. 8 compases a 80 BPM = 24 s, y todo lo que suena cerca del final se enrolla al principio, así el corte del loop no se oye.
RATE = 22050
BPM = 80.0
BEAT = 60.0 / BPM
BARS = 8
BEATS_PER_BAR = 4
LENGTH = int(RATE * BEAT * BEATS_PER_BAR * BARS)

buf = [0.0] * LENGTH


def add(start_sec, samples):
    start = int(start_sec * RATE)
    for i, value in enumerate(samples):
        buf[(start + i) % LENGTH] += value


def midi_to_hz(note):
    return 440.0 * 2.0 ** ((note - 69) / 12.0)


def pad_note(freq, seconds, volume):
    n = int(seconds * RATE)
    out = []
    attack = 0.9
    release = 1.1
    for i in range(n):
        t = i / RATE
        env = min(1.0, t / attack) * min(1.0, (seconds - t) / release)
        env = max(0.0, env)
        phase = 2.0 * math.pi * freq * t
        value = math.sin(phase) + 0.35 * math.sin(2.0 * phase + 0.4) + 0.12 * math.sin(3.0 * phase)
        # un poquito de vibrato lento para que no sea un tono frío
        value *= 1.0 + 0.04 * math.sin(2.0 * math.pi * 0.35 * t)
        out.append(value * env * volume)
    return out


def pluck_note(freq, volume, seconds=1.6):
    n = int(seconds * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        env = min(1.0, t / 0.006) * math.exp(-t / 0.30)
        phase = 2.0 * math.pi * freq * t
        value = math.sin(phase) + 0.28 * math.sin(4.0 * phase) * math.exp(-t / 0.08) + 0.10 * math.sin(2.0 * phase)
        out.append(value * env * volume)
    return out


def bass_note(freq, seconds, volume):
    n = int(seconds * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        env = min(1.0, t / 0.04) * max(0.0, 1.0 - t / seconds) ** 0.7
        out.append(math.sin(2.0 * math.pi * freq * t) * env * volume)
    return out


# C - Am - F - G, dos veces.
chords = [
    (48, [60, 64, 67]),   # C
    (45, [57, 60, 64]),   # Am
    (41, [53, 57, 60]),   # F
    (43, [55, 59, 62]),   # G
]
pentatonic = [0, 2, 4, 7, 9]   # C D E G A

rng = random.Random(7)
bar_seconds = BEAT * BEATS_PER_BAR

for bar in range(BARS):
    bass, triad = chords[bar % 4]
    start = bar * bar_seconds

    for note in triad:
        add(start, pad_note(midi_to_hz(note), bar_seconds + 0.6, 0.060))

    add(start, bass_note(midi_to_hz(bass), BEAT * 2.0, 0.22))
    add(start + BEAT * 2.0, bass_note(midi_to_hz(bass + 7), BEAT * 2.0, 0.16))

    # melodía: corcheas con silencios, sobre la pentatónica de la octava 5
    step = BEAT / 2.0
    last = None
    for eighth in range(8):
        if rng.random() < 0.42:
            continue
        if eighth % 2 == 0 and rng.random() < 0.6:
            degree = pentatonic[rng.choice([0, 1, 2, 3])]   # tiempos fuertes: notas estables
        else:
            degree = pentatonic[rng.randrange(5)]
        if last is not None and degree == last:
            degree = pentatonic[(pentatonic.index(degree) + 1) % 5]
        last = degree
        midi = 72 + degree
        add(start + eighth * step, pluck_note(midi_to_hz(midi), 0.12 + 0.05 * rng.random()))

# eco: dos repeticiones suaves enrolladas
echo = [0.0] * LENGTH
for delay, gain in ((BEAT * 0.75, 0.32), (BEAT * 1.5, 0.18), (BEAT * 2.25, 0.09)):
    shift = int(delay * RATE)
    for i in range(LENGTH):
        echo[(i + shift) % LENGTH] += buf[i] * gain
for i in range(LENGTH):
    buf[i] += echo[i]

peak = max(abs(v) for v in buf)
scale = 0.55 / peak if peak > 0 else 1.0

out_path = sys.argv[1]
with wave.open(out_path, "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(RATE)
    frames = bytearray()
    for v in buf:
        frames += struct.pack("<h", int(max(-1.0, min(1.0, v * scale)) * 32767))
    w.writeframes(bytes(frames))

print("listo: %s (%.1f s, pico %.3f)" % (out_path, LENGTH / RATE, peak))
