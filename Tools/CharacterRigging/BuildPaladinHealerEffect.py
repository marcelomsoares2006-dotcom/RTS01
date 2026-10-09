"""Gera proceduralmente as texturas suaves do efeito de cura do Paladino (sem arquivos de terceiros):
  PaladinHealAura_Ring.png   anel dourado com brilho suave e marcas radiais discretas (alfa = forma)
  PaladinHealAura_Disc.png   disco de luz com cruz suave e anel interno
  PaladinHealAura_Mote.png   particula redonda suave com cintilacao em 4 pontas
Saida: Assets/CharacterRigging/PaladinHealer/Textures. Uso: blender -b --factory-startup --python BuildPaladinHealerEffect.py
Os materiais usam Legacy Shaders/Particles/Additive (cor = textura * tinta; alfa controla a intensidade).
"""
from pathlib import Path
import numpy as np
import bpy

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/CharacterRigging/PaladinHealer/Textures"
GOLD = np.array([1.0, 0.80, 0.32], dtype=np.float32)
WARM = np.array([1.0, 0.93, 0.70], dtype=np.float32)


def grid(n):
    c = (np.arange(n, dtype=np.float32) + 0.5) / n * 2.0 - 1.0
    x, y = np.meshgrid(c, c)
    return x, y, np.sqrt(x * x + y * y), np.arctan2(y, x)


def edge_fade(r, start=0.90):
    return np.clip((1.0 - r) / (1.0 - start), 0.0, 1.0) ** 2


def rgba(alpha, tint):
    out = np.zeros(alpha.shape + (4,), dtype=np.float32)
    out[..., :3] = tint
    out[..., 3] = np.clip(alpha, 0.0, 1.0)
    return out


def save(name, arr):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True, float_buffer=False)
    img.alpha_mode = "STRAIGHT"
    img.pixels.foreach_set(arr.ravel())
    img.filepath_raw = str(OUT / f"{name}.png")
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def ring(n=512):
    x, y, r, th = grid(n)
    core = np.exp(-((r - 0.86) / 0.035) ** 2)
    glow = 0.35 * np.exp(-((r - 0.86) / 0.11) ** 2)
    inner = 0.18 * np.exp(-((r - 0.62) / 0.02) ** 2)
    ticks = 0.80 + 0.20 * np.cos(36.0 * th)
    a = (core * ticks + glow + inner) * edge_fade(r, 0.93)
    tint = GOLD[None, None, :] * (1 - core[..., None] * 0.5) + WARM[None, None, :] * core[..., None] * 0.5
    arr = rgba(a, tint)
    return arr


def disc(n=512):
    x, y, r, th = grid(n)
    glow = np.clip(1.0 - r, 0.0, 1.0) ** 1.6 * 0.55
    inner = 0.20 * np.exp(-((r - 0.45) / 0.025) ** 2)
    arm = lambda a, b: np.exp(-(a / 0.035) ** 2) * np.exp(-(np.abs(b) / 0.34) ** 2)
    cross = np.maximum(arm(x, y), arm(y, x)) * 0.35
    a = (glow + inner + cross) * edge_fade(r, 0.92)
    return rgba(a, GOLD)


def mote(n=128):
    x, y, r, th = grid(n)
    soft = np.exp(-(r / 0.28) ** 2)
    star = np.maximum(np.exp(-(x / 0.035) ** 2) * np.exp(-(np.abs(y) / 0.55) ** 2),
                      np.exp(-(y / 0.035) ** 2) * np.exp(-(np.abs(x) / 0.55) ** 2)) * 0.8
    a = (soft + star) * edge_fade(r, 0.85)
    return rgba(a, WARM)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name, arr in (("PaladinHealAura_Ring", ring()), ("PaladinHealAura_Disc", disc()), ("PaladinHealAura_Mote", mote())):
        save(name, arr)
    print("EFFECT_TEXTURES_OK")


main()
