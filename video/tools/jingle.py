"""Původní 8bitová znělka a zvukové efekty pro video k Přepni.

Vše se syntetizuje tady (obdélník, trojúhelník, šum), žádné cizí vzorky ani melodie:
    python video/tools/jingle.py
zapíše video/public/audio/{music,tap,flip,success}.wav.
"""

import wave
from pathlib import Path

import numpy as np

RATE = 44100
BPM = 120
EIGHTH = 60 / BPM / 2  # 0,25 s
OUT = Path(__file__).resolve().parent.parent / "public" / "audio"


def note(name: str) -> float:
    """Frekvence noty, třeba „A4“ nebo „C#5“."""
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}
    pitch, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[pitch] + 12 * (octave - 4)) / 12)


def envelope(n: int, attack: float = 0.005, release: float = 0.08) -> np.ndarray:
    t = np.arange(n) / RATE
    env = np.minimum(1, t / attack)
    tail = (n / RATE) - t
    return env * np.minimum(1, tail / release)


def square(freq: float, seconds: float, duty: float = 0.25) -> np.ndarray:
    t = np.arange(int(seconds * RATE)) / RATE
    return np.where((t * freq) % 1 < duty, 1.0, -1.0) * envelope(len(t))


def triangle(freq: float, seconds: float) -> np.ndarray:
    t = np.arange(int(seconds * RATE)) / RATE
    return (2 * np.abs(2 * ((t * freq) % 1) - 1) - 1) * envelope(len(t), release=0.03)


def noise(seconds: float, decay: float) -> np.ndarray:
    n = int(seconds * RATE)
    t = np.arange(n) / RATE
    return np.random.default_rng(7).uniform(-1, 1, n) * np.exp(-t / decay)


def kick() -> np.ndarray:
    t = np.arange(int(0.14 * RATE)) / RATE
    freq = 150 * np.exp(-t * 18) + 45
    return np.sin(2 * np.pi * np.cumsum(freq) / RATE) * np.exp(-t * 22)


def place(track: np.ndarray, sound: np.ndarray, at: float, gain: float) -> None:
    start = int(at * RATE)
    end = min(len(track), start + len(sound))
    track[start:end] += sound[: end - start] * gain


def music(seconds: float = 30.0) -> np.ndarray:
    track = np.zeros(int(seconds * RATE))

    # Akordy po taktech (2 s): a moll, F, C, G; basa pulzuje v osminách s oktávovým skokem.
    roots = ["A2", "F2", "C3", "G2"]

    # Melodie po osminách (None = pauza): pentatonika v a moll, vlastní motiv „píšu–oprava“.
    phrase = [
        "E5", "A5", "E5", "C5", "D5", None, "E5", None,
        "C5", "D5", "C5", "A4", "C5", None, None, None,
        "E5", "A5", "B5", "A5", "G5", None, "E5", "D5",
        "E5", None, "G5", "E5", "A5", None, None, None,
    ]

    bars = int(seconds / (8 * EIGHTH))
    for bar in range(bars):
        start = bar * 8 * EIGHTH
        root = note(roots[bar % 4])

        for i in range(8):
            at = start + i * EIGHTH
            place(track, triangle(root * (2 if i % 2 else 1), EIGHTH * 0.9), at, 0.32)
            place(track, noise(0.05, 0.012), at + EIGHTH / 2, 0.06)  # hi-hat na „a“

        for beat in (0, 4):
            place(track, kick(), start + beat * EIGHTH, 0.55)
        for beat in (2, 6):
            place(track, noise(0.12, 0.04), start + beat * EIGHTH, 0.22)  # virbl

        # Melodie od druhého taktu, ať úvod začne jen rytmem.
        if bar >= 1:
            for i in range(8):
                pitch = phrase[(bar % 4) * 8 + i]
                if pitch:
                    place(track, square(note(pitch), EIGHTH * 0.85), start + i * EIGHTH, 0.13)

    return track


def tap() -> np.ndarray:
    """Cvaknutí klávesy: krátký šum a vysoké pípnutí."""
    beep = square(1600, 0.035, 0.5)
    click = noise(0.03, 0.006)
    return np.pad(click, (0, len(beep) - len(click))) * 0.6 + beep * 0.25


def flip() -> np.ndarray:
    """„5“ se mění na „ř“: rychlé vzestupné arpeggio."""
    parts = [square(note(n), 0.045, 0.5) for n in ("C6", "E6", "G6", "C7")]
    return np.concatenate(parts) * 0.35


def success() -> np.ndarray:
    """Hotovo: dvě noty nahoru, druhá delší."""
    return np.concatenate([square(note("B5"), 0.08, 0.5), square(note("E6"), 0.3, 0.5)]) * 0.3


def write(name: str, samples: np.ndarray) -> None:
    peak = np.max(np.abs(samples)) or 1
    data = (samples / max(peak, 1) * 0.9 * 32767).astype(np.int16)
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(data.tobytes())


if __name__ == "__main__":
    write("music", music())
    write("tap", tap())
    write("flip", flip())
    write("success", success())
    print("Zapsáno do", OUT)
